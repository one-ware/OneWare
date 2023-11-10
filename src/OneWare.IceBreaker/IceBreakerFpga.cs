using Avalonia.Controls;
using OneWare.IceBreaker.Views;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.IceBreaker;

public class IceBreakerFpga : FpgaModel
{
    public IceBreakerFpga() : base("IceBreaker V1_0E", new IceBreakerV1_0e())
    {
        
    }
}