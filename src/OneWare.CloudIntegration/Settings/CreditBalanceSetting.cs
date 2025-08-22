using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.SignalR.Client;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.ViewModels;
using RestSharp;

namespace OneWare.CloudIntegration.Settings;

public class CreditBalanceSetting(string title, IImage icon) : ObservableObject, IOneWareCloudAccountFlyoutSetting
{
    private string? _value;
    private bool _isVisible;
    
    public string Title { get; } = title;
    public IImage? Icon { get; set; } = icon;
    public string? Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public async Task UpdateBalanceAsync(OneWareCloudLoginService loginService)
    {
        var balance = await GetBalanceAsync(loginService);
        Value = balance.ToString();
    }
    public void SubscribeToHub(OneWareCloudNotificationService service)
    {
        service.SubscribeToHubMethod<int>("Credits_Updated", creditBalance =>
        {
            Value = creditBalance.ToString();
        });
    }
    private async Task<int> GetBalanceAsync(OneWareCloudLoginService cloudLoginService, CancellationToken cancellationToken = default)
    {
        var request = new RestRequest($"/api/users/balance");
        var response = await Task.Run(() =>
        {
            var (jwt, status) = cloudLoginService.GetLoggedInJwtTokenAsync().WaitAsync(cancellationToken).Result;
            if (jwt == null)
                return null;
            
            request.AddHeader("Authorization", $"Bearer {jwt}");
            request.AddHeader("Accept", "application/json");

            return cloudLoginService.GetRestClient().ExecuteAsync<int>(request, cancellationToken).WaitAsync(cancellationToken).Result;
        }, cancellationToken);
        
        return response?.StatusCode == HttpStatusCode.OK ? response.Data : 0;
    }
}