using HrSystemApp.Application.DTOs.LeaveBalances;
using HrSystemApp.Domain.Models;
using Mapster;

namespace HrSystemApp.Application.Mappings;

public class LeaveBalanceMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<LeaveBalance, LeaveBalanceResponse>()
            .Map(dest => dest.LeaveType, src => src.LeaveType.ToString());
    }
}
