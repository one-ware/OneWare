using System.Collections.Generic;
using OneWare.Vcd.Parser.Data;
using OneWare.WaveFormViewer.ViewModels;
using Xunit;

namespace OneWare.WaveFormViewer.Tests;

public class WaveFormViewModelTests
{
    private static VcdSignal<StdLogic> CreateSignal(List<long> changeTimes, params (int timeIndex, StdLogic value)[] changes)
    {
        var signal = new VcdSignal<StdLogic>(changeTimes, VcdLineType.Reg, 1, "!", "clk");
        foreach (var (idx, val) in changes)
            signal.AddChange(idx, val);
        return signal;
    }

    [Fact]
    public void ClearMarkers_ResetsBothMarkers()
    {
        var vm = new WaveFormViewModel
        {
            Max = 1000,
            MarkerOffset = 100,
            SecondMarkerOffset = 200
        };

        vm.ClearMarkers();

        Assert.Equal(long.MaxValue, vm.MarkerOffset);
        Assert.Equal(long.MaxValue, vm.SecondMarkerOffset);
    }

    [Fact]
    public void JumpToNextEdge_MovesMarkerToNextChangeTime()
    {
        var changeTimes = new List<long> { 0, 10, 20, 30, 40 };
        var signal = CreateSignal(changeTimes,
            (0, StdLogic.L), (1, StdLogic.H), (2, StdLogic.L), (3, StdLogic.H), (4, StdLogic.L));

        var vm = new WaveFormViewModel { Max = 100 };
        var model = vm.AddSignal(signal);
        vm.SelectedSignal = model;

        vm.MarkerOffset = 15;
        vm.JumpToNextEdge();

        Assert.Equal(20, vm.MarkerOffset);
    }

    [Fact]
    public void JumpToNextEdge_FromChangeTime_JumpsStrictlyForward()
    {
        var changeTimes = new List<long> { 0, 10, 20, 30 };
        var signal = CreateSignal(changeTimes,
            (0, StdLogic.L), (1, StdLogic.H), (2, StdLogic.L), (3, StdLogic.H));

        var vm = new WaveFormViewModel { Max = 100 };
        var model = vm.AddSignal(signal);
        vm.SelectedSignal = model;

        vm.MarkerOffset = 20;
        vm.JumpToNextEdge();

        Assert.Equal(30, vm.MarkerOffset);
    }

    [Fact]
    public void JumpToNextEdge_PastLastEdge_DoesNothing()
    {
        var changeTimes = new List<long> { 0, 10, 20 };
        var signal = CreateSignal(changeTimes,
            (0, StdLogic.L), (1, StdLogic.H), (2, StdLogic.L));

        var vm = new WaveFormViewModel { Max = 100 };
        var model = vm.AddSignal(signal);
        vm.SelectedSignal = model;

        vm.MarkerOffset = 25;
        vm.JumpToNextEdge();

        Assert.Equal(25, vm.MarkerOffset);
    }

    [Fact]
    public void JumpToPreviousEdge_MovesMarkerToPreviousChangeTime()
    {
        var changeTimes = new List<long> { 0, 10, 20, 30, 40 };
        var signal = CreateSignal(changeTimes,
            (0, StdLogic.L), (1, StdLogic.H), (2, StdLogic.L), (3, StdLogic.H), (4, StdLogic.L));

        var vm = new WaveFormViewModel { Max = 100 };
        var model = vm.AddSignal(signal);
        vm.SelectedSignal = model;

        vm.MarkerOffset = 25;
        vm.JumpToPreviousEdge();

        Assert.Equal(20, vm.MarkerOffset);
    }

    [Fact]
    public void JumpToPreviousEdge_FromChangeTime_JumpsStrictlyBackward()
    {
        var changeTimes = new List<long> { 0, 10, 20, 30 };
        var signal = CreateSignal(changeTimes,
            (0, StdLogic.L), (1, StdLogic.H), (2, StdLogic.L), (3, StdLogic.H));

        var vm = new WaveFormViewModel { Max = 100 };
        var model = vm.AddSignal(signal);
        vm.SelectedSignal = model;

        vm.MarkerOffset = 20;
        vm.JumpToPreviousEdge();

        Assert.Equal(10, vm.MarkerOffset);
    }

    [Fact]
    public void JumpToPreviousEdge_BeforeFirstEdge_DoesNothing()
    {
        var changeTimes = new List<long> { 10, 20, 30 };
        var signal = CreateSignal(changeTimes,
            (0, StdLogic.L), (1, StdLogic.H), (2, StdLogic.L));

        var vm = new WaveFormViewModel { Max = 100 };
        var model = vm.AddSignal(signal);
        vm.SelectedSignal = model;

        vm.MarkerOffset = 5;
        vm.JumpToPreviousEdge();

        Assert.Equal(5, vm.MarkerOffset);
    }

    [Fact]
    public void JumpToNextEdge_WithoutSelectedSignal_DoesNothing()
    {
        var vm = new WaveFormViewModel { Max = 100, MarkerOffset = 10 };
        vm.JumpToNextEdge();
        Assert.Equal(10, vm.MarkerOffset);
    }
}
