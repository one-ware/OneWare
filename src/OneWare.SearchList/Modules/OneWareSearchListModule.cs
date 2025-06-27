using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OneWare.Essentials.Adapters;

namespace OneWare.SearchList.Modules;

public class OneWareSearchListModule
{
    private readonly IContainerAdapter _containerAdapter;
    private IConfiguration _configuration;

    public OneWareSearchListModule(IContainerAdapter containerAdapter)
    {
        _containerAdapter = containerAdapter;
    }

    public void Load()
    {

    }
}