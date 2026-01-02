using System;
using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using AvaloniaEdit;
using DynamicData.Binding;
using OneWare.Output.ViewModels;
using ReactiveUI;

namespace OneWare.Output.Views;

public abstract class OutputBaseView : UserControl
{
    private TextEditor? _output;
    private OutputBaseViewModel? _viewModel;

    private CompositeDisposable _subscriptions = new();
    private bool _stopScroll;
    private bool _isAttached;

    protected OutputBaseView()
    {
        this.WhenValueChanged(x => x.DataContext)
            .Subscribe(_ => TryInitialize());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        TryInitialize();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttached = false;
        Cleanup();
    }

    private void TryInitialize()
    {
        if (!_isAttached)
            return;

        Cleanup();

        _output = this.FindControl<TextEditor>("Output");
        _viewModel = DataContext as OutputBaseViewModel;

        if (_output == null || _viewModel == null)
            return;

        InitializeEditor();
        BindViewModel();
    }

    private void InitializeEditor()
    {
        _output!.TextArea.TextView.LinkTextForegroundBrush =
            new BrushConverter().ConvertFrom("#f5cd56") as IBrush
            ?? throw new InvalidOperationException("Invalid brush color");

        _output.Document = _viewModel!.OutputDocument;
        _output.Document.UndoStack.SizeLimit = 0;

        _output.TextArea.TextView.LineTransformers.Clear();

        AddHandler(
            PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Tunnel,
            true);
    }

    private void BindViewModel()
    {
        _viewModel!.WhenValueChanged(x => x.AutoScroll)
            .Throttle(TimeSpan.FromMilliseconds(20))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(_ => ScrollToEnd())
            .DisposeWith(_subscriptions);

        _viewModel!.OutputDocument.WhenValueChanged(x => x.LineCount)
            .Throttle(TimeSpan.FromMilliseconds(20))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(_ => UpdateLineColors())
            .DisposeWith(_subscriptions);

        _viewModel.LineContexts.CollectionChanged += OnLineContextsChanged;

        UpdateLineColors();
        ScrollToEnd();
    }

    private void OnLineContextsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(UpdateLineColors, DispatcherPriority.Background);
    }

    private void UpdateLineColors()
    {
        if (_output == null || _viewModel == null)
            return;

        var transformers = _output.TextArea.TextView.LineTransformers;

        // Remove old colorizers safely
        for (int i = transformers.Count - 1; i >= 0; i--)
        {
            if (transformers[i] is LineColorizer)
                transformers.RemoveAt(i);
        }

        // Apply current line colors
        for (int i = 0; i < _viewModel.LineContexts.Count; i++)
        {
            var ctx = _viewModel.LineContexts[i];
            if (ctx.LineColor != null)
            {
                transformers.Add(new LineColorizer(i + 1, ctx.LineColor));
            }
        }

        _output.TextArea.TextView.Redraw();
    }

    private void ScrollToEnd()
    {
        if (_output == null || _viewModel == null)
            return;

        if (!_viewModel.AutoScroll || _stopScroll || !IsEffectivelyVisible)
            return;

        _output.CaretOffset = _output.Text.Length;
        _output.TextArea.Caret.BringCaretToView(5);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        _stopScroll =
            e.GetCurrentPoint(null).Properties.IsLeftButtonPressed &&
            _output?.IsPointerOver == true;
    }

    private void Cleanup()
    {
        _subscriptions.Dispose();
        _subscriptions = new CompositeDisposable();

        if (_viewModel != null)
            _viewModel.LineContexts.CollectionChanged -= OnLineContextsChanged;

        if (_output != null)
            _output.TextArea.TextView.LineTransformers.Clear();

        _viewModel = null;
        _output = null;
        _stopScroll = false;
    }
}
