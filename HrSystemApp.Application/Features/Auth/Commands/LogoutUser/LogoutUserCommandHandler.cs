using MediatR;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;

namespace HrSystemApp.Application.Features.Auth.Commands.LogoutUser;

public class LogoutUserCommandHandler : IRequestHandler<LogoutUserCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LogoutUserCommandHandler> _logger;

    public LogoutUserCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<LogoutUserCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(LogoutUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            if (user != null)
            {
                user.FcmToken = null;
                user.DeviceType = null;
                
                await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("User {UserId} logged out and FCM token cleared", request.UserId);
            }
            else
            {
                _logger.LogWarning("User {UserId} not found during logout token clearing", request.UserId);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout for user {UserId}", request.UserId);
            return Result.Failure(DomainErrors.General.ServerError);
        }
    }
}
