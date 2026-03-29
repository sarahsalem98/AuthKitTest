using AuthKit.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthKitTest.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var roleNames = new[] { "Admin", "SuperAdmin" };
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
            "export:data"
        };
        foreach (var name in permissionNames)
        {
            if (!await db.Permissions.AnyAsync(p => p.Name == name))
                db.Permissions.Add(new PermissionEntity { Name = name });
        }

        await db.SaveChangesAsync();
    }
}
