namespace OneWare.CloudIntegration.Dto;

/// <summary>
///     The signed-in user's view of an organization's credits (mirror of the cloud's <c>OrganizationBalanceDto</c>).
///     Returned by <c>GET /api/credits/balance</c> for the active organization and pushed via <c>Balance_Updated</c>.
///     Organization-wide balances are null unless <see cref="CanViewBalances" /> (owners and administrators).
/// </summary>
public class OrganizationBalanceDto
{
    public Guid OrganizationId { get; set; }

    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>Owner, Admin or Member.</summary>
    public string Role { get; set; } = string.Empty;

    public string? PlanName { get; set; }

    /// <summary>Free, Trial, Pro or Custom (serialized as a string).</summary>
    public string? PlanKind { get; set; }

    /// <summary>When the current plan ends (trial end, custom agreement end); null for open-ended plans.</summary>
    public DateTime? PlanExpiresAt { get; set; }

    public bool CanViewBalances { get; set; }

    // Credit amounts are decimals with up to three places (e.g. 24999.875); Cloud AI usage is billed in fractions.
    public decimal? CreditBalance { get; set; }

    public decimal? IncludedMonthlyCredits { get; set; }

    public decimal? IncludedMonthlyCreditsUsed { get; set; }

    public decimal? MonthlyCreditCap { get; set; }

    public decimal MonthlyCreditsUsed { get; set; }

    public DateTime BudgetResetsAt { get; set; }

    public bool CanSpendDeploymentCredits { get; set; }
    
    public decimal? DeploymentCreditBalance { get; set; }

    public decimal IncludedMonthlyCreditsRemaining =>
        Math.Max(0, (IncludedMonthlyCredits ?? 0) - (IncludedMonthlyCreditsUsed ?? 0));

    public decimal? MonthlyCreditsRemaining =>
        MonthlyCreditCap is { } cap ? Math.Max(0, cap - MonthlyCreditsUsed) : null;
}
