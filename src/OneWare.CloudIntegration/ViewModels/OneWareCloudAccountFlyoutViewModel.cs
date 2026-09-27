using System.Globalization;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.CloudIntegration.Dto;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;

namespace OneWare.CloudIntegration.ViewModels;

public class OneWareCloudAccountFlyoutViewModel : ObservableObject
{
    private const string RegisterPath = "/account/register";
    private const string ManageAccountPath = "/account/manage";
    private const string ChangeAddressPath = "/account/manage/changeAddress";

    private readonly OneWareCloudLoginService _loginService;

    public OneWareCloudAccountFlyoutViewModel(
        OneWareCloudAccountSetting setting,
        OneWareCloudCurrentAccountService accountService,
        OneWareCloudLoginService loginService)
    {
        CurrentAccountService = accountService;
        _loginService = loginService;

        const string baseUrl = OneWareCloudIntegrationModule.CredentialStore;
        SettingViewModel = new OneWareCloudAccountSettingViewModel(setting);

        accountService.WhenValueChanged(x => x.CurrentUser).Subscribe(x =>
        {
            if (x == null)
                Url = $"{baseUrl}{RegisterPath}";
            else
                Url = $"{baseUrl}{ManageAccountPath}";
        });

        accountService.WhenValueChanged(x => x.ActiveOrganization).Subscribe(_ => NotifyOrganizationChanged());

        ChangeAddressLink = $"{baseUrl}{ChangeAddressPath}";
    }

    public OneWareCloudCurrentAccountService CurrentAccountService { get; }

    public OneWareCloudAccountSettingViewModel SettingViewModel { get; }

    public string? Url
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string ChangeAddressLink { get; }

    private OrganizationBalanceDto? Organization => CurrentAccountService.ActiveOrganization;

    public bool HasActiveOrganization => Organization != null;

    public string OrganizationName => Organization?.OrganizationName ?? string.Empty;

    public string OrganizationDetails => Organization == null
        ? string.Empty
        : string.IsNullOrWhiteSpace(Organization.PlanName)
            ? Organization.Role
            : $"{Organization.Role} · {Organization.PlanName}";

    /// <summary>Owners and administrators see the organization wallet.</summary>
    public bool ShowOrganizationCredits => Organization?.CanViewBalances == true;

    /// <summary>Members only see their personal monthly budget.</summary>
    public bool ShowPersonalBudget => Organization is { CanViewBalances: false };

    /// <summary>Training Credits available now: remaining monthly allowance plus the prepaid balance.</summary>
    public string TrainingCreditsText => FormatCredits(
        Math.Max(0, Organization?.IncludedMonthlyCreditsRemaining ?? 0) + Math.Max(0, Organization?.CreditBalance ?? 0));

    public string PrepaidCreditsText => FormatCredits(Organization?.CreditBalance ?? 0);

    public string IncludedCreditsText => FormatCredits(Organization?.IncludedMonthlyCreditsRemaining ?? 0);

    /// <summary>AI Credits available now: remaining plan allowance plus the prepaid balance.</summary>
    public string AiCreditsText => FormatCredits(
        Math.Max(0, Organization?.IncludedMonthlyAiCreditsRemaining ?? 0) + Math.Max(0, Organization?.AiCreditBalance ?? 0));

    public string PrepaidAiCreditsText => FormatCredits(Organization?.AiCreditBalance ?? 0);

    public string IncludedAiCreditsText => FormatCredits(Organization?.IncludedMonthlyAiCreditsRemaining ?? 0);

    /// <summary>A trial grants its AI allowance once instead of monthly.</summary>
    public string IncludedAiCreditsLabel => Organization is { PlanKind: "Trial" } ? "Trial allowance" : "Monthly allowance";

    public bool SpendingSuspended => Organization?.SpendingSuspended == true;

    public CreditBudget TrainingBudget => Organization == null
        ? CreditBudget.Empty
        : new CreditBudget("Your Training Credit budget", Organization.MonthlyCreditCap, Organization.MonthlyCreditsUsed,
            Organization.BudgetResetsAt);

    public CreditBudget AiBudget => Organization == null
        ? CreditBudget.Empty
        : new CreditBudget("Your AI Credit budget", Organization.MonthlyAiCreditCap, Organization.MonthlyAiCreditsUsed,
            Organization.BudgetResetsAt);

