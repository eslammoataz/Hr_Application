namespace HrSystemApp.Domain.Models;

public class CompanyLocation : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
}