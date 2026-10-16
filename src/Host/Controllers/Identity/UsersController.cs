using Microsoft.AspNetCore.RateLimiting;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Identity.Users.Password;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace Boilerate.Host.Controllers.Identity;

/// <summary>
/// Users Controller - User management APIs
/// Endpoints: List users, Get user, Create user, Assign roles, Email confirmation, Password reset
/// </summary>
[Authorize(Policy = "CentralAdmin")]
public class UsersController : BaseApiController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("search")]
    [MustHavePermission(AppAction.View, AppFunction.User)]
    public Task<Boilerate.Application.Common.Models.PaginationResponse<UserDetailDto>> SearchAsync(
        UserParameterFilter filter, CancellationToken cancellationToken) => _userService.SearchAsync(filter, cancellationToken);

    /// <summary>
    /// Lấy danh sách tất cả users
    /// Requires: Users.View permission
    /// </summary>
    [HttpGet("list")]
    [MustHavePermission(AppAction.View, AppFunction.User)]
    [OpenApiOperation("Get list of all users.", "")]
    public Task<List<UserDetailDto>> GetListAsync(CancellationToken cancellationToken)
    {
        return _userService.GetListAsync(cancellationToken);
    }

    /// <summary>
    /// Lấy chi tiết user theo ID
    /// Requires: Users.View permission
    /// </summary>
    [HttpGet("{id}")]
    [MustHavePermission(AppAction.View, AppFunction.User)]
    [OpenApiOperation("Get a user's details.", "")]
    public Task<UserDetailDto> GetByIdAsync(
  string id,
        CancellationToken cancellationToken)
    {
        return _userService.GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Lấy danh sách roles của user
    /// Requires: Users.View permission
    /// </summary>
    [HttpGet("{id}/roles")]
    [MustHavePermission(AppAction.View, AppFunction.User)]
    [OpenApiOperation("Get a user's roles.", "")]
    public Task<List<UserRoleDto>> GetRolesAsync(
        string id,
        CancellationToken cancellationToken)
    {
        return _userService.GetRolesAsync(id, cancellationToken);
    }

    /// <summary>
    /// Gán roles cho user
    /// Requires: Users.Update permission
    /// </summary>
    [HttpPut("{id}/roles")]
    [MustHavePermission(AppAction.Update, AppFunction.User)]
    [OpenApiOperation("Update a user's assigned roles.", "")]
    public Task<string> AssignRolesAsync(
   string id,
        UserRolesRequest request,
        CancellationToken cancellationToken)
    {
        return _userService.AssignRolesAsync(id, request, cancellationToken);
    }

    /// <summary>
    /// Tạo user mới (Admin only)
    /// Requires: Users.Create permission
    /// </summary>
    [HttpPost("create")]
    [MustHavePermission(AppAction.Create, AppFunction.User)]
    [OpenApiOperation("Creates a new user.", "")]
    public Task<string> CreateAsync(CreateUserRequest request)
    {
        // Get origin URL cho email confirmation link
        var origin = GetOriginFromRequest();
        return _userService.CreateAsync(request, origin);
    }

    /// <summary>
    /// Self-registration - User tự tạo tài khoản (Public)
    /// Anonymous endpoint - Không cần authentication
    /// </summary>
    [HttpPost("self-register")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [OpenApiOperation("Anonymous user creates a user.", "")]
    public Task<string> SelfRegisterAsync(CreateUserRequest request)
    {
        var origin = GetOriginFromRequest();
        return _userService.CreateAsync(request, origin);
    }

    /// <summary>
    /// Toggle user active status (Enable/Disable user)
    /// Requires: Users.Update permission
    /// </summary>
    [HttpPost("{id}/toggle-status")]
    [MustHavePermission(AppAction.Update, AppFunction.User)]
    [OpenApiOperation("Toggle a user's active status.", "")]
    public async Task<ActionResult> ToggleStatusAsync(
        string id,
        ToggleUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        // Validate ID match
        if (id != request.UserId)
        {
            return BadRequest("ID mismatch");
        }

        await _userService.ToggleStatusAsync(request, cancellationToken);
        return Ok(new { message = "User status updated successfully" });
    }

    /// <summary>
    /// Confirm email address (Public endpoint)
    /// Called from email confirmation link
    /// </summary>
    [HttpGet("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [OpenApiOperation("Confirm email address for a user.", "")]
    public Task<string> ConfirmEmailAsync(
[FromQuery] string userId,
        [FromQuery] string code,
        CancellationToken cancellationToken)
    {
        return _userService.ConfirmEmailAsync(userId, code, cancellationToken);
    }

    /// <summary>
    /// Confirm phone number (Public endpoint)
    /// </summary>
    [HttpGet("confirm-phone-number")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [OpenApiOperation("Confirm phone number for a user.", "")]
    public Task<string> ConfirmPhoneNumberAsync(
        [FromQuery] string userId,
        [FromQuery] string code)
    {
        return _userService.ConfirmPhoneNumberAsync(userId, code);
    }

    /// <summary>
    /// Forgot password - Request password reset email (Public)
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [OpenApiOperation("Request a password reset email for a user.", "")]
    public Task<string> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var origin = GetOriginFromRequest();
        return _userService.ForgotPasswordAsync(request, origin);
    }

    /// <summary>
    /// Reset password (Public endpoint)
    /// Called from password reset link
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [OpenApiOperation("Reset a user's password.", "")]
    public Task<string> ResetPasswordAsync(ResetPasswordRequest request)
    {
        return _userService.ResetPasswordAsync(request);
    }

    /// <summary>
    /// Xuất danh sách users ra Excel
    /// Requires: Users.Export permission
    /// </summary>
    [HttpPost("export")]
    [MustHavePermission(AppAction.Export, AppFunction.User)]
    [OpenApiOperation("Export users to Excel.", "ExportUsers")]
    public async Task<FileResult> ExportAsync(
        [FromBody] ExportUsersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return File(result, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Users.xlsx");
    }

    /// <summary>
    /// Helper method để lấy origin URL
    /// </summary>
    private string GetOriginFromRequest() =>
        $"{Request.Scheme}://{Request.Host.Value}{Request.PathBase.Value}";
}
