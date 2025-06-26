using Microsoft.Extensions.Configuration;
using OneWare.Core.Adapters;
using Serilog;
using Serilog.Extensions.Autofac.DependencyInjection;

namespace OneWare.Core.Modules
{
    public class OneWareCoreModule
    {
        private readonly IContainerAdapter _containerAdapter;
        private IConfiguration _configuration;

        public OneWareCoreModule(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
        }

        public void Load()
        {
            _configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory()) // Set base path to application's executable directory
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // Load appsettings.json
               .Build();

            var loggerConfig = new LoggerConfiguration()
               .ReadFrom.Configuration(_configuration); // READ FROM CONFIG

            // Configure Serilog
            //var loggerConfig = new LoggerConfiguration()
            //    .MinimumLevel.Debug()
            //    .WriteTo.Console();


            if (_containerAdapter is AutofacContainerAdapter autofacAdapter)
            {
                autofacAdapter.ConfigureBuilder(builder =>
                {
                    builder.RegisterSerilog(loggerConfig);
                });
            }
            else
            {
                var logger = loggerConfig.CreateLogger();
                _containerAdapter.RegisterInstance<ILogger>(logger); // Register as instance, not explicit singleton param
            }
        }
    }
}