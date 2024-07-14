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
    private TextEditor? Output;

    public OutputBaseView()
    {
        this.WhenValueChanged(x => x.DataContext).Subscribe(x =>
        {
            Output = this.Find<TextEditor>("Output");
            if (Output == null) return;

            Output.TextArea.TextView.LinkTextForegroundBrush =
                new BrushConverter().ConvertFrom("#f5cd56") as IBrush ?? throw new NullReferenceException();

            var viewModel = DataContext as OutputBaseViewModel;
            if (viewModel != null)
            {
            }
            else
            {
                //ContainerLocator.Container.Resolve<ILogger>()?.Error("Output no datacontext!"); TODO
                return;
            }

            viewModel.OutputDocument.UndoStack.SizeLimit = 0;
            Output.Document = viewModel.OutputDocument;

            var stopScroll = false;

            viewModel.WhenValueChanged(x => x.AutoScroll).Subscribe(x =>
            {
                if (!IsEffectivelyVisible) return;
                Output.CaretOffset = Output.Text.Length;
                Output.TextArea.Caret.BringCaretToView(5);
            });

            viewModel.OutputDocument.WhenValueChanged(x => x.LineCount).Subscribe(x =>
            {
                _ = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateLineColors(viewModel);

                    if (viewModel is { AutoScroll: true } && !stopScroll)
                    {
                        Output.CaretOffset = Output.Text.Length;
                        if (IsEffectivelyVisible)
                            Output.TextArea.Caret.BringCaretToView(5);
                    }
                }, DispatcherPriority.Background);
            });

            viewModel?.LineColors.ToObservableChangeSet().Subscribe(changes => UpdateLineColors(viewModel));

            AddHandler(PointerMovedEvent, (o, i) =>
            {
                if (i.GetCurrentPoint(null).Properties.IsLeftButtonPressed && Output.IsPointerOver)
                    stopScroll = true;
                else
                    stopScroll = false;
            }, RoutingStrategies.Tunnel, true);

            Output.CaretOffset = Output.Text.Length;
            Output.TextArea.Caret.BringCaretToView(1);
        });
    }

    protected void UpdateLineColors(OutputBaseViewModel evm)
    {
        if (Output == null) throw new NullReferenceException(nameof(Output));
        Output.TextArea.TextView.LineTransformers.Clear();
        for (var i = 0; i < evm.LineColors.Count; i++)
            if (evm.LineColors[i] != null)
                Output.TextArea.TextView.LineTransformers.Add(new LineColorizer(i + 1, evm.LineColors[i]));
    }
}