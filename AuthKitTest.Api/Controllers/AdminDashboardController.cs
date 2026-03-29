using AuthKit.Attributes;
using AuthKit.Contracts;
using AuthKit.Services;
using AuthKitTest.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace AuthKitTest.Api.Controllers;

[ApiController]
[Route("api/admin")]
[RequirePortal("admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly ICurrentUser _currentUser;

    public AdminDashboardController(ICurrentUser currentUser)
        => _currentUser = currentUser;

    [HttpGet("profile")]
    public IActionResult Profile() => Ok(new
    {
        userId      = _currentUser.UserId,
        portal      = _currentUser.PortalKey,
        roles       = _currentUser.Roles,
        permissions = _currentUser.Permissions
    });

    [HttpGet("reports")]
    [RequirePermission("read:reports")]
    public IActionResult GetReports() => Ok("reports data");

    [HttpPost("users")]
    [RequirePermission("write:users")]
    public IActionResult CreateUser() => Ok("user created");

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

    [HttpGet("billing")]
    [RequireRole("Admin")]
    [RequirePermission("manage:billing")]
    public IActionResult GetBilling() => Ok("billing data");
}
