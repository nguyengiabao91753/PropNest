using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PropNest.Api.Middleware;
using PropNest.Api.Services;
using PropNest.Application.Abstractions;
using PropNest.Application.DependencyInjection;
using PropNest.Infrastructure.DependencyInjection;
using PropNest.Api.Serialization;
using Microsoft.EntityFrameworkCore;
using PropNest.Infrastructure.Persistence;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    throw new InvalidOperationException("Jwt:SigningKey is required.");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityCore<PropNest.Domain.Users.ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddRoles<Microsoft.AspNetCore.Identity.IdentityRole<long>>()
.AddEntityFrameworkStores<PropNest.Infrastructure.Persistence.PropNestDbContext>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new VietnamDateTimeOffsetJsonConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "PropNest API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanModerateListings", policy => policy.RequireRole("Moderator", "Admin"));
});

var app = builder.Build();

// chỗ này migrate database
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PropNestDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        LogApplyingMigrations(logger);
        await dbContext.Database.MigrateAsync();
        LogMigrationsApplied(logger);
    }
    catch (Exception ex)
    {
        LogMigrationError(logger, ex);
        throw;
    }
}

await SeedRolesAsync(app.Services);
await SeedAdminUserAsync(app.Services);

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program
{
    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<long>>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        foreach (var roleName in new[] { "Seller", "Moderator", "Admin" })
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                LogRoleAlreadyExists(logger, roleName);
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<long>(roleName));
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Could not seed role '{roleName}': {errors}");
            }

            LogRoleSeeded(logger, roleName);
        }
    }

    private static async Task SeedAdminUserAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PropNest.Domain.Users.ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        const string adminEmail = "admin@propnest.vn";
        const string adminPassword = "Admin@123456";

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new PropNest.Domain.Users.ApplicationUser(adminEmail, "PropNest Administrator", "0901234567", "Admin");
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                LogAdminSeedError(logger, errors);
                return;
            }

            await userManager.AddToRolesAsync(admin, new[] { "Admin", "Moderator" });
            LogAdminSeeded(logger, adminEmail);
        }
        else
        {
            var userRoles = await userManager.GetRolesAsync(admin);
            if (!userRoles.Contains("Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
            if (!userRoles.Contains("Moderator"))
            {
                await userManager.AddToRoleAsync(admin, "Moderator");
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Đang kiểm tra và áp dụng Database Migrations còn thiếu...")]
    private static partial void LogApplyingMigrations(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Database đã được đồng bộ thành công!")]
    private static partial void LogMigrationsApplied(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Có lỗi xảy ra khi tự động migrate database.")]
    private static partial void LogMigrationError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Seeded role: {RoleName}")]
    private static partial void LogRoleSeeded(ILogger logger, string roleName);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Role already exists: {RoleName}")]
    private static partial void LogRoleAlreadyExists(ILogger logger, string roleName);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Khởi tạo thành công tài khoản Admin mặc định: {Email}")]
    private static partial void LogAdminSeeded(ILogger logger, string email);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Không thể seed tài khoản Admin mặc định: {Errors}")]
    private static partial void LogAdminSeedError(ILogger logger, string errors);
}
