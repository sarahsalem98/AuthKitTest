using AuthKit.Contracts;
using AuthKitTest.Api.Data;
using AuthKitTest.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthKitTest.Api.Auth;

public class VendorLookupStrategy : IUserLookupStrategy<AppUser, VendorLoginModel>
{
    private readonly AppDbContext _db;

    public VendorLookupStrategy(AppDbContext db) => _db = db;

    public async Task<AppUser?> FindAsync(VendorLoginModel model, CancellationToken ct)
        => await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.UserPermissions)
                .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Email == model.Email && u.IsActive, ct);
}
