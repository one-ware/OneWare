using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.IceBreaker.ViewModels;

public class IceBreakerV10EViewModel : FpgaModelBase
{
    public IceBreakerV10EViewModel()
    {
        LoadFromJson("avares://OneWare.IceBreaker/Assets/IceBreakerV1.0e.json");
    }
}