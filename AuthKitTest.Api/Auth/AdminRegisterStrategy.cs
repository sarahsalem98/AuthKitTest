using AuthKit.Contracts;
using AuthKit.EntityFramework.Entities;
using AuthKit.Exceptions;
using AuthKitTest.Api.Data;
using AuthKitTest.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthKitTest.Api.Auth;

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
            Email        = model.Email,
            FirstName    = model.FirstName,
            LastName     = model.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, 12),
            UserRoles    = new List<UserRoleEntity> { new() { RoleId = role.Id } }
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }
}
