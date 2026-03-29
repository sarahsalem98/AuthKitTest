# AuthKit

A fully generic, multi-portal authentication and authorization library for ASP.NET Core.
Targets **net8.0** and **net9.0**.

No hardcoded user tables. No hardcoded login models. Bring your own entities, strategies, and portal policies — AuthKit handles JWT issuance, refresh token rotation, role/permission enforcement, and RFC 7807 error responses.

---

## Table of Contents

1. [What AuthKit Does](#what-authkit-does)
2. [Packages](#packages)
3. [Database Schema](#database-schema)
4. [Integration Guide — Step by Step](#integration-guide)
   - [Step 1 — Install packages](#step-1--install-packages)
   - [Step 2 — Create your DbContext](#step-2--create-your-dbcontext)
   - [Step 3 — Create your User entity](#step-3--create-your-user-entity)
   - [Step 4 — Create your Login and Register models](#step-4--create-your-login-and-register-models)
   - [Step 5 — Implement IUserLookupStrategy](#step-5--implement-iuserlookupstrategy)
   - [Step 6 — Implement IRegisterStrategy](#step-6--implement-iregisterstrategy)
   - [Step 7 — Define Portal Policies](#step-7--define-portal-policies)
   - [Step 8 — Configure appsettings.json](#step-8--configure-appsettingsjson)
   - [Step 9 — Wire up Program.cs](#step-9--wire-up-programcs)
   - [Step 10 — Run Migrations](#step-10--run-migrations)
   - [Step 11 — Seed Roles and Permissions](#step-11--seed-roles-and-permissions)
   - [Step 12 — Create Auth Controller](#step-12--create-auth-controller)
   - [Step 13 — Protect Controllers](#step-13--protect-controllers)
5. [Multiple Portals](#multiple-portals)
6. [ICurrentUser — Reading the Logged-in User](#icurrentuser)
7. [Error Responses](#error-responses)
8. [Claim Types Reference](#claim-types-reference)
9. [Full Project Structure Example](#full-project-structure-example)

---

## What AuthKit Does

| Feature | Description |
|---|---|
| JWT access tokens | HS256 signed, configurable expiry |
| Refresh token rotation | Secure rotation with revocation |
| Multi-portal login | Each portal has its own allowed roles and permissions |
| Role-based access | `[RequireRole("Admin")]` on any controller or action |
| Permission-based access | `[RequirePermission("read:reports")]` on any controller or action |
| Portal locking | `[RequirePortal("admin")]` locks an entire controller to one portal |
| RFC 7807 errors | All auth errors return structured ProblemDetails JSON |
| Normalized DB | Roles and permissions in their own tables with join tables |

---

## Packages

| Package | Purpose |
|---|---|
| `AuthKit` | Core — contracts, services, middleware, attributes, DI |
| `AuthKit.EntityFramework` | Optional — EF Core base classes, normalized tables, token store |

---

## Database Schema

After running migrations, your database will contain these AuthKit-managed tables alongside your own:

```
Roles
├── Id      int (PK, auto-increment)
└── Name    nvarchar(128) UNIQUE

Permissions
├── Id      int (PK, auto-increment)
└── Name    nvarchar(256) UNIQUE

UserRoles
├── UserId  nvarchar(256)  FK → your Users table
└── RoleId  int            FK → Roles.Id

UserPermissions
├── UserId       nvarchar(256)  FK → your Users table
└── PermissionId int            FK → Permissions.Id

RefreshTokens
├── Id         int (PK, auto-increment)
├── Token      nvarchar(512) UNIQUE
├── UserId     nvarchar(256)
├── PortalKey  nvarchar(128)
├── ExpiresAt  datetimeoffset
└── IsRevoked  bit
```

Your `Users` table is defined entirely by you. AuthKit links to it only via string `UserId` — no hard foreign key constraint from AuthKit tables into your Users table, so you stay in full control of your schema.

---

## Integration Guide

### Step 1 — Install packages

```bash
dotnet add package AuthKit
dotnet add package AuthKit.EntityFramework
```

Or reference the projects directly if building from source:

```xml
<ProjectReference Include="..\AuthKit\AuthKit.csproj" />
<ProjectReference Include="..\AuthKit.EntityFramework\AuthKit.EntityFramework.csproj" />
```

---

### Step 2 — Create your DbContext

Your `DbContext` must inherit `AuthDbContextBase`. This gives you the `Roles`, `Permissions`, `UserRoles`, `UserPermissions`, and `RefreshTokens` DbSets automatically. Add your own `DbSet` properties on top.

```csharp
// Data/AppDbContext.cs
using AuthKit.EntityFramework;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : AuthDbContextBase
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Your own tables
    public DbSet<AppUser> Users => Set<AppUser>();
}
```

---

### Step 3 — Create your User entity

Inherit `AuthUserBase`. It provides `Id`, `PasswordHash`, `UserRoles`, and `UserPermissions`. Add any extra columns your application needs.

```csharp
// Models/AppUser.cs
using AuthKit.EntityFramework;

public class AppUser : AuthUserBase
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

Then configure the Users table in your `AppDbContext.OnModelCreating`. Always call `base.OnModelCreating` first:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder); // sets up all AuthKit tables — do not skip

    modelBuilder.Entity<AppUser>(e =>
    {
        e.HasKey(u => u.Id);
        e.HasIndex(u => u.Email).IsUnique();
        e.Property(u => u.Email).IsRequired().HasMaxLength(256);
        e.Property(u => u.FirstName).HasMaxLength(128);
        e.Property(u => u.LastName).HasMaxLength(128);

        // Tell EF how UserRoles links back to AppUser
        e.HasMany(u => u.UserRoles)
         .WithOne()
         .HasForeignKey(ur => ur.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        // Tell EF how UserPermissions links back to AppUser
        e.HasMany(u => u.UserPermissions)
         .WithOne()
         .HasForeignKey(up => up.UserId)
         .OnDelete(DeleteBehavior.Cascade);
    });
}
```

---

### Step 4 — Create your Login and Register models

Plain records or classes — no base class required. The only rule: your **login model must have a `Password` property** so AuthKit can verify it with BCrypt.

```csharp
// Models/AdminLoginModel.cs
public record AdminLoginModel(string Email, string Password);

// Models/AdminRegisterModel.cs
public record AdminRegisterModel(
    string Email,
    string Password,
    string FirstName,
    string LastName
);
```

For multiple portals you can have separate models per portal, or reuse the same ones:

```csharp
public record CustomerLoginModel(string Email, string Password);
public record CustomerRegisterModel(string Email, string Password, string PhoneNumber);
```

---

### Step 5 — Implement IUserLookupStrategy

This tells AuthKit how to find a user by your login model.

> **Always `.Include()` roles and permissions.** Without this the portal role check will fail silently because the navigation properties will be empty collections.

```csharp
// Auth/AdminLookupStrategy.cs
using AuthKit.Contracts;
using Microsoft.EntityFrameworkCore;

public class AdminLookupStrategy : IUserLookupStrategy<AppUser, AdminLoginModel>
{
    private readonly AppDbContext _db;

    public AdminLookupStrategy(AppDbContext db) => _db = db;

    public async Task<AppUser?> FindAsync(AdminLoginModel model, CancellationToken ct)
        => await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserPermissions)
                .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Email == model.Email && u.IsActive, ct);
}
```

---

### Step 6 — Implement IRegisterStrategy

This tells AuthKit how to create a new user. You are responsible for:
- Checking for duplicates and throwing `AuthException(UserAlreadyExists, ...)`
- Hashing the password with BCrypt
- Assigning initial roles and permissions from the normalized tables

```csharp
// Auth/AdminRegisterStrategy.cs
using AuthKit.Contracts;
using AuthKit.Exceptions;
using AuthKit.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

public class AdminRegisterStrategy : IRegisterStrategy<AppUser, AdminRegisterModel>
{
    private readonly AppDbContext _db;

    public AdminRegisterStrategy(AppDbContext db) => _db = db;

    public async Task<AppUser> CreateAsync(AdminRegisterModel model, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.Email == model.Email, ct))
            throw new AuthException(AuthErrorCode.UserAlreadyExists, "This email is already registered.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", ct)
            ?? throw new InvalidOperationException("Role 'Admin' not found. Run seed first.");

        var user = new AppUser
        {
            Email      = model.Email,
            FirstName  = model.FirstName,
            LastName   = model.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, 12),
            UserRoles  = new List<UserRoleEntity> { new() { RoleId = role.Id } }
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }
}
```

---

### Step 7 — Define Portal Policies

A portal policy controls which roles (and optionally which permissions) are allowed to log into a specific portal. One class per portal.

```csharp
// Auth/Portals/AdminPortalPolicy.cs
using AuthKit.Contracts;

public class AdminPortalPolicy : IPortalPolicy
{
    public string PortalKey => "admin";
    public IReadOnlyList<string> AllowedRoles => new[] { "Admin", "SuperAdmin" };
    public IReadOnlyList<string> AllowedPermissions => Array.Empty<string>();
}

// Auth/Portals/CustomerPortalPolicy.cs
public class CustomerPortalPolicy : IPortalPolicy
{
    public string PortalKey => "customer";
    public IReadOnlyList<string> AllowedRoles => new[] { "Customer" };
    public IReadOnlyList<string> AllowedPermissions => Array.Empty<string>();
}

// Auth/Portals/VendorPortalPolicy.cs
public class VendorPortalPolicy : IPortalPolicy
{
    public string PortalKey => "vendor";
    public IReadOnlyList<string> AllowedRoles => new[] { "Vendor" };
    // User must also have this permission to access the vendor portal
    public IReadOnlyList<string> AllowedPermissions => new[] { "portal:vendor" };
}
```

---

### Step 8 — Configure appsettings.json

```json
{
  "AuthKit": {
    "SecretKey": "your-secret-key-must-be-at-least-32-characters-long",
    "Issuer": "https://yourdomain.com",
    "Audience": "https://yourdomain.com",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=MyAppDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

> **Important:** `SecretKey` must be at least 32 characters for HS256. Use environment variables or a secrets manager in production — never commit it to source control.

---

### Step 9 — Wire up Program.cs

```csharp
using AuthKit;
using AuthKit.DI;
using AuthKit.EntityFramework.DI;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Read AuthKit options early — needed to configure JWT Bearer scheme
var authKitOptions = builder.Configuration.GetSection("AuthKit").Get<AuthKitOptions>()!;

// 2. Add JWT Bearer authentication scheme
builder.Services.AddAuthKitJwtBearer(authKitOptions);

// 3. Register AuthKit core + portals + strategies + EF token store
builder.Services
    .AddAuthKit(builder.Configuration)
    .AddPortal<AdminPortalPolicy>()
    .AddPortal<CustomerPortalPolicy>()
    .AddPortal<VendorPortalPolicy>()
    .WithAuthService<AppUser, AdminLoginModel, AdminRegisterModel>()
    .WithLookupStrategy<AdminLookupStrategy>()
    .WithRegisterStrategy<AdminRegisterStrategy>()
    .AddAuthKitEfCore<AppDbContext>();

// 4. Register your DbContext
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();

var app = builder.Build();

// 5. UseAuthKit BEFORE UseAuthorization
//    Registers AuthExceptionMiddleware + UseAuthentication in the correct order
app.UseAuthKit();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

---

### Step 10 — Run Migrations

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

This creates all six tables: `Users`, `Roles`, `Permissions`, `UserRoles`, `UserPermissions`, `RefreshTokens`.

---

### Step 11 — Seed Roles and Permissions

AuthKit does not auto-seed roles or permissions — you define what strings exist.

```csharp
// Data/DbSeeder.cs
using AuthKit.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var roleNames = new[] { "Admin", "SuperAdmin", "Customer", "Vendor" };
        foreach (var name in roleNames)
        {
            if (!await db.Roles.AnyAsync(r => r.Name == name))
                db.Roles.Add(new RoleEntity { Name = name });
        }

        var permissionNames = new[]
        {
            "read:reports",
            "write:reports",
            "read:users",
            "write:users",
            "delete:users",
            "manage:billing",
            "export:data",
            "portal:vendor"
        };
        foreach (var name in permissionNames)
        {
            if (!await db.Permissions.AnyAsync(p => p.Name == name))
                db.Permissions.Add(new PermissionEntity { Name = name });
        }

        await db.SaveChangesAsync();
    }
}
```

Call it in `Program.cs` before `app.Run()`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}
```

---

### Step 12 — Create Auth Controller

```csharp
// Controllers/AuthController.cs
using AuthKit.Contracts;
using AuthKit.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService<AppUser, AdminLoginModel, AdminRegisterModel> _auth;

    public AuthController(AuthService<AppUser, AdminLoginModel, AdminRegisterModel> auth)
        => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AdminLoginModel model, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(model, "admin", ct);
        return Ok(result);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] AdminRegisterModel model, CancellationToken ct)
    {
        await _auth.RegisterAsync(model, "admin", ct);
        return StatusCode(201);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _auth.RefreshAsync(request.RefreshToken, ct);
        return Ok(result);
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke(
        [FromServices] ICurrentUser currentUser,
        CancellationToken ct)
    {
        await _auth.RevokeAsync(currentUser.UserId!, ct);
        return NoContent();
    }
}

public record RefreshRequest(string RefreshToken);
```

---

### Step 13 — Protect Controllers

Use the three AuthKit attributes on controllers or individual actions:

```csharp
// Controllers/AdminDashboardController.cs
using AuthKit.Attributes;
using AuthKit.Contracts;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/admin")]
[RequirePortal("admin")]          // ALL actions: portal_key claim must equal "admin"
public class AdminDashboardController : ControllerBase
{
    private readonly ICurrentUser _currentUser;

    public AdminDashboardController(ICurrentUser currentUser)
        => _currentUser = currentUser;

    // Any authenticated admin portal user
    [HttpGet("profile")]
    public IActionResult Profile() => Ok(new
    {
        userId      = _currentUser.UserId,
        portal      = _currentUser.PortalKey,
        roles       = _currentUser.Roles,
        permissions = _currentUser.Permissions
    });

    // Requires "read:reports" permission claim in JWT
    [HttpGet("reports")]
    [RequirePermission("read:reports")]
    public IActionResult GetReports() => Ok("reports data");

    // Requires "write:users" permission claim in JWT
    [HttpPost("users")]
    [RequirePermission("write:users")]
    public IActionResult CreateUser() => Ok("user created");

    // Requires "SuperAdmin" role claim in JWT
    [HttpDelete("users/{id}")]
    [RequireRole("SuperAdmin")]
    public async Task<IActionResult> DeleteUser(
        string id,
        [FromServices] AuthService<AppUser, AdminLoginModel, AdminRegisterModel> auth,
        CancellationToken ct)
    {
        await auth.RevokeAsync(id, ct);
        return NoContent();
    }

    // Stack multiple — user needs BOTH the role AND the permission
    [HttpGet("billing")]
    [RequireRole("Admin")]
    [RequirePermission("manage:billing")]
    public IActionResult GetBilling() => Ok("billing data");
}
```

---

## Multiple Portals

Each portal is fully independent. Separate controllers, separate policies, separate login endpoints.

```csharp
[ApiController]
[Route("api/admin")]
[RequirePortal("admin")]
public class AdminController : ControllerBase { }

[ApiController]
[Route("api/customer")]
[RequirePortal("customer")]
public class CustomerController : ControllerBase { }

[ApiController]
[Route("api/vendor")]
[RequirePortal("vendor")]
public class VendorController : ControllerBase { }
```

A user with role `"Admin"` attempting to log in through the customer portal (`portalKey = "customer"`) will receive a `403 PortalAccessDenied` — the `CustomerPortalPolicy` only allows the `"Customer"` role.

---

## ICurrentUser

Inject `ICurrentUser` into any controller or service to read the current user's identity without touching `HttpContext` directly:

```csharp
public class ReportService
{
    private readonly ICurrentUser _currentUser;

    public ReportService(ICurrentUser currentUser) => _currentUser = currentUser;

    public void GenerateReport()
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException();

        var userId  = _currentUser.UserId;           // "abc-123"
        var email   = _currentUser.Email;            // "user@example.com"
        var portal  = _currentUser.PortalKey;        // "admin"
        var roles   = _currentUser.Roles;            // ["Admin"]
        var perms   = _currentUser.Permissions;      // ["read:reports"]

        bool isAdmin  = _currentUser.HasRole("Admin");
        bool canRead  = _currentUser.HasPermission("read:reports");
        bool inPortal = _currentUser.IsInPortal("admin");
    }
}
```

---

## Error Responses

All `AuthException` errors are caught by `AuthExceptionMiddleware` and returned as RFC 7807 JSON:

```json
{
  "status": 401,
  "title": "Invalid Credentials",
  "detail": "The password provided is incorrect.",
  "instance": "/api/auth/login",
  "errorCode": "InvalidCredentials"
}
```

| Scenario | HTTP Status | errorCode |
|---|---|---|
| Wrong password | 401 | `InvalidCredentials` |
| User not found | 401 | `UserNotFound` |
| Expired refresh token | 401 | `TokenExpired` |
| Revoked or invalid token | 401 | `TokenInvalid` |
| Role not allowed for portal | 403 | `PortalAccessDenied` |
| Missing required permission on portal | 403 | `PortalAccessDenied` |
| `[RequireRole]` check failed | 403 | `Forbidden` |
| `[RequirePermission]` check failed | 403 | `Forbidden` |
| `[RequirePortal]` check failed | 403 | `Forbidden` |
| Email already registered | 409 | `UserAlreadyExists` |

---

## Claim Types Reference

All JWT claim names are constants in `AuthKitClaimTypes`:

| Constant | JWT Claim | Example Value |
|---|---|---|
| `AuthKitClaimTypes.Sub` | `sub` | `"abc-123"` |
| `AuthKitClaimTypes.Email` | `email` | `"user@example.com"` |
| `AuthKitClaimTypes.PortalKey` | `portal_key` | `"admin"` |
| `AuthKitClaimTypes.Role` | `role` | `"Admin"` (one claim per role) |
| `AuthKitClaimTypes.Permission` | `permission` | `"read:reports"` (one claim per permission) |

---

## Full Project Structure Example

```
MyApp/
├── MyApp.csproj
├── Program.cs
├── appsettings.json
│
├── Data/
│   ├── AppDbContext.cs               ← extends AuthDbContextBase
│   └── DbSeeder.cs                   ← seeds Roles and Permissions tables
│
├── Models/
│   ├── AppUser.cs                    ← extends AuthUserBase
│   ├── AdminLoginModel.cs
│   ├── AdminRegisterModel.cs
│   ├── CustomerLoginModel.cs
│   └── CustomerRegisterModel.cs
│
├── Auth/
│   ├── Portals/
│   │   ├── AdminPortalPolicy.cs      ← implements IPortalPolicy
│   │   ├── CustomerPortalPolicy.cs
│   │   └── VendorPortalPolicy.cs
│   ├── AdminLookupStrategy.cs        ← implements IUserLookupStrategy
│   ├── AdminRegisterStrategy.cs      ← implements IRegisterStrategy
│   ├── CustomerLookupStrategy.cs
│   └── CustomerRegisterStrategy.cs
│
└── Controllers/
    ├── AuthController.cs             ← login, register, refresh, revoke
    ├── AdminController.cs            ← [RequirePortal("admin")]
    ├── CustomerController.cs         ← [RequirePortal("customer")]
    └── VendorController.cs           ← [RequirePortal("vendor")]
```
