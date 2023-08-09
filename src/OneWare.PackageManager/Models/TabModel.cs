namespace OneWare.PackageManager.Models;

public class TabModel
{
    public string Title { get; }
    public string Content { get; }

    public TabModel(string title, string content)
    {
        Title = title;
        Content = content;
    }
}