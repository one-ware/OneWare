
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

namespace OneWare.Json.Modules
{
    public class JsonModule :IOneWareModule
    {
        private readonly IContainerAdapter _containerAdapter;
        
        public JsonModule(IContainerAdapter containerAdapter)
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
           .RegisterStandaloneTypeAssistance(typeof(TypeAssistanceJson), ".json");
        }
    }
}
