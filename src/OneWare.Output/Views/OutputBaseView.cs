using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using DynamicData.Binding;
using OneWare.Output.ViewModels;

namespace OneWare.Output.Views;

public abstract class OutputBaseView : UserControl
{
    private TextEditor? _output;

    public OutputBaseView()
    {
        this.WhenValueChanged(x => x.DataContext).Subscribe(xb =>
        {
            _output = this.Find<TextEditor>("Output");
            if (_output == null) return;

            _output.TextArea.TextView.LinkTextForegroundBrush =
                new BrushConverter().ConvertFrom("#f5cd56") as IBrush ?? throw new NullReferenceException();

            var viewModel = DataContext as OutputBaseViewModel;
            if (viewModel != null)
            {
            }
            else
            {
                //_logger.LogError("Output no datacontext!"); TODO
                return;
            }

            viewModel.OutputDocument.UndoStack.SizeLimit = 0;
            _output.Document = viewModel.OutputDocument;

            var stopScroll = false;

            viewModel.WhenValueChanged(xa => xa.AutoScroll).Subscribe(_ =>
            {
                if (!IsEffectivelyVisible) return;
                _output.CaretOffset = _output.Text.Length;
                _output.TextArea.Caret.BringCaretToView(5);
            });

            viewModel.OutputDocument.WhenValueChanged(xa => xa.LineCount).Subscribe(x =>
            {
                _ = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateLineColors(viewModel);

                    if (viewModel is { AutoScroll: true } && !stopScroll)
                    {
                        _output.CaretOffset = _output.Text.Length;
                        if (IsEffectivelyVisible)
                            _output.TextArea.Caret.BringCaretToView(5);
                    }
                }, DispatcherPriority.Background);
            });

            viewModel.LineContexts.ToObservableChangeSet().Subscribe(_ => UpdateLineColors(viewModel));

            AddHandler(PointerMovedEvent, (_, i) =>
            {
                if (i.GetCurrentPoint(null).Properties.IsLeftButtonPressed && _output.IsPointerOver)
                    stopScroll = true;
                else
                    stopScroll = false;
            }, RoutingStrategies.Tunnel, true);

            _output.CaretOffset = _output.Text.Length;
            _output.TextArea.Caret.BringCaretToView(1);
        });
    }

    protected void UpdateLineColors(OutputBaseViewModel evm)
    {
        if (_output == null) throw new NullReferenceException(nameof(_output));
        for(var i = 2; i < _output.TextArea.TextView.LineTransformers.Count; i++)
            _output.TextArea.TextView.LineTransformers.RemoveAt(i);
        for (var i = 0; i < evm.LineContexts.Count; i++)
            if (evm.LineContexts[i].LineColor != null)
                _output.TextArea.TextView.LineTransformers.Add(new LineColorizer(i + 1, evm.LineContexts[i].LineColor));
    }
}