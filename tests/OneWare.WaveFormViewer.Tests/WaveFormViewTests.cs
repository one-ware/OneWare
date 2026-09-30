using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OneWare.WaveFormViewer.Controls;
using OneWare.WaveFormViewer.ViewModels;
using OneWare.WaveFormViewer.Views;
using Xunit;

namespace OneWare.WaveFormViewer.Tests;

public class WaveFormViewTests
{
    private readonly List<long> _changeTimes = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];

    private (Window window, WaveFormView view, WaveFormViewModel vm) Create()
    {
        var vm = new WaveFormViewModel { Max = 100 };
        vm.AddSignal(TestSignals.CreateBit("a", _changeTimes, 0, 50));
        vm.AddSignal(TestSignals.CreateBit("b", _changeTimes, 0, 20, 70));

        var view = new WaveFormView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, view, vm);
    }

    private static WaveFormEffects Effects(WaveFormView view)
    {
        return view.GetControl<WaveFormEffects>("SimulatorEffectsRenderer");
    }

    /// <summary>
    ///     Returns a point in window coordinates at <paramref name="fraction" /> of the wave area width.
    /// </summary>
    private static Point WavePoint(Window window, WaveFormView view, double fraction)
    {
        var effects = Effects(view);
        return effects.TranslatePoint(new Point(effects.Bounds.Width * fraction, 20), window)!.Value;
    }

    private static long OffsetAt(WaveFormView view, double fraction)
    {
        var effects = Effects(view);
        return effects.GetOffsetFromPosition(effects.Bounds.Width * fraction);
    }

    [AvaloniaFact]
    public void Drag_KeepsSecondMarkerAfterRelease()
    {
        var (window, view, vm) = Create();

        window.MouseDown(WavePoint(window, view, 0.2), MouseButton.Left);
        window.MouseMove(WavePoint(window, view, 0.6));
        window.MouseUp(WavePoint(window, view, 0.6), MouseButton.Left);

        Assert.Equal(OffsetAt(view, 0.2), vm.MarkerOffset);
        Assert.Equal(OffsetAt(view, 0.6), vm.SecondMarkerOffset);
        Assert.True(vm.HasSecondMarker);
    }

    [AvaloniaFact]
    public void Click_MovesMarkerAndKeepsSecondMarker()
    {
        var (window, view, vm) = Create();
        vm.SecondMarkerOffset = 80;

        window.MouseDown(WavePoint(window, view, 0.3), MouseButton.Left);
        window.MouseUp(WavePoint(window, view, 0.3), MouseButton.Left);

        Assert.Equal(OffsetAt(view, 0.3), vm.MarkerOffset);
        Assert.Equal(80, vm.SecondMarkerOffset);
    }

    [AvaloniaFact]
    public void RightClick_SetsSecondMarker()
    {
        var (window, view, vm) = Create();
        vm.MarkerOffset = 10;

        window.MouseDown(WavePoint(window, view, 0.5), MouseButton.Right);
        window.MouseUp(WavePoint(window, view, 0.5), MouseButton.Right);

        Assert.Equal(10, vm.MarkerOffset);
        Assert.Equal(OffsetAt(view, 0.5), vm.SecondMarkerOffset);
    }

    [AvaloniaFact]
    public void ShiftClick_SetsSecondMarker()
    {
        var (window, view, vm) = Create();
        vm.MarkerOffset = 10;

        window.MouseDown(WavePoint(window, view, 0.5), MouseButton.Left, RawInputModifiers.Shift);
        window.MouseUp(WavePoint(window, view, 0.5), MouseButton.Left, RawInputModifiers.Shift);

        Assert.Equal(10, vm.MarkerOffset);
        Assert.Equal(OffsetAt(view, 0.5), vm.SecondMarkerOffset);
    }

    [AvaloniaFact]
    public void Escape_ClearsSecondMarker()
    {
        var (window, view, vm) = Create();
        vm.MarkerOffset = 10;
        vm.SecondMarkerOffset = 50;
        view.Focus();

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

        Assert.False(vm.HasSecondMarker);
        Assert.Equal(10, vm.MarkerOffset);
    }

    [AvaloniaFact]
    public void CtrlArrows_JumpBetweenEdges()
    {
        var (window, view, vm) = Create();
        vm.MarkerOffset = 0;
        view.Focus();

        window.KeyPress(Key.Right, RawInputModifiers.Control, PhysicalKey.ArrowRight, null);
        Assert.Equal(20, vm.MarkerOffset);

        window.KeyPress(Key.Right, RawInputModifiers.Control, PhysicalKey.ArrowRight, null);
        Assert.Equal(50, vm.MarkerOffset);

        window.KeyPress(Key.Left, RawInputModifiers.Control, PhysicalKey.ArrowLeft, null);
        Assert.Equal(20, vm.MarkerOffset);
    }

    [AvaloniaFact]
    public void MultiSelection_IsSyncedAndCanBeDeleted()
    {
        var (window, view, vm) = Create();
        var list = view.GetControl<ListBox>("TextPartScroll");

        list.SelectAll();
        Assert.Equal(2, vm.SelectedSignals.Count);

        list.ContainerFromIndex(0)!.Focus();
        window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);

        Assert.Empty(vm.Signals);
    }

    [AvaloniaFact]
    public void Separator_KeepsNameAndWaveRowsAligned()
    {
        var (_, view, vm) = Create();
        vm.Signals[1].SeparatorLabel = "Outputs";
        Dispatcher.UIThread.RunJobs();
        view.UpdateLayout();

        var names = view.GetControl<ListBox>("TextPartScroll");
        var waves = view.GetControl<ListBox>("SimPartScroll");

        for (var i = 0; i < vm.Signals.Count; i++)
        {
            var name = names.ContainerFromIndex(i)!.Bounds;
            var wave = waves.ContainerFromIndex(i)!.Bounds;
            Assert.Equal(name.Y, wave.Y);
            Assert.Equal(name.Height, wave.Height);
        }

        Assert.True(names.ContainerFromIndex(1)!.Bounds.Height > names.ContainerFromIndex(0)!.Bounds.Height);
    }

    [AvaloniaFact]
    public void RenderImage_ContainsOnlyTheWaveformArea()
    {
        var (_, view, vm) = Create();
        vm.Signals[1].SeparatorLabel = "Outputs";
        vm.MarkerOffset = 30;
        vm.SecondMarkerOffset = 60;
        Dispatcher.UIThread.RunJobs();

        using var withMarkers = view.RenderImage(true);
        using var withoutMarkers = view.RenderImage(false);

        var area = view.GetControl<WaveFormScale>("SimulatorScaleRenderer").Bounds;
        Assert.NotNull(withMarkers);
        Assert.NotNull(withoutMarkers);
        Assert.Equal(PixelSize.FromSize(area.Size, 1), withMarkers.PixelSize);
        Assert.Equal(withMarkers.PixelSize, withoutMarkers.PixelSize);
        Assert.True(Effects(view).IsVisible);
        Assert.NotEqual(GetPixels(withMarkers), GetPixels(withoutMarkers));

        if (Environment.GetEnvironmentVariable("WAVEFORM_EXPORT_SAMPLE") is { } samplePath)
            withMarkers.Save(samplePath);

        var path = Path.Combine(Path.GetTempPath(), $"waveform-{Guid.NewGuid()}.png");
        try
        {
            withMarkers.Save(path);
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] GetPixels(Bitmap bitmap)
    {
        var stride = bitmap.PixelSize.Width * 4;
        var pixels = new byte[stride * bitmap.PixelSize.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), handle.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        return pixels;
    }
}
