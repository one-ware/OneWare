namespace OneWare.PackageManager.Models;

public class TabModel
{
    public TabModel(string title, string content)
    {
        Title = title;
        Content = content;
    }

    public string Title { get; }
    public string Content { get; }
}