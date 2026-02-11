using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.SignalR.Client;
using OneWare.CloudIntegration.Services;
using OneWare.CloudIntegration.Settings;

namespace OneWare.CloudIntegration.ViewModels;

public class CloudIntegrationMainWindowBottomRightExtensionViewModel : ObservableObject
{
    private readonly OneWareCloudNotificationService _notificationService;

    private HubConnectionState _connectionState;

    public CloudIntegrationMainWindowBottomRightExtensionViewModel(OneWareCloudAccountSetting accountSetting, OneWareCloudNotificationService service)
    {
        _notificationService = service;
        AccountSetting = accountSetting;
        
        service.ConnectionStateChanged += (sender, args) => { ConnectionState = args; };
    }

    public OneWareCloudAccountSetting AccountSetting { get; }
    
    public HubConnectionState ConnectionState
    {
        get => _connectionState;
        set
        {
            SetProperty(ref _connectionState, value);
            OnPropertyChanged(nameof(IsConnecting));
        }
    }

    public bool IsConnecting => ConnectionState is HubConnectionState.Connecting or HubConnectionState.Reconnecting;

    public async Task ConnectAsync()
    {
        if (_notificationService.ConnectionState == HubConnectionState.Connected)
            await _notificationService.DisconnectAsync();
        await _notificationService.ConnectAsync();
    }

    public async Task DisconnectAsync()
    {
        if (_notificationService.ConnectionState == HubConnectionState.Disconnected) return;
        await _notificationService.DisconnectAsync();
    }
}