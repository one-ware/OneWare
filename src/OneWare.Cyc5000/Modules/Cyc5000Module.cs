using OneWare.Essentials.Adapters;
using OneWare.Essentials.Interfaces;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Cyc5000.Modules
{
    public class Cyc5000Module : IOneWareModule
    {
        private readonly IContainerAdapter _containerAdapter;

        public Cyc5000Module(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
        }

        public void RegisterTypes()
        {


            OnExecute();
        }

        public void OnExecute()
        {
             _containerAdapter.Resolve<FpgaService>().RegisterFpgaPackage(new Cyc5000FpgaPackage());
        }
    }
}