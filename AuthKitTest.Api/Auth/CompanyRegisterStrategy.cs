using AuthKit.Contracts;
using AuthKit.EntityFramework.Entities;
using AuthKit.Exceptions;
using AuthKitTest.Api.Auth.Portals;
using AuthKitTest.Api.Data;
using AuthKitTest.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthKitTest.Api.Auth;

public class CompanyRegisterStrategy : IRegisterStrategy<AppUser, CompanyRegisterModel>
{
    private readonly AppDbContext _db;
    private readonly CompanyPortalPolicy _portal;

    public CompanyRegisterStrategy(AppDbContext db, CompanyPortalPolicy portal)
    {
        _db     = db;
        _portal = portal;
    }

    public async Task<AppUser> CreateAsync(CompanyRegisterModel model, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.Email == model.Email, ct))
            throw new AuthException(AuthErrorCode.UserAlreadyExists, "This email is already registered.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "CompanyUser", ct)
            ?? throw new InvalidOperationException("Role 'CompanyUser' not found. Run seed first.");

        var basePermissions = await _db.Permissions
            .Where(p => _portal.AllowedPermissions.Contains(p.Name))
            .ToListAsync(ct);

        var user = new AppUser
        {
            Email           = model.Email,
            FirstName       = model.FirstName,
            LastName        = model.LastName,
            PasswordHash    = BCrypt.Net.BCrypt.HashPassword(model.Password, 12),
            UserRoles       = new List<UserRoleEntity> { new() { RoleId = role.Id } },
            UserPermissions = basePermissions
                .Select(p => new UserPermissionEntity { PermissionId = p.Id })
                .ToList()
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }
}