    /// <summary>Balances are shown as whole Credits, rounded down so they never overstate what is available.</summary>
    public static string FormatCredits(decimal credits) => decimal.Floor(credits).ToString("N0", CreditNumberFormat);

    /// <summary>Same format as the cloud UI: non-breaking space groups thousands, '.' separates decimals ("998 797").</summary>
    public static NumberFormatInfo CreditNumberFormat { get; } = CreateCreditNumberFormat();

    private static NumberFormatInfo CreateCreditNumberFormat()
    {
        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.NumberGroupSeparator = "\u00A0";
        format.NumberDecimalSeparator = ".";
        return NumberFormatInfo.ReadOnly(format);
    }

    public string? PlanStatusText => Organization?.PlanKind switch
    {
        "Free" => "Free plan · evaluation only",
        "Trial" => Organization.PlanExpiresAt is { } trialEnd
            ? $"Pro trial · ends {trialEnd.ToLocalTime():d MMM yyyy}"
            : "Pro trial",
        "Custom" when Organization.PlanExpiresAt is { } customEnd =>
            $"Custom plan · until {customEnd.ToLocalTime():d MMM yyyy}",
        _ => null
    };

    public bool ShowPlanStatus => PlanStatusText != null;

    /// <summary>Owners and administrators of Free or trial organizations can upgrade to Pro.</summary>
    public bool ShowUpgrade => Organization is { CanViewBalances: true, PlanKind: "Free" or "Trial" };

    public string? OrganizationUrl =>
        Organization == null ? null : $"{_loginService.BaseUrl}/organizations/{Organization.OrganizationId}";

    public string? AddCreditsUrl => Organization == null ? null : $"{OrganizationUrl}/credits";

    public string? UpgradeUrl => Organization == null ? null : $"{OrganizationUrl}/credits#organization-plans";

    public Task RefreshAsync() => CurrentAccountService.RefreshActiveOrganizationAsync();

    public async Task OpenFeedbackDialogAsync(Control parent)
    {
        await OneWareCloudIntegrationModule.OpenFeedbackDialogAsync();
    }

    private void NotifyOrganizationChanged()
    {
        foreach (var property in new[]
                 {
                     nameof(HasActiveOrganization), nameof(OrganizationName),
                     nameof(OrganizationDetails), nameof(ShowOrganizationCredits), nameof(ShowPersonalBudget),
                     nameof(TrainingCreditsText), nameof(PrepaidCreditsText), nameof(IncludedCreditsText),
                     nameof(AiCreditsText), nameof(PrepaidAiCreditsText), nameof(IncludedAiCreditsText),
                     nameof(IncludedAiCreditsLabel), nameof(SpendingSuspended), nameof(TrainingBudget), nameof(AiBudget),
                     nameof(OrganizationUrl),
                     nameof(PlanStatusText), nameof(ShowPlanStatus), nameof(ShowUpgrade), nameof(UpgradeUrl),
                     nameof(AddCreditsUrl)
                 })
            OnPropertyChanged(property);
    }
}

/// <summary>A member's personal monthly budget for one currency, as shown in the account flyout.</summary>
public sealed class CreditBudget(string title, decimal? cap, decimal used, DateTime resetsAt)
{
    public static CreditBudget Empty { get; } = new(string.Empty, null, 0, DateTime.MinValue);

    public string Title { get; } = title;

    public bool HasLimit => cap != null;

    public double Limit => (double)Math.Max(1, cap ?? 1);

    public double Used => Math.Min((double)used, Limit);

    public string Text => cap is { } limit
        ? $"{OneWareCloudAccountFlyoutViewModel.FormatCredits(used)} / {OneWareCloudAccountFlyoutViewModel.FormatCredits(limit)}"
        : $"{OneWareCloudAccountFlyoutViewModel.FormatCredits(used)} used";

    public string Hint => cap is { } limit
        ? $"{OneWareCloudAccountFlyoutViewModel.FormatCredits(Math.Max(0, limit - used))} left · resets {resetsAt.ToLocalTime():d MMM}"
        : "No personal limit this month";
}
