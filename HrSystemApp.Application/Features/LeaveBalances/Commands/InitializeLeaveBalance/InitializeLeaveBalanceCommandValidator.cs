using FluentValidation;

namespace HrSystemApp.Application.Features.LeaveBalances.Commands.InitializeLeaveBalance;

public class InitializeLeaveBalanceCommandValidator : AbstractValidator<InitializeLeaveBalanceCommand>
{
    public InitializeLeaveBalanceCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.TotalDays).GreaterThan(0);
    }
}
