using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OneWare.Chat.ViewModels;

namespace OneWare.Chat.Views;

public partial class ChatView : UserControl
{
    private CompositeDisposable _disposables = new();
    
    public ChatView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ChatViewModel chatViewModel)
        {
            _disposables.Dispose();
            _disposables = new CompositeDisposable();
            
            ScrollViewer.ScrollToEnd();

            Observable.FromEventPattern(chatViewModel, nameof(chatViewModel.ContentAdded)).Subscribe(x =>
            {
                ScrollViewer.ScrollToEnd();
            })
            .DisposeWith(_disposables);
        }
    }
}