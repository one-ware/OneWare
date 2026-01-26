using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.ViewModels.Windows;

public enum MessageBoxMode
{
    AllButtons,
    NoCancel,
    OnlyOk,
    Input,
    PasswordInput,
    SelectFolder,
    SelectItem
}

public class MessageBoxViewModel : FlexibleWindowViewModelBase
{
    private readonly MessageBoxRequest _request;
    private FlexibleWindow? _window;
    private MessageBoxResult _result = MessageBoxResult.Canceled();
    private string? _input;
    private object? _selectedItem;
    private ObservableCollection<object> _selectionItems = new();

    public MessageBoxViewModel(MessageBoxRequest request)
    {
        _request = request;
        Title = request.Title;
        Message = request.Message;
        Id = "MessageBox...";

        if (request.Input is { DefaultValue: { } defaultValue })
            _input = defaultValue;

        if (request.SelectionItems != null)
            SelectionItems = new ObservableCollection<object>(request.SelectionItems);

        SelectedItem = request.SelectedItem;

        Buttons = new ObservableCollection<MessageBoxButtonViewModel>(
            (request.Buttons.Count > 0 ? request.Buttons : CreateFallbackButtons())
            .Select(button => new MessageBoxButtonViewModel(button))
        );

        ButtonCommand = new RelayCommand<MessageBoxButtonViewModel>(ExecuteButton, CanExecuteButton);
    }

    public string? Input
    {
        get => _input;
        set
        {
            if (SetProperty(ref _input, value))
                ButtonCommand.NotifyCanExecuteChanged();
        }
    }

    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
                ButtonCommand.NotifyCanExecuteChanged();
        }
    }

    public ObservableCollection<object> SelectionItems
    {
        get => _selectionItems;
        set => SetProperty(ref _selectionItems, value);
    }

    public ObservableCollection<MessageBoxButtonViewModel> Buttons { get; }

    public IRelayCommand<MessageBoxButtonViewModel> ButtonCommand { get; }

    public MessageBoxResult Result => _result;

    public string Message { get; }

    public bool ShowInput => _request.Input != null;
    public bool ShowFolderButton => _request.Input?.ShowFolderButton == true;
    public bool ShowSelection => SelectionItems.Count > 0;
    public string? InputLabel => _request.Input?.Label;
    public bool ShowInputLabel => !string.IsNullOrWhiteSpace(InputLabel);
    public string? InputPlaceholder => _request.Input?.Placeholder;
    public char PasswordChar => _request.Input?.Kind == MessageBoxInputKind.Password ? '*' : '\0';
    public bool InputRequired => _request.Input?.IsRequired == true;
    public bool SelectionRequired => _request.SelectionRequired;

    public void AttachWindow(FlexibleWindow window)
    {
        _window = window;
    }

    public bool TryExecuteDefault()
    {
        var button = Buttons.FirstOrDefault(x => x.IsDefault) ?? Buttons.FirstOrDefault();
        if (button == null || !ButtonCommand.CanExecute(button))
            return false;

        ButtonCommand.Execute(button);
        return true;
    }

    public bool TryExecuteCancel()
    {
        var button = Buttons.FirstOrDefault(x => x.IsCancel || x.Role == MessageBoxButtonRole.Cancel);
        if (button == null || !ButtonCommand.CanExecute(button))
            return false;

        ButtonCommand.Execute(button);
        return true;
    }

    public void EnsureCanceled()
    {
        if (_result.Button == null)
            _result = MessageBoxResult.Canceled();
    }

    public async Task SelectPathAsync(FlexibleWindow window)
    {
        if (window.Host == null) throw new NullReferenceException(nameof(FlexibleWindow));

        var folder = await StorageProviderHelper.SelectFolderAsync(window.Host, "Select Directory", Input);

        Input = folder ?? "";
    }

    private void ExecuteButton(MessageBoxButtonViewModel? button)
    {
        if (button == null)
            return;

        _result = new MessageBoxResult
        {
            Button = button.Model,
            Input = Input,
            SelectedItem = SelectedItem
        };

        _window?.Close();
    }

    private bool CanExecuteButton(MessageBoxButtonViewModel? button)
    {
        if (button == null)
            return false;

        if (button.Role == MessageBoxButtonRole.Cancel)
            return true;

        if (InputRequired && string.IsNullOrWhiteSpace(Input))
            return false;

        if (SelectionRequired && SelectedItem == null)
            return false;

        return true;
    }

    private static IEnumerable<MessageBoxButton> CreateFallbackButtons()
    {
        yield return new MessageBoxButton
        {
            Text = "Ok",
            Role = MessageBoxButtonRole.Yes,
            Style = MessageBoxButtonStyle.Primary,
            IsDefault = true,
        };
    }
}

public class MessageBoxButtonViewModel : ObservableObject
{
    public MessageBoxButtonViewModel(MessageBoxButton model)
    {
        Model = model;
    }

    public MessageBoxButton Model { get; }
    public string Text => Model.Text;
    public bool IsDefault => Model.IsDefault;
    public bool IsCancel => Model.IsCancel;
    public MessageBoxButtonRole Role => Model.Role;

    public Classes StyleClass => Model.Style switch
    {
        MessageBoxButtonStyle.Primary => new Classes("PrimaryButton"),
        MessageBoxButtonStyle.Secondary => new Classes("SecondaryButton"),
        MessageBoxButtonStyle.Danger => new Classes("MessageBoxDanger"),
        _ => new Classes()
    };
}
