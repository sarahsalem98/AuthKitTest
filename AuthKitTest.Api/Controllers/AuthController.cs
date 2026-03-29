using AuthKit.Contracts;
using AuthKit.Services;
using AuthKitTest.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthKitTest.Api.Controllers;

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
