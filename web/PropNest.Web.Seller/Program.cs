using PropNest.Web.Seller.Extensions;
using PropNest.Web.Seller.Helpers;
using PropNest.Web.Seller.Services;

var builder = WebApplication.CreateBuilder(args);
JwtUserHelper.Configure(builder.Configuration);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddBackendApiClients(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Properties}/{action=Index}/{id?}");

app.Run();
