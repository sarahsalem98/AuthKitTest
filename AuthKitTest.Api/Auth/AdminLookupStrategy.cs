using AuthKit.Contracts;
using AuthKitTest.Api.Data;
using AuthKitTest.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthKitTest.Api.Auth;

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
