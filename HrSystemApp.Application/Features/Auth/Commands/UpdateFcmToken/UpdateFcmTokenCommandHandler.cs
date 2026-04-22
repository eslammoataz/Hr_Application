using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Auth.Commands.UpdateFcmToken;

public class UpdateFcmTokenCommandHandler : IRequestHandler<UpdateFcmTokenCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateFcmTokenCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public UpdateFcmTokenCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateFcmTokenCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result> Handle(UpdateFcmTokenCommand request, CancellationToken cancellationToken)
    {

        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarningUnauthorized(_loggingOptions, LogAction.Auth.UpdateFcmToken);
            return Result.Failure(DomainErrors.User.NotFound);
        }

        user.FcmToken = request.FcmToken;
        user.DeviceType = request.DeviceType;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}