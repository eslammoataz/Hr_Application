using HrSystemApp.Domain.Common;

namespace HrSystemApp.Domain.Models;

public abstract class AuditableEntity : BaseEntity
{
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }
}