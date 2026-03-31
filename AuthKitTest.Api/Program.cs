using AuthKit;
using AuthKit.DI;
using AuthKitTest.Api.Auth;
using AuthKitTest.Api.Auth.Portals;
using AuthKitTest.Api.Data;
using AuthKitTest.Api.Models;
using AuthKitTest.Api.Resources;
using Microsoft.EntityFrameworkCore;
using SharedLocalization.Extensions;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddLocalizationKit(options =>
{
    options.ResourcesAssembly = typeof(ResourceMarker).Assembly;
    options.ResourcesPath = "Resources";       // default
    options.ResourceBaseName = "Resource";  // default
    options.DefaultCulture = "en-US";           // default
    options.SupportedCultures = new() { "en-US", "ar-SA" }; // override auto-detect
});

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
    .AddPortal<CompanyPortalPolicy>()
    .WithAuthService<AppUser, CompanyLoginModel, CompanyRegisterModel>()
    .WithLookupStrategy<CompanyLookupStrategy>()
    .WithRegisterStrategy<CompanyRegisterStrategy>()
    .AddPortal<VendorPortalPolicy>()
    .WithAuthService<AppUser, VendorLoginModel, VendorRegisterModel>()
    .WithLookupStrategy<VendorLookupStrategy>()
    .WithRegisterStrategy<VendorRegisterStrategy>()
    .AddAuthKitEfCore<AppDbContext>();

// 4. Register portal policies as singletons so register strategies can inject them
builder.Services.AddSingleton<AdminPortalPolicy>();
builder.Services.AddSingleton<CompanyPortalPolicy>();
builder.Services.AddSingleton<VendorPortalPolicy>();

// 5. Register DbContext
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<AcceptLanguageHeaderFilter>();

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = "Enter the access token returned from /login or /refresh."
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 5. UseLocalizationKit BEFORE UseAuthKit/UseAuthorization

// 6. UseAuthKit BEFORE UseAuthorization
app.UseAuthKit();
app.UseLocalizationKit();
app.UseAuthorization();

// 7. Migrate DB and seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.MapControllers();
app.Run();
