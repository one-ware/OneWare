namespace OneWare.CloudIntegration.Dto;

public class CurrentUserDto
{
    public string? Email { get; set; }

    public string? AvatarUrl { get; set; }

    public Guid? DefaultOrganizationId { get; set; }

    /// <summary>
    ///     Legacy plan of the active organization. <see cref="UserPlanDto.IncludedMonthlyCredits" /> follows
    ///     <see cref="Services.OneWareCloudCurrentAccountService.ActiveOrganization" />.
    /// </summary>
    [Obsolete(LegacyCloudContract.ObsoleteMessage)]
    public UserPlanDto UserPlan { get; set; } = new() { Id = Guid.Empty, Name = string.Empty };

}