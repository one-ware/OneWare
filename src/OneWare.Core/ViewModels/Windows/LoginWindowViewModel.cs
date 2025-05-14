using Avalonia.Controls;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using RestSharp;
using Autofac;  // Import Autofac for DI

namespace OneWare.Core.ViewModels.Windows
{
    internal class LoginWindowViewModel : FlexibleWindowViewModelBase
    {
        private readonly ILogger _logger;  // Injected ILogger via Autofac
        private readonly RestClient _client;  // Injected RestClient via Autofac or instantiate here

        // Constructor with Autofac DI
        public LoginWindowViewModel(ILogger logger, RestClient client)
        {
            _logger = logger;  // Assign injected ILogger
            _client = client ?? new RestClient("https://api.vhdplus.com"); // Default if not injected
        }

        public string? Email { get; set; }
        public string? Password { get; set; }
        public bool RememberPassword { get; set; }

        public async Task LoginAsync(Window window)
        {
            var request = new RestRequest("/auth/login", Method.Post);
            request.AddParameter("username", Email);
            request.AddParameter("password", Password);

            var result = await _client.ExecuteAsync(request);

            _logger?.Error("RES: " + result.Content, null, true, true, window);  // Use injected ILogger
        }
    }
}
