using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PropNest.Api.Middleware;
using PropNest.Api.Services;
using PropNest.Application.Abstractions;
using PropNest.Application.DependencyInjection;
using PropNest.Infrastructure.DependencyInjection;
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
})
.AddRoles<Microsoft.AspNetCore.Identity.IdentityRole<long>>()
.AddEntityFrameworkStores<PropNest.Infrastructure.Persistence.PropNestDbContext>();
builder.Services.AddControllers();
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
            RoleClaimType = "role",
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
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Đang kiểm tra và áp dụng Database Migrations còn thiếu...")]
    private static partial void LogApplyingMigrations(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Database đã được đồng bộ thành công!")]
    private static partial void LogMigrationsApplied(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Có lỗi xảy ra khi tự động migrate database.")]
    private static partial void LogMigrationError(ILogger logger, Exception ex);
}
