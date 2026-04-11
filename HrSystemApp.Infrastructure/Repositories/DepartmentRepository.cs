using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Infrastructure.Repositories;

public class DepartmentRepository : Repository<Department>, IDepartmentRepository
{
    public DepartmentRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
            /// Retrieves a department by its identifier, including its VicePresident, Manager, Units, and each unit's UnitLeader.
            /// </summary>
            /// <param name="id">The identifier of the department to retrieve.</param>
            /// <param name="cancellationToken">A token to cancel the operation.</param>
            /// <returns>The matching department with related navigation properties loaded, or <c>null</c> if no match is found.</returns>
            public async Task<Department?> GetWithUnitsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Departments
            .Include(d => d.VicePresident)
            .Include(d => d.Manager)
            .Include(d => d.Units)
                .ThenInclude(u => u.UnitLeader)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <summary>
            /// Retrieve all departments that belong to the specified company, including each department's VicePresident and Manager.
            /// </summary>
            /// <param name="companyId">The company identifier used to filter departments.</param>
            /// <param name="cancellationToken">Token to observe while waiting for the task to complete.</param>
            /// <returns>A read-only list of departments that belong to the specified company; empty if no matches.</returns>
            public async Task<IReadOnlyList<Department>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
        => await _context.Departments
            .Include(d => d.VicePresident)
            .Include(d => d.Manager)
            .Where(d => d.CompanyId == companyId)
            .ToListAsync(cancellationToken);
}
