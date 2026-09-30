using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AvaloniaEdit;
using NSubstitute;
using OneWare.ApplicationCommands.Services;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Services;
using Xunit;

namespace OneWare.ApplicationCommands.UnitTests;

public class TextAreaApplicationCommandTests : IDisposable
{
    // ApplicationCommandService registers a static class handler, so share one instance across all tests.
    private static readonly Lazy<ApplicationCommandService> SharedService = new(() =>
    {
        var paths = Substitute.For<IPaths>();
        paths.AppDataDirectory.Returns(Path.GetTempPath());
        return new ApplicationCommandService(paths);
    });

    private readonly KeyGesture? _originalDeleteLineGesture = AvaloniaEditCommands.DeleteLine.Gesture;

    public void Dispose()
    {
        AvaloniaEditCommands.DeleteLine.Gesture = _originalDeleteLineGesture;
        SharedService.Value.ApplicationCommands.Clear();
    }

    [AvaloniaFact]
    public void TakeOverGesture_MovesGestureToApplicationCommand()
    {
        var command = new TextAreaApplicationCommand("Editor: Delete Line", AvaloniaEditCommands.DeleteLine, true);

        Assert.Equal(new KeyGesture(Key.D, KeyModifiers.Control), command.DefaultGesture);
        Assert.Equal(command.DefaultGesture, command.ActiveGesture);
        Assert.Null(AvaloniaEditCommands.DeleteLine.Gesture);
    }

    [AvaloniaFact]
    public void DefaultGesture_ExecutesThroughApplicationCommandService()
    {
        var service = SharedService.Value;
        service.RegisterCommand(
            new TextAreaApplicationCommand("Editor: Delete Line", AvaloniaEditCommands.DeleteLine, true));

        var (window, editor) = CreateEditor("line1\nline2\nline3");

        window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.D, "d");

        Assert.Equal("line2\nline3", editor.Text);
    }

    [AvaloniaFact]
    public void ReboundGesture_ReplacesDefaultGesture()
    {
        var service = SharedService.Value;
        var command = new TextAreaApplicationCommand("Editor: Delete Line", AvaloniaEditCommands.DeleteLine, true)
        {
            ActiveGesture = new KeyGesture(Key.E, KeyModifiers.Control)
        };
        service.RegisterCommand(command);

        var (window, editor) = CreateEditor("line1\nline2\nline3");

        window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.D, "d");
        Assert.Equal("line1\nline2\nline3", editor.Text);

        window.KeyPress(Key.E, RawInputModifiers.Control, PhysicalKey.E, "e");
        Assert.Equal("line2\nline3", editor.Text);
    }

    [AvaloniaFact]
    public void Execute_WithoutTextArea_ReturnsFalse()
    {
        var command = new TextAreaApplicationCommand("Editor: Delete Line", AvaloniaEditCommands.DeleteLine);
        var button = new Button();
        var window = new Window { Content = button };
        window.Show();

        Assert.False(command.CanExecute(button));
        Assert.False(command.Execute(button));
    }

    private static (Window window, TextEditor editor) CreateEditor(string text)
    {
        var editor = new TextEditor { Text = text };
        var window = new Window { Content = editor, Width = 400, Height = 300 };
        window.Show();
        editor.TextArea.Focus();
        editor.TextArea.Caret.Offset = 0;
        return (window, editor);
    }
}
