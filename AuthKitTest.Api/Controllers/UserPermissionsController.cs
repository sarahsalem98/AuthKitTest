using AuthKit.Attributes;
using AuthKit.EntityFramework.Entities;
using AuthKit.Services;
using AuthKitTest.Api.Data;
using AuthKitTest.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthKitTest.Api.Controllers;

[ApiController]
[Route("api/admin/users/{userId}/permissions")]
[RequirePortal("admin")]
[RequireRole("SuperAdmin")]
public class UserPermissionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService<AppUser, AdminLoginModel, AdminRegisterModel> _auth;

    public UserPermissionsController(
        AppDbContext db,
        AuthService<AppUser, AdminLoginModel, AdminRegisterModel> auth)
    {
        _db   = db;
        _auth = auth;
    }

    [HttpGet]
    public async Task<IActionResult> GetUserPermissions(string userId, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserPermissions)
                .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) return NotFound("User not found.");

        var permissions = user.UserPermissions
            .Select(up => up.Permission.Name)
            .ToList();

        return Ok(new { userId, permissions });
    }

    [HttpPost]
    public async Task<IActionResult> AssignPermission(
        string userId,
        [FromBody] AssignPermissionRequest request,
        CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserPermissions)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) return NotFound("User not found.");

        var permission = await _db.Permissions
            .FirstOrDefaultAsync(p => p.Name == request.Permission, ct);

        if (permission is null) return BadRequest($"Permission '{request.Permission}' does not exist.");

        if (user.UserPermissions.Any(up => up.PermissionId == permission.Id))
            return Conflict($"User already has permission '{request.Permission}'.");

        user.UserPermissions.Add(new UserPermissionEntity { PermissionId = permission.Id });
        await _db.SaveChangesAsync(ct);

        // Revoke active tokens so the user re-logs in and gets a fresh JWT with updated permissions
        await _auth.RevokeAsync(userId, ct);

        return Ok(new { userId, assigned = request.Permission });
    }

    [HttpDelete("{permission}")]
    public async Task<IActionResult> RevokePermission(
        string userId,
        string permission,
        CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserPermissions)
                .ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) return NotFound("User not found.");

        var entry = user.UserPermissions
            .FirstOrDefault(up => up.Permission.Name == permission);

        if (entry is null) return NotFound($"User does not have permission '{permission}'.");

        user.UserPermissions.Remove(entry);
        await _db.SaveChangesAsync(ct);

        // Revoke active tokens so the user re-logs in and gets a fresh JWT with updated permissions
        await _auth.RevokeAsync(userId, ct);

        return NoContent();
    }
}

public record AssignPermissionRequest(string Permission);
