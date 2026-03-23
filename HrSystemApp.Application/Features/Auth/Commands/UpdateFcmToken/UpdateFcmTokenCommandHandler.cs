using MediatR;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;

namespace HrSystemApp.Application.Features.Auth.Commands.UpdateFcmToken;

public class UpdateFcmTokenCommandHandler : IRequestHandler<UpdateFcmTokenCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateFcmTokenCommandHandler> _logger;

    public UpdateFcmTokenCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateFcmTokenCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateFcmTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user is null)
        {
            _logger.LogWarning("FCM token update attempt for unknown user: {UserId}", request.UserId);
            return Result.Failure(DomainErrors.User.NotFound);
        }

        user.FcmToken = request.FcmToken;
        user.DeviceType = request.DeviceType;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("FCM token and device type updated for user {UserId}", request.UserId);

        return Result.Success();
    }
}
