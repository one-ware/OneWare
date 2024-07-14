using Avalonia.Controls;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using RestSharp;

namespace OneWare.Core.ViewModels.Windows;

internal class LoginWindowViewModel : FlexibleWindowViewModelBase
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public bool RememberPassword { get; set; }

    public async Task LoginAsync(Window window)
    {
        var client = new RestClient("https://api.vhdplus.com");

        var request = new RestRequest("/auth/login", Method.Post);
        request.AddParameter("username", Email);
        request.AddParameter("password", Password);

        var result = await client.ExecuteAsync(request);

        ContainerLocator.Container.Resolve<ILogger>()?.Error("RES: " + result.Content, null, true, true, window);
    }
}