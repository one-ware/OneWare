using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Interfaces;

namespace OneWare.SerialMonitor.Modules
{
    public class SerialMonitorModule : IOneWareModule
    {
        private readonly IContainerAdapter _containerAdapter;

        public SerialMonitorModule(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
        }

        public void RegisterTypes()
        {


            OnExecute();
        }

        public void OnExecute()
        {
        }
    }
}