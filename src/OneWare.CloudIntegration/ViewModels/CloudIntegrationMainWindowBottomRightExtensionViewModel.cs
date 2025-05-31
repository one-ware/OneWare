using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.SignalR.Client;
using OneWare.CloudIntegration.Services;

namespace OneWare.CloudIntegration.ViewModels;

public class CloudIntegrationMainWindowBottomRightExtensionViewModel : ObservableObject
{
    private readonly OneWareCloudNotificationService _notificationService;
    
    private HubConnectionState _connectionState;
    
    public CloudIntegrationMainWindowBottomRightExtensionViewModel(OneWareCloudNotificationService service)
    {
        _notificationService = service;
        
        service.ConnectionStateChanged += (sender, args) =>
        {
            ConnectionState = args;
        };
    }
    
    public HubConnectionState ConnectionState
    {
        get => _connectionState;
        set => SetProperty(ref _connectionState, value);
    }

    public async Task ConnectAsync()
    {
        if (_notificationService.ConnectionState == HubConnectionState.Connected)
        {
            await _notificationService.DisconnectAsync();
        } 
        await _notificationService.ConnectAsync();
    }
    
    public async Task DisconnectAsync()
    {
        if (_notificationService.ConnectionState == HubConnectionState.Disconnected)
        {
            return;
        }
        await _notificationService.DisconnectAsync();
    }
}