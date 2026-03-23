using MediatR;
using Microsoft.Extensions.Logging;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;

namespace HrSystemApp.Application.Features.Auth.Commands.UpdateLanguage;

public class UpdateLanguageCommandHandler : IRequestHandler<UpdateLanguageCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateLanguageCommandHandler> _logger;

    public UpdateLanguageCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateLanguageCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateLanguageCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user is null)
        {
            _logger.LogWarning("Language update attempt for unknown user: {UserId}", request.UserId);
            return Result.Failure(DomainErrors.User.NotFound);
        }

        user.Language = request.Language;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Language updated to {Language} for user {UserId}", request.Language, request.UserId);

        return Result.Success();
    }
}
