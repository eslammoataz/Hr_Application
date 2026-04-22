using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Auth;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Auth.Commands.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public ChangePasswordCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<ChangePasswordCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<AuthResponse>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.Auth.ChangePassword);

        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Auth.ChangePassword);
            sw.Stop();
            return Result.Failure<AuthResponse>(DomainErrors.User.NotFound);
        }

        var (succeeded, errors) =
            await _unitOfWork.Users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!succeeded)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Auth.ChangePassword, LogStage.Processing,
                "PasswordChangeFailed", new { UserId = user.Id, ErrorCount = errors.Count() });
            sw.Stop();
            return Result.Failure<AuthResponse>(new Error(DomainErrors.Auth.PasswordChangeFailed.Code,
                string.Join(". ", errors)));
        }

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var roles = await _unitOfWork.Users.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(user, roles);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.Auth.ChangePassword, sw.ElapsedMilliseconds);

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
            Language: user.Language
        ));
    }
}