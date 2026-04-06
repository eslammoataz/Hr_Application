using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Application.Features.Attendance.Commands.OverrideClockOut;
using MediatR;

namespace HrSystemApp.Application.Features.Attendance.Commands.BatchOverrideClockOut;

public sealed record BatchOverrideItem(
    Guid EmployeeId,
    DateOnly Date,
    DateTime ClockOutUtc,
    string Reason);

public sealed record BatchOverrideClockOutResult(
    Guid EmployeeId,
    DateOnly Date,
    bool IsSuccess,
    string? Error,
    AttendanceResponse? Attendance);

public sealed record BatchOverrideClockOutCommand(
    IReadOnlyList<BatchOverrideItem> Items) : IRequest<Result<IReadOnlyList<BatchOverrideClockOutResult>>>;

public class BatchOverrideClockOutCommandHandler
    : IRequestHandler<BatchOverrideClockOutCommand, Result<IReadOnlyList<BatchOverrideClockOutResult>>>
{
    private readonly IMediator _mediator;

    public BatchOverrideClockOutCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<Result<IReadOnlyList<BatchOverrideClockOutResult>>> Handle(
        BatchOverrideClockOutCommand request,
        CancellationToken cancellationToken)
    {
        var results = new List<BatchOverrideClockOutResult>(request.Items.Count);

        foreach (var item in request.Items)
        {
            var result = await _mediator.Send(
                new OverrideClockOutCommand(item.EmployeeId, item.Date, item.ClockOutUtc, item.Reason),
                cancellationToken);

            if (result.IsSuccess)
            {
                results.Add(new BatchOverrideClockOutResult(
                    item.EmployeeId,
                    item.Date,
                    true,
                    null,
                    result.Value));
                continue;
            }

            results.Add(new BatchOverrideClockOutResult(
                item.EmployeeId,
                item.Date,
                false,
                result.Error.Message,
                null));
        }

        return Result.Success<IReadOnlyList<BatchOverrideClockOutResult>>(results);
    }
}
