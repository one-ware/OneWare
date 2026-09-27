namespace OneWare.CloudIntegration.Dto;

/// <summary>
///     The signed-in user's view of an organization's credits (mirror of the cloud's <c>OrganizationBalanceDto</c>).
///     Returned by <c>GET /api/organizations/current/balance</c> for the active organization and pushed via
///     <c>OrganizationBalance_Updated</c>.
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

    // Credit amounts are decimals: Training Credits are whole numbers, AI Credits have up to three places (Cloud AI
    // usage is billed in fractions) and Deployment Credits two.

    /// <summary>Prepaid Training Credits (training, tests and exports). Null for members.</summary>
    public decimal? CreditBalance { get; set; }

    /// <summary>Training Credits included by the plan each month. Null for members.</summary>
    public decimal? IncludedMonthlyCredits { get; set; }

    public decimal? IncludedMonthlyCreditsUsed { get; set; }

    /// <summary>The viewer's monthly Training Credit limit, or null when unlimited.</summary>
    public decimal? MonthlyCreditCap { get; set; }

    public decimal MonthlyCreditsUsed { get; set; }

    /// <summary>Prepaid AI Credits (OneWare Cloud AI chat). Null for members.</summary>
    public decimal? AiCreditBalance { get; set; }

    /// <summary>AI Credits included by the plan per period (once for a trial). Null for members.</summary>
    public decimal? IncludedMonthlyAiCredits { get; set; }

    public decimal? IncludedMonthlyAiCreditsUsed { get; set; }

    /// <summary>The viewer's monthly AI Credit limit, or null when unlimited.</summary>
    public decimal? MonthlyAiCreditCap { get; set; }

    public decimal MonthlyAiCreditsUsed { get; set; }

    /// <summary>When the included credits renew (UTC); null for a trial, whose allowance is granted once.</summary>
    public DateTime? IncludedCreditsResetAt { get; set; }

    /// <summary>Spending is suspended (e.g. after a chargeback) until OneWare support clears it.</summary>
    public bool SpendingSuspended { get; set; }

    public DateTime BudgetResetsAt { get; set; }

    public bool CanSpendDeploymentCredits { get; set; }
    
    public decimal? DeploymentCreditBalance { get; set; }

    public decimal IncludedMonthlyCreditsRemaining =>
        Math.Max(0, (IncludedMonthlyCredits ?? 0) - (IncludedMonthlyCreditsUsed ?? 0));

    public decimal? MonthlyCreditsRemaining =>
        MonthlyCreditCap is { } cap ? Math.Max(0, cap - MonthlyCreditsUsed) : null;

    public decimal IncludedMonthlyAiCreditsRemaining =>
        Math.Max(0, (IncludedMonthlyAiCredits ?? 0) - (IncludedMonthlyAiCreditsUsed ?? 0));

    public decimal? MonthlyAiCreditsRemaining =>
        MonthlyAiCreditCap is { } cap ? Math.Max(0, cap - MonthlyAiCreditsUsed) : null;
}
