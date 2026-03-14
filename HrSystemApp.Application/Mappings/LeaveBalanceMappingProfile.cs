using AutoMapper;
using HrSystemApp.Application.DTOs.LeaveBalances;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Mappings;

public class LeaveBalanceMappingProfile : Profile
{
    public LeaveBalanceMappingProfile()
    {
        CreateMap<LeaveBalance, LeaveBalanceResponse>()
            .ForMember(d => d.LeaveType, o => o.MapFrom(s => s.LeaveType.ToString()));
    }
}
