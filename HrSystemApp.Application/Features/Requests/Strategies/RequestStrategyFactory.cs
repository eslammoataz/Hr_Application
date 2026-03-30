using HrSystemApp.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace HrSystemApp.Application.Features.Requests.Strategies;

public interface IRequestStrategyFactory
{
    IRequestBusinessStrategy? GetStrategy(RequestType type);
}

public class RequestStrategyFactory : IRequestStrategyFactory
{
    private readonly IEnumerable<IRequestBusinessStrategy> _strategies;

    public RequestStrategyFactory(IEnumerable<IRequestBusinessStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IRequestBusinessStrategy? GetStrategy(RequestType type)
    {
        return _strategies.FirstOrDefault(s => s.Type == type);
    }
}
