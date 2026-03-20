using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Models;
using Microsoft.AspNetCore.Identity;

namespace HrSystemApp.Application.Features.Auth.Commands.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ChangePasswordCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return Result.Failure(DomainErrors.User.NotFound);

        var result = await _userManager.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            var message = string.Join(", ", result.Errors.Select(e => e.Description));
            return Result.Failure(new Error("Auth.PasswordChangeFailed", message));
        }

        // Clear the forced-change flag so the employee is no longer redirected
        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);

        return Result.Success();
    }
}
