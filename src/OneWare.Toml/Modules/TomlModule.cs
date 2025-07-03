using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Interfaces;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;

namespace OneWare.Toml.Modules
{
    public class TomlModule :IOneWareModule
    {
        private readonly IContainerAdapter _containerAdapter;

        public TomlModule(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
        }

        public void RegisterTypes()
        {

            OnExecute();
        }

        public void OnExecute()
        {
            _containerAdapter.Resolve<ILanguageManager>()
    .RegisterStandaloneTypeAssistance(typeof(TypeAssistanceToml), ".toml");
            _containerAdapter.Resolve<ILanguageManager>()
                .RegisterTextMateLanguage("toml", "avares://OneWare.Toml/Assets/TOML.tmLanguage.json", ".toml");
        }
    }
}