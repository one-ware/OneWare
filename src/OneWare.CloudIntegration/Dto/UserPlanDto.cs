namespace OneWare.CloudIntegration.Dto;

public class UserPlanDto
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public decimal DiscountRate { get; set; }

    public long MaxProjectSizeBytes { get; set; }

    public int MaxProjectCount { get; set; }

    public int MaxModelCount { get; set; }

    public int MaxModelExports { get; set; }

    public int MaxModelTests { get; set; }

    public long MaxProjectSizeMegaBytes
    {
        get => MaxProjectSizeBytes / (1000 * 1000);
        set => MaxProjectSizeBytes = value * 1000 * 1000;
    }

    public int? DefaultDurationDays { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? AssignedAt { get; set; }

    public int IncludedMonthlyCredits { get; set; }
}