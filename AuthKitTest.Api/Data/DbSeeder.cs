using AuthKit.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthKitTest.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var roleNames = new[]
        {
            "Admin", "SuperAdmin",
            "CompanyManager", "CompanyUser",
            "VendorManager", "VendorSupplier"
        };
        foreach (var name in roleNames)
        {
            if (!await db.Roles.AnyAsync(r => r.Name == name))
                db.Roles.Add(new RoleEntity { Name = name });
        }

        var permissionNames = new[]
        {
            // Admin portal
            "read:reports",
            "write:reports",
            "read:users",
            "write:users",
            "delete:users",
            "manage:billing",
            "export:data",
            // Company portal
            "read:products",
            "write:products",
            "manage:orders",
            "read:invoices",
            // Vendor portal
            "read:catalog",
            "submit:quotes",
            "manage:contracts"
        };
        foreach (var name in permissionNames)
        {
            if (!await db.Permissions.AnyAsync(p => p.Name == name))
                db.Permissions.Add(new PermissionEntity { Name = name });
        }

        await db.SaveChangesAsync();
    }
}
