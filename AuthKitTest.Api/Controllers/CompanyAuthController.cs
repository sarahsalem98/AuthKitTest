using AuthKit.Contracts;
using AuthKit.Services;
using AuthKitTest.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLocalization.Services;

namespace AuthKitTest.Api.Controllers;

[ApiController]
[Route("api/company/auth")]
public class CompanyAuthController : ControllerBase
{
    private readonly AuthService<AppUser, CompanyLoginModel, CompanyRegisterModel> _auth ;
    private readonly ILocalizationService _loc;    

    public CompanyAuthController(AuthService<AppUser, CompanyLoginModel, CompanyRegisterModel> auth , ILocalizationService loc)
        => (_auth, _loc) = (auth, loc);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] CompanyLoginModel model, CancellationToken ct)
    {
        try
        {
            var result = await _auth.LoginAsync(model, "company", ct);
            var mssg = _loc.T("hello");
            return Ok(new
            {
                mssg=mssg
            });
        }
        catch (Exception ex)
        {

            return BadRequest(new
            {
                error = ex.Message
            });
        }
      
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] CompanyRegisterModel model, CancellationToken ct)
    {
        await _auth.RegisterAsync(model, "company", ct);
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
