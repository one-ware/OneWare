namespace OneWare.Core.Events;

public class TextEventArgs : EventArgs
{
    public TextEventArgs(string text)
    {
        Text = text;
    }

    public string Text { get; }
}