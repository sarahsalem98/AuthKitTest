using AuthKit;
using AuthKit.DI;
using AuthKitTest.Api.Auth;
using AuthKitTest.Api.Auth.Portals;
using AuthKitTest.Api.Data;
using AuthKitTest.Api.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Read AuthKit options
var authKitOptions = builder.Configuration.GetSection("AuthKit").Get<AuthKitOptions>()!;

// 2. Add JWT Bearer authentication scheme
builder.Services.AddAuthKitJwtBearer(authKitOptions);

// 3. Register AuthKit core + portals + strategies + EF token store
builder.Services
    .AddAuthKit(builder.Configuration)
    .AddPortal<AdminPortalPolicy>()
    .WithAuthService<AppUser, AdminLoginModel, AdminRegisterModel>()
    .WithLookupStrategy<AdminLookupStrategy>()
    .WithRegisterStrategy<AdminRegisterStrategy>()
    .AddAuthKitEfCore<AppDbContext>();

// 4. Register DbContext
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 5. UseAuthKit BEFORE UseAuthorization
app.UseAuthKit();
app.UseAuthorization();

// 6. Migrate DB and seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.MapControllers();
app.Run();
