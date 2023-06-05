using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Shared;
using Prism.Ioc;
using RestSharp;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

namespace OneWare.Core.ViewModels.Windows
{
    internal class LoginWindowViewModel : FlexibleWindowViewModelBase
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public bool RememberPassword { get; set; }
        
        public async Task LoginAsync(Window window)
        {
            var client = new RestClient("https://api.vhdplus.com");

            var request = new RestRequest("/auth/login", Method.Post);
            request.AddParameter("username", this.Email);
            request.AddParameter("password", this.Password);
            
            var result = await client.ExecuteAsync(request);
            
            ContainerLocator.Container.Resolve<ILogger>()?.Error("RES: " + result.Content, null, true, true, window);
        }
    }
}