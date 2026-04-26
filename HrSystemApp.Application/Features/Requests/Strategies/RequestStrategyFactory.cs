using Microsoft.Extensions.DependencyInjection;

namespace HrSystemApp.Application.Features.Requests.Strategies;

public interface IRequestStrategyFactory
{
    IRequestBusinessStrategy? GetStrategy(string? typeKey);
}

public class RequestStrategyFactory : IRequestStrategyFactory
{
    private readonly IEnumerable<IRequestBusinessStrategy> _strategies;

    public RequestStrategyFactory(IEnumerable<IRequestBusinessStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IRequestBusinessStrategy? GetStrategy(string? typeKey)
    {
        if (string.IsNullOrEmpty(typeKey))
            return null;
        return _strategies.FirstOrDefault(s => s.TypeKey.Equals(typeKey, StringComparison.OrdinalIgnoreCase));
    }
}
