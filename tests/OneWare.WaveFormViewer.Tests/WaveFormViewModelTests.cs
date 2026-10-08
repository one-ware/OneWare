using OneWare.WaveFormViewer.Enums;
using OneWare.WaveFormViewer.ViewModels;
using Xunit;

namespace OneWare.WaveFormViewer.Tests;

public class WaveFormViewModelTests
{
    private readonly List<long> _changeTimes = [0, 10, 20, 30, 40];

    private WaveFormViewModel CreateViewModel()
    {
        var vm = new WaveFormViewModel { Max = 40 };
        vm.AddSignal(TestSignals.CreateBit("a", _changeTimes, 0, 30));
        vm.AddSignal(TestSignals.CreateBit("b", _changeTimes, 0, 10, 20));
        vm.AddSignal(TestSignals.CreateBus("c", _changeTimes));
        vm.AddSignal(TestSignals.CreateBus("d", _changeTimes));
        return vm;
    }

    [Fact]
    public void JumpToEdge_UsesSelectedSignals()
    {
        var vm = CreateViewModel();
        vm.MarkerOffset = 0;
        vm.SelectedSignals.Add(vm.Signals[0]);

        vm.JumpToNextEdge();
        Assert.Equal(30, vm.MarkerOffset);

        vm.JumpToPreviousEdge();
        Assert.Equal(0, vm.MarkerOffset);
    }

    [Fact]
    public void JumpToEdge_WithoutSelection_UsesAllSignals()
    {
        var vm = CreateViewModel();
        vm.MarkerOffset = 0;

        vm.JumpToNextEdge();
        Assert.Equal(10, vm.MarkerOffset);

        vm.JumpToNextEdge();
        Assert.Equal(20, vm.MarkerOffset);
    }

    [Fact]
    public void JumpToEdge_WithoutMarker_StartsAtViewOffset()
    {
        var vm = CreateViewModel();
        vm.Offset = 15;

        vm.JumpToNextEdge();

        Assert.Equal(20, vm.MarkerOffset);
    }

    [Fact]
    public void JumpToEdge_ScrollsMarkerIntoView()
    {
        var vm = CreateViewModel();
        vm.Max = 1000;
        vm.ZoomLevel = 3;
        vm.MarkerOffset = 0;
        vm.SelectedSignals.Add(vm.Signals[0]);

        vm.JumpToNextEdge();

        Assert.InRange(vm.MarkerOffset, vm.Offset, vm.Offset + vm.ViewPortWidth);
    }

    [Fact]
    public void RemoveSignals_RemovesSelectionIfClickedSignalIsSelected()
    {
        var vm = CreateViewModel();
        vm.SelectedSignals.Add(vm.Signals[0]);
        vm.SelectedSignals.Add(vm.Signals[1]);

        vm.RemoveSignals(vm.Signals[1]);

        Assert.Equal(["c", "d"], vm.Signals.Select(x => x.Signal.Id));
    }

    [Fact]
    public void RemoveSignals_RemovesOnlyClickedSignalIfNotSelected()
    {
        var vm = CreateViewModel();
        vm.SelectedSignals.Add(vm.Signals[0]);

        vm.RemoveSignals(vm.Signals[2]);

        Assert.Equal(["a", "b", "d"], vm.Signals.Select(x => x.Signal.Id));
    }

    [Fact]
    public void SetDataType_AppliesToSelectedSignalsThatSupportIt()
    {
        var vm = CreateViewModel();
        foreach (var signal in vm.Signals) vm.SelectedSignals.Add(signal);

        vm.SetDataType(WaveDataType.Hex);

        Assert.Equal(WaveDataType.Binary, vm.Signals[0].DataType);
        Assert.Equal(WaveDataType.Hex, vm.Signals[2].DataType);
        Assert.Equal(WaveDataType.Hex, vm.Signals[3].DataType);
    }

    [Fact]
    public void ToggleAutomaticFixedPointShift_AppliesToSelection()
    {
        var vm = CreateViewModel();
        vm.SelectedSignals.Add(vm.Signals[2]);
        vm.SelectedSignals.Add(vm.Signals[3]);

        vm.ToggleAutomaticFixedPointShift(vm.Signals[2]);

        Assert.True(vm.Signals[2].AutomaticFixedPointShift);
        Assert.True(vm.Signals[3].AutomaticFixedPointShift);
    }

    [Fact]
    public void RemoveSeparators_AppliesToSelection()
    {
        var vm = CreateViewModel();
        vm.Signals[0].SeparatorLabel = "Inputs";
        vm.Signals[1].SeparatorLabel = "";
        vm.SelectedSignals.Add(vm.Signals[0]);
        vm.SelectedSignals.Add(vm.Signals[1]);

        Assert.True(vm.Signals[1].HasSeparator);

        vm.RemoveSeparators(vm.Signals[0]);

        Assert.False(vm.Signals[0].HasSeparator);
        Assert.False(vm.Signals[1].HasSeparator);
    }

    [Fact]
    public void SecondMarker_ShowsDistanceUntilCleared()
    {
        var vm = CreateViewModel();
        vm.MarkerOffset = 5;
        vm.SecondMarkerOffset = 15;

        Assert.True(vm.HasSecondMarker);
        Assert.StartsWith("+", vm.MarkerTextOriginal);
        Assert.Equal("1", vm.Signals[1].MarkerValue);

        vm.ClearSecondMarker();

        Assert.False(vm.HasSecondMarker);
        Assert.Equal("5 ", vm.MarkerTextOriginal);
        Assert.Equal("0", vm.Signals[1].MarkerValue);
    }
}
