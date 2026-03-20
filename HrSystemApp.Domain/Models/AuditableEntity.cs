namespace HrSystemApp.Domain.Models;

public class AuditableEntity : BaseEntity
{
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }
}