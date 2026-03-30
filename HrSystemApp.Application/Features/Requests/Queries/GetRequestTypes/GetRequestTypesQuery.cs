using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using MediatR;

namespace HrSystemApp.Application.Features.Requests.Queries.GetRequestTypes;

public record GetRequestTypesQuery : IRequest<Result<List<RequestTypeDto>>>;

public record RequestTypeDto(int Id, string Name);

public class GetRequestTypesQueryHandler : IRequestHandler<GetRequestTypesQuery, Result<List<RequestTypeDto>>>
{
    public Task<Result<List<RequestTypeDto>>> Handle(GetRequestTypesQuery request, CancellationToken cancellationToken)
    {
        var types = Enum.GetValues<RequestType>()
            .Select(t => new RequestTypeDto((int)t, t.ToString()))
            .OrderBy(t => t.Name)
            .ToList();

        return Task.FromResult(Result.Success(types));
    }
}
