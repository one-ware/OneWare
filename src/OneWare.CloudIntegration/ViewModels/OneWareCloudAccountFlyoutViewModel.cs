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

    /// <summary>Compute Credits available now: remaining monthly allowance plus the prepaid balance.</summary>
    public string ComputeCreditsText => FormatCredits(
        Math.Max(0, Organization?.IncludedMonthlyCreditsRemaining ?? 0) + Math.Max(0, Organization?.CreditBalance ?? 0));

    public string PrepaidCreditsText => FormatCredits(Organization?.CreditBalance ?? 0);

    public string IncludedCreditsText => FormatCredits(Organization?.IncludedMonthlyCreditsRemaining ?? 0);

    public bool HasBudgetLimit => Organization?.MonthlyCreditCap != null;

    public double BudgetLimit => (double)Math.Max(1, Organization?.MonthlyCreditCap ?? 1);

    public double BudgetUsed => Math.Min((double)(Organization?.MonthlyCreditsUsed ?? 0), BudgetLimit);

    public string BudgetText => Organization switch
    {
        null => string.Empty,
        { MonthlyCreditCap: { } cap } => $"{FormatCredits(Organization.MonthlyCreditsUsed)} / {FormatCredits(cap)}",
        _ => $"{FormatCredits(Organization.MonthlyCreditsUsed)} used"
    };

    public string BudgetHint => Organization switch
    {
        null => string.Empty,
        { MonthlyCreditCap: not null } =>
            $"{FormatCredits(Organization.MonthlyCreditsRemaining ?? 0)} left · resets {Organization.BudgetResetsAt.ToLocalTime():d MMM}",
        _ => "No personal limit this month"
    };

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
                     nameof(ComputeCreditsText), nameof(PrepaidCreditsText), nameof(IncludedCreditsText), nameof(HasBudgetLimit),
                     nameof(BudgetLimit), nameof(BudgetUsed), nameof(BudgetText), nameof(BudgetHint),
                     nameof(OrganizationUrl),
                     nameof(PlanStatusText), nameof(ShowPlanStatus), nameof(ShowUpgrade), nameof(UpgradeUrl),
                     nameof(AddCreditsUrl)
                 })
            OnPropertyChanged(property);
    }
}
