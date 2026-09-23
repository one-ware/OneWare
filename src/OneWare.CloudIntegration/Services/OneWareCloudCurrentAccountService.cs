using System.Net;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using Microsoft.AspNetCore.SignalR.Client;
using OneWare.CloudIntegration.Dto;
using OneWare.CloudIntegration.Settings;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.CloudIntegration.Services;

public class OneWareCloudCurrentAccountService : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly OneWareCloudAccountSetting _accountSetting;
    private readonly OneWareCloudLoginService _loginService;

    public OneWareCloudCurrentAccountService(
        OneWareCloudAccountSetting accountSetting,
        OneWareCloudLoginService loginService,
        OneWareCloudNotificationService notificationService)
    {
        _accountSetting = accountSetting;
        _loginService = loginService;

        accountSetting.WhenValueChanged(x => x.Value)
            .Subscribe(_ => _ = ResolveAsync());

        Observable.FromEventPattern<HubConnectionState>(
                notificationService,
                nameof(notificationService.ConnectionStateChanged))
            .Subscribe(_ => OnConnectionStateChanged(notificationService.ConnectionState));

        notificationService.SubscribeToHubMethod<OrganizationBalanceDto>("Balance_Updated", OnBalanceUpdated);
        // Sent after the active organization changed on the server (switch, invitation, leave, removal, deletion).
        notificationService.SubscribeToHubMethod<OrganizationBalanceDto>("ActiveOrganization_Changed",
            SetActiveOrganization);
    }

    public bool IsConnected
    {
        get;
        set => SetProperty(ref field, value);
    }

    public CurrentUserDto? CurrentUser
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    ///     The user's view of their active organization and its credits, or null when signed out or when the user
    ///     has no active organization. Members only see their own budget; organization balances are null for them.
    /// </summary>
    public OrganizationBalanceDto? ActiveOrganization
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string? UserId => _accountSetting.Value.ToString();

    /// <summary>Reloads <see cref="ActiveOrganization" />, e.g. after the active organization was changed on the web.</summary>
    public async Task RefreshActiveOrganizationAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(UserId)) return;

            var (jwt, _) = await _loginService.GetJwtTokenAsync(UserId);
            if (jwt == null) return;

            var request = new RestRequest("/api/credits/balance");
            request.AddHeader("Authorization", $"Bearer {jwt.RawData}");
            var response = await _loginService.GetRestClient().ExecuteGetAsync(request);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            {
                SetActiveOrganization(null);
                return;
            }

            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content)) return;

            SetActiveOrganization(JsonSerializer.Deserialize<OrganizationBalanceDto>(response.Content, JsonOptions));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void OnConnectionStateChanged(HubConnectionState state)
    {
        IsConnected = state == HubConnectionState.Connected;
        // Balance updates may have been missed while disconnected.
        if (IsConnected && CurrentUser != null)
            _ = RefreshActiveOrganizationAsync();
    }

    private void OnBalanceUpdated(OrganizationBalanceDto update)
    {
        // Balance_Updated is sent for every organization the user belongs to; only the active one is shown.
        var activeOrganizationId = ActiveOrganization?.OrganizationId ?? CurrentUser?.DefaultOrganizationId;
        if (activeOrganizationId != update.OrganizationId) return;

        SetActiveOrganization(update);
    }

    private void SetActiveOrganization(OrganizationBalanceDto? organization)
    {
        if (Dispatcher.UIThread.CheckAccess())
            ActiveOrganization = organization;
        else
            Dispatcher.UIThread.Post(() => ActiveOrganization = organization);
    }

    private async Task ResolveAsync()
    {
        try
        {
            _accountSetting.Image = null;
            _accountSetting.Email = null;
            CurrentUser = null;
            SetActiveOrganization(null);

            if (string.IsNullOrEmpty(UserId)) return;

            var (jwt, status) = await _loginService.GetJwtTokenAsync(UserId);
            if (jwt == null)
            {
                if (status == HttpStatusCode.Unauthorized)
                {
                    _loginService.Logout(UserId);
                    _accountSetting.Value = string.Empty;
                }

                return;
            }

            var request = new RestRequest("/api/users/current");
            request.AddHeader("Authorization", $"Bearer {jwt.RawData}");

            var response = await _loginService.GetRestClient().ExecuteGetAsync(request);
            CurrentUser = JsonSerializer.Deserialize<CurrentUserDto>(
                response.Content!,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            _accountSetting.Email = CurrentUser?.Email ?? string.Empty;
            await RefreshActiveOrganizationAsync();
            await ContainerLocator.Container.Resolve<OneWareCloudNotificationService>().ConnectAsync();

            var httpService = ContainerLocator.Container.Resolve<IHttpService>();
            if (CurrentUser?.AvatarUrl != null)
                _accountSetting.Image = await httpService.DownloadImageAsync(CurrentUser.AvatarUrl);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
