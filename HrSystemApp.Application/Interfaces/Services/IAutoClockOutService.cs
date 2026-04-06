namespace HrSystemApp.Application.Interfaces.Services;

public interface IAutoClockOutService
{
    Task ProcessAutoClockOutAsync(CancellationToken cancellationToken = default);
}
