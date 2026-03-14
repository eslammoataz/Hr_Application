using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;

namespace HrSystemApp.Infrastructure.Repositories;

public class CompanyLocationRepository : Repository<CompanyLocation>, ICompanyLocationRepository
{
    public CompanyLocationRepository(ApplicationDbContext context) : base(context)
    {
    }
}
