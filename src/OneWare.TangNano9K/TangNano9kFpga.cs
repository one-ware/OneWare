﻿using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.TangNano9K;

public class TangNano9KFpga : FpgaBase
{
    public TangNano9KFpga()
    {
        LoadFromJsonAsset("avares://OneWare.TangNano9K/Assets/TangNano9K.json");
    }
}