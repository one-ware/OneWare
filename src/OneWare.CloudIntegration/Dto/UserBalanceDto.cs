namespace OneWare.CloudIntegration.Dto;

/// <summary>
///     Legacy user balance in whole Credits, kept so plugins built against older OneWare versions still load.
///     Derived from <see cref="OrganizationBalanceDto" />; use that instead.
/// </summary>
[Obsolete(LegacyCloudContract.ObsoleteMessage)]
public class UserBalanceDto
{
    public required int Balance { get; init; }

    public required int IncludedMonthlyCreditsUsed { get; init; }
}
