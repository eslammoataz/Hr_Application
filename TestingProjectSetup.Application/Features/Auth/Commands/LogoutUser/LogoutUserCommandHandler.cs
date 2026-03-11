using MediatR;
using Microsoft.Extensions.Logging;
using TestingProjectSetup.Application.Common;
using TestingProjectSetup.Application.Errors;
using TestingProjectSetup.Application.Interfaces;

namespace TestingProjectSetup.Application.Features.Auth.Commands.LogoutUser;

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
            await _unitOfWork.Users.RemoveTokenAsync(request.UserId, request.Token, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {UserId} logged out", request.UserId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout for user {UserId}", request.UserId);
            return Result.Failure(DomainErrors.General.ServerError);
        }
    }
}
