using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Auth.Commands.ForceChangePassword;

public class ForceChangePasswordCommandHandler : IRequestHandler<ForceChangePasswordCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ForceChangePasswordCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public ForceChangePasswordCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<ForceChangePasswordCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<AuthResponse>> Handle(ForceChangePasswordCommand request, CancellationToken cancellationToken)
    {

        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Auth.ForceChangePassword);
            return Result.Failure<AuthResponse>(DomainErrors.User.NotFound);
        }

        if (!user.MustChangePassword)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.ForceChangePassword, LogStage.Authorization,
                "ForcedChangeNotRequired", new { UserId = user.Id });
            return Result.Failure<AuthResponse>(DomainErrors.Auth.ForcedChangeNotRequired);
        }

        var (succeeded, errors) =
            await _unitOfWork.Users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!succeeded)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.ForceChangePassword, LogStage.Processing,
                "PasswordChangeFailed", new { UserId = user.Id, ErrorCount = errors.Count() });
            return Result.Failure<AuthResponse>(new Error(DomainErrors.Auth.PasswordChangeFailed.Code,
                string.Join(". ", errors)));
        }

        user.MustChangePassword = false;
        user.LastLoginAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var roles = await _unitOfWork.Users.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);

        IReadOnlyList<string> permissions = Array.Empty<string>();
        if (user.EmployeeId.HasValue)
        {
            permissions = await _unitOfWork.EmployeeCompanyRoles.GetPermissionsForEmployeeAsync(user.EmployeeId.Value, cancellationToken);
        }

        return Result.Success(new AuthResponse(
            Token: token,
            RefreshToken: null,
            UserId: user.Id,
            Email: user.Email!,
            Name: user.Name,
            Role: roles.FirstOrDefault() ?? string.Empty,
            EmployeeId: user.EmployeeId,
            MustChangePassword: false,
            ExpiresAt: expiresAt,
            PhoneNumber: user.PhoneNumber,
            Language: user.Language,
            Permissions: permissions
        ));
    }
}