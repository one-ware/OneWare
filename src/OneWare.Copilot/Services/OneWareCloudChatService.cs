using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using GitHub.Copilot;
using Microsoft.Extensions.Logging;
using OneWare.CloudIntegration.ViewModels;
using OneWare.CloudIntegration.Views;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

/// <summary>
/// OneWare Cloud AI. Runs the Copilot runtime against the OneWare Cloud endpoint with the signed-in
/// OneWare account, and handles everything specific to it: account switches, per-user history,
/// idempotency keys, plan/organization restrictions and Compute Credit billing errors.
/// </summary>
public sealed class OneWareCloudChatService : CopilotChatServiceBase, IChatServiceWithHistoryReset
{
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string CloudOwnerMarkerFileName = ".oneware-cloud-owner";

    /// <summary>Error code of the OneWare Cloud 403 for plans that do not include Cloud AI.</summary>
    private const string CloudAiNotInPlanErrorCode = "cloud_ai_not_in_plan";

    private const string CloudAiDisabledByOrganizationErrorCode = "cloud_ai_disabled_by_organization";

    private static readonly Regex InsufficientCreditsStatusRegex = new(@"\b402\b", RegexOptions.Compiled);

    private readonly IOneWareCloudAccess _cloudAccess;
    private readonly IWindowService _windowService;
    private readonly SemaphoreSlim _cloudAccountSync = new(1, 1);
    private IDisposable? _cloudAccountSubscription;
    private bool _wasInitialized;
    private string? _initializedCloudUserId;
    private volatile string? _currentTurnIdempotencyKey;

    public OneWareCloudChatService(
        ISettingsService settingsService,
        IAiFunctionProvider toolProvider,
        IPackageService packageService,
        IPackageWindowService packageWindowService,
        IWindowService windowService,
        IMainDockService mainDockService,
        IPaths paths,
        IChatAgentService agentService,
        IOneWareCloudAccess cloudAccess)
        : base(
            settingsService,
            toolProvider,
            packageService,
            packageWindowService,
            windowService,
            mainDockService,
            paths,
            agentService)
    {
        _cloudAccess = cloudAccess;
        _windowService = windowService;
        UsesGitHubAuthentication = false;
    }

    public override string Name => "OneWare Cloud";

    public override bool IsOneWareCloud => true;

    private string CloudBaseUrl => _cloudAccess.BaseUrl.TrimEnd('/');

    protected override string GetSelectedModelSettingKey() =>
        CopilotModule.CopilotOneWareCloudSelectedModelSettingKey;

    #region Initialization and account

    protected override async Task<bool> PrepareInitializationAsync()
    {
        _wasInitialized = true;
        EnsureCloudAccountSubscription();
        _initializedCloudUserId = _cloudAccess.UserId;

        if (!await EnsureAuthenticatedAsync())
            return false;

        // Also covers a user switch that happened while the app was closed.
        if (_cloudAccess.UserId is { } userId) EnsureCloudHistoryOwner(userId);

        return true;
    }

    protected override ByokConfiguration GetByokConfiguration()
    {
        if (!Uri.TryCreate(_cloudAccess.BaseUrl, UriKind.Absolute, out var cloudUri) ||
            cloudUri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("The OneWare Cloud URL is invalid.");

        return new ByokConfiguration(
            CopilotModule.ProviderOneWareCloud,
            "openai",
            $"{CloudBaseUrl}/api/copilot/v1",
            string.Empty,
            string.Empty,
            CopilotModule.WireApiResponses);
    }

    protected override void ApplyByokStatus(ByokConfiguration configuration)
    {
        UsesGitHubAuthentication = false;
        AccountLogin = null;
        CanSignOut = false;
        IsAuthenticated = true;
        AccountAuthType = "oneware-cloud";
        AccountStatusText = "Signed in to OneWare Cloud";
    }

    protected override Task SignInCoreAsync(Control? owner) => AuthenticateAsync(owner);

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        try
        {
            await _cloudAccess.GetAccessTokenAsync();
            return true;
        }
        catch (InvalidOperationException)
        {
            ApplyAuthStatus(null);
            UsesGitHubAuthentication = false;
            RaiseStatus(new StatusEvent(false, "Not Authenticated"));
            Blocker = new ChatServiceBlocker("Sign in to OneWare Cloud",
                "OneWare Cloud AI uses your OneWare account. Sign in to start chatting.")
            {
                ActionText = "Login to OneWare Cloud",
                ActionCommand = new AsyncRelayCommand<Control?>(AuthenticateAsync)
            };
            return false;
        }
    }

    private async Task AuthenticateAsync(Control? owner)
    {
        var ownerWindow = owner != null ? TopLevel.GetTopLevel(owner) as Window : null;
        var userIdBeforeLogin = _cloudAccess.UserId;

        await _windowService.ShowDialogAsync(new AuthenticateCloudView
        {
            DataContext = ContainerLocator.Container.Resolve<AuthenticateCloudViewModel>()
        }, ownerWindow);

        // A login that changed the user id is picked up by the account subscription.
        if (_cloudAccountSubscription != null && _cloudAccess.UserId != userIdBeforeLogin) return;

        try
        {
            await _cloudAccess.GetAccessTokenAsync();
        }
        catch (InvalidOperationException ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex,
                "OneWare Cloud authentication did not complete.");
            return;
        }

        await HandleCloudAccountChangedAsync(true);
    }

    private void EnsureCloudAccountSubscription()
    {
        if (_cloudAccountSubscription != null) return;

        // Skip the current value: only react to logins and logouts that happen from now on.
        _cloudAccountSubscription = _cloudAccess.UserIdObservable
            .Skip(1)
            // Run off the caller: logouts are raised from inside the token refresh of the login service.
            .Subscribe(_ => Task.Run(() => HandleCloudAccountChangedAsync()));
    }

    /// <summary>
    /// Restarts the OneWare Cloud runtime after a login or logout anywhere in the app. A logout keeps
    /// the conversation so the same user can resume it; a different user starts with an empty history.
    /// </summary>
    private async Task HandleCloudAccountChangedAsync(bool force = false)
    {
        if (!_wasInitialized) return;

        await _cloudAccountSync.WaitAsync().ConfigureAwait(false);
        try
        {
            // Read the id now instead of using the notified value, so queued changes always apply the latest state.
            var userId = _cloudAccess.UserId;
            if (!force && string.Equals(userId, _initializedCloudUserId, StringComparison.Ordinal)) return;

            // The runtime holds the session files that may have to be deleted, so it is stopped first.
            await RestartAsync(userId != null ? () => EnsureCloudHistoryOwner(userId) : null);
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogError(ex,
                "Failed to apply the OneWare Cloud account change.");
        }
        finally
        {
            _cloudAccountSync.Release();
        }
    }

    /// <summary>
    /// Deletes the stored OneWare Cloud sessions when they belong to a different user than
    /// <paramref name="userId"/>. Must only run while the Copilot runtime is stopped.
    /// </summary>
    private void EnsureCloudHistoryOwner(string userId)
    {
        var directory = GetByokDataDirectory(GetByokConfiguration());
        var markerPath = Path.Combine(directory, CloudOwnerMarkerFileName);
        var owner = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();

        string? previousOwner = null;
        try
        {
            if (File.Exists(markerPath)) previousOwner = File.ReadAllText(markerPath).Trim();
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex,
                "Could not read the OneWare Cloud history owner.");
        }

        if (string.Equals(previousOwner, owner, StringComparison.Ordinal)) return;

        // Data without an owner (created before owners were tracked) is adopted by the first user.
        if (!string.IsNullOrEmpty(previousOwner))
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                try
                {
                    if (entry is DirectoryInfo subDirectory) subDirectory.Delete(true);
                    else entry.Delete();
                }
                catch (Exception ex)
                {
                    ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex,
                        "Could not delete OneWare Cloud history entry {Path}.", entry.FullName);
                }
            }

            NotifyHistoryCleared();
        }

        try
        {
            File.WriteAllText(markerPath, owner);
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex,
                "Could not store the OneWare Cloud history owner.");
        }
    }

    protected override bool HandleInitializationError(Exception exception)
    {
        if (FindCloudAiBlocked(exception) is not { } blocked) return false;

        if (blocked.Code == CloudAiNotInPlanErrorCode)
        {
            RaiseStatus(new StatusEvent(false, "Pro plan required"));
            ReportCloudAiUpgradeRequired();
        }
        else
        {
            RaiseStatus(new StatusEvent(false, "Disabled by organization"));
            ReportCloudAiDisabledByOrganization(blocked.Message);
        }

        return true;
    }

    #endregion

    #region Requests

    protected override CopilotRequestHandler CreateRequestHandler() =>
        new IdempotencyKeyRequestHandler(IdempotencyHeader, GetCurrentTurnIdempotencyKey);

    protected override void ConfigureProvider(ProviderConfig provider, ByokConfiguration configuration)
    {
        provider.BearerTokenProvider = _ => _cloudAccess.GetAccessTokenAsync();
    }

    protected override void ConfigureMessageOptions(MessageOptions options)
    {
        var idempotencyKey = Guid.NewGuid().ToString("N");
        _currentTurnIdempotencyKey = idempotencyKey;
        options.RequestHeaders = new Dictionary<string, string>
        {
            [IdempotencyHeader] = idempotencyKey
        };
    }

    // Requests outside a user turn (e.g. background work after a restart) still need a valid key.
    private string GetCurrentTurnIdempotencyKey()
    {
        return _currentTurnIdempotencyKey ??= Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// The runtime does not tell the model which ids the `task` tool's `model` parameter accepts
    /// (the OneWare Cloud session has no CAPI model catalog), so the list is provided here.
    /// </summary>
    protected override IEnumerable<string> GetRuntimeInstructions()
    {
        if (Models.Count == 0) yield break;

        var lines = Models.Select(m =>
        {
            var name = string.IsNullOrWhiteSpace(m.Name) || m.Name == m.Id ? "" : $" ({m.Name})";
            var efforts = m.Capabilities.Supports.ReasoningEffort && m.SupportedReasoningEfforts is { Count: > 0 } e
                ? $" — reasoning efforts: {string.Join(", ", e)}"
                : "";
            return $"- `{m.Id}`{name}{efforts}";
        });

        yield return "Models available for sub-agents. When delegating with the `task` tool you may pass one of " +
                     "these exact ids as `model`; omit it to use the current model. Never use ids not in this list.\n" +
                     string.Join("\n", lines);
    }

    #endregion

    #region Models

    protected override async Task AuthorizeModelListRequestAsync(HttpRequestMessage request,
        ByokConfiguration configuration, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", await _cloudAccess.GetAccessTokenAsync(cancellationToken));
    }

    protected override async Task<string?> ReadModelListErrorAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var (message, code) = await ReadProviderErrorAsync(response, cancellationToken);
        if (code is CloudAiNotInPlanErrorCode or CloudAiDisabledByOrganizationErrorCode)
            throw new CloudAiBlockedException(code, message);

        return message;
    }

    /// <summary>
    /// Enables the reasoning effort picker for OneWare Cloud models that report
    /// <c>supports_reasoning_effort</c> with their <c>reasoning_efforts</c>.
    /// </summary>
    protected override void ApplyModelMetadata(ModelInfo model, JsonElement item)
    {
        if (!item.TryGetProperty("supports_reasoning_effort", out var supports) ||
            supports.ValueKind != JsonValueKind.True ||
            !item.TryGetProperty("reasoning_efforts", out var effortsProperty) ||
            effortsProperty.ValueKind != JsonValueKind.Array)
            return;

        var efforts = effortsProperty.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        if (efforts.Count == 0) return;

        model.Capabilities.Supports.ReasoningEffort = true;
        model.SupportedReasoningEfforts = efforts;
        if (item.TryGetProperty("default_reasoning_effort", out var defaultEffort) &&
            defaultEffort.ValueKind == JsonValueKind.String &&
            efforts.Contains(defaultEffort.GetString()!))
            model.DefaultReasoningEffort = defaultEffort.GetString();
    }

    #endregion

    #region Errors and billing

    protected override bool HandleSessionError(SessionErrorEvent error, string? agentId)
    {
        if (error.Data.StatusCode is 403)
        {
            _ = ReportCloudAccessDeniedAsync(agentId);
            return true;
        }

        if (!IsInsufficientCreditsError(error.Data.Message)) return false;

        // Cloud AI is paid with Compute Credits. The 402 body carries the reason (MemberBudget when the
        // member's own monthly budget ran out, SpendingSuspended after a chargeback, else OrganizationCredits).
        var message = error.Data.Message ?? string.Empty;
        if (message.Contains("SpendingSuspended", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("suspended", StringComparison.OrdinalIgnoreCase))
        {
            RaiseEvent(new ChatErrorEvent(
                "Spending is suspended for this organization. Please contact OneWare support.")
            {
                AgentId = agentId
            });
            return true;
        }

        var budgetExceeded = message.Contains("MemberBudget", StringComparison.OrdinalIgnoreCase) ||
                             message.Contains("budget", StringComparison.OrdinalIgnoreCase);
        var path = budgetExceeded ? "/organization" : "/credits";
        RaiseEvent(new ChatButtonEvent(
            budgetExceeded
                ? "This request exceeds your monthly OneWare Cloud budget in this organization."
                : "This organization does not have enough OneWare Cloud credits for this request.",
            budgetExceeded ? "View budget" : "Add credits",
            new RelayCommand<Control?>(_ => PlatformHelper.OpenHyperLink($"{CloudBaseUrl}{path}")))
        {
            AgentId = agentId
        });
        return true;
    }

    /// <summary>Detects the 402 the OneWare Cloud endpoint returns when the organization is out of credits.</summary>
    private static bool IsInsufficientCreditsError(string? message)
    {
        return message is not null &&
               (message.Contains("insufficient_credits", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Payment Required", StringComparison.OrdinalIgnoreCase) ||
                InsufficientCreditsStatusRegex.IsMatch(message));
    }

    /// <summary>Reads <c>error.message</c> and <c>error.code</c> of a OneWare Cloud error response.</summary>
    private static async Task<(string? Message, string? Code)> ReadProviderErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("error", out var error) ||
                error.ValueKind != JsonValueKind.Object)
                return default;

            return (ReadString(error, "message"), ReadString(error, "code"));
        }
        catch (JsonException)
        {
            return default;
        }

        static string? ReadString(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static CloudAiBlockedException? FindCloudAiBlocked(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is CloudAiBlockedException blocked) return blocked;
            if (current is AggregateException aggregate &&
                aggregate.InnerExceptions.Select(FindCloudAiBlocked).FirstOrDefault(x => x != null) is { } inner)
                return inner;
        }

        return null;
    }

    /// <summary>
    /// The runtime only reports "Authentication failed (HTTP 403)" without the response body, so the reason is
    /// read from the model list, which applies the same organization and plan checks.
    /// </summary>
    private async Task ReportCloudAccessDeniedAsync(string? agentId)
    {
        (string? Message, string? Code) problem = default;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{CloudBaseUrl}/api/copilot/v1/models");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", await _cloudAccess.GetAccessTokenAsync());
            using var response = await ByokHttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                problem = await ReadProviderErrorAsync(response, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(ex,
                "Could not read why OneWare Cloud rejected the OneWare Cloud AI request.");
        }

        if (problem.Code == CloudAiNotInPlanErrorCode)
        {
            ReportCloudAiUpgradeRequired();
            return;
        }

        if (problem.Code == CloudAiDisabledByOrganizationErrorCode)
        {
            ReportCloudAiDisabledByOrganization(problem.Message);
            return;
        }

        RaiseEvent(new ChatErrorEvent(
            problem.Message ?? "OneWare Cloud rejected the request. Check the organization selected in your account.")
        {
            AgentId = agentId
        });
    }

    private void ReportCloudAiUpgradeRequired()
    {
        Blocker = new ChatServiceBlocker("OneWare Cloud Pro required",
            "OneWare Cloud AI is included in the Pro plans. Upgrade your organization, then refresh.")
        {
            ActionText = "Upgrade to Pro",
            ActionCommand = new RelayCommand<Control?>(_ =>
                PlatformHelper.OpenHyperLink($"{CloudBaseUrl}/organization/credits"))
        };
    }

    private void ReportCloudAiDisabledByOrganization(string? message)
    {
        Blocker = new ChatServiceBlocker("OneWare Cloud AI disabled",
            (message ?? "OneWare Cloud AI is turned off for this organization by its administrators.") +
            " Ask an organization admin to allow it, or switch to another organization or provider.");
    }

    /// <summary>OneWare Cloud refused Cloud AI for the organization (not in the plan or turned off by its admins).</summary>
    private sealed class CloudAiBlockedException(string code, string? message)
        : InvalidOperationException(message ?? "OneWare Cloud AI is not available for this organization.")
    {
        public string Code { get; } = code;
    }

    #endregion
}
