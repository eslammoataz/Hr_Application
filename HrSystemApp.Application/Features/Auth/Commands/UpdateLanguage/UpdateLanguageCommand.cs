using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.Auth.Commands.UpdateLanguage;

public record UpdateLanguageCommand(
    string UserId,
    string Language) : IRequest<Result>;
