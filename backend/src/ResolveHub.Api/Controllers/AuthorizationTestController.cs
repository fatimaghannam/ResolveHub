using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/authorization-test")]
[Authorize]
public sealed class AuthorizationTestController : ControllerBase
{
    [HttpGet("authenticated")]
    public IActionResult GetAuthenticated()
    {
        return Ok(new
        {
            message = "You are authenticated.",
            user = User.Identity?.Name
        });
    }

    [HttpGet("employee")]
    [Authorize(Roles = RoleNames.Employee)]
    public IActionResult GetEmployee()
    {
        return Ok(new
        {
            message = "Employee endpoint accessed successfully."
        });
    }

    [HttpGet("agent")]
    [Authorize(Roles = RoleNames.ITAgent)]
    public IActionResult GetAgent()
    {
        return Ok(new
        {
            message = "IT Agent endpoint accessed successfully."
        });
    }

    [HttpGet("admin")]
    [Authorize(Roles = RoleNames.Admin)]
    public IActionResult GetAdmin()
    {
        return Ok(new
        {
            message = "Admin endpoint accessed successfully."
        });
    }

    [HttpGet("manager")]
    [Authorize(Roles = RoleNames.Manager)]
    public IActionResult GetManager()
    {
        return Ok(new
        {
            message = "Manager endpoint accessed successfully."
        });
    }
}