namespace OneWare.PackageManager.Models;

public class LinkModel
{
    public string Label { get; }
    public string Url { get; }

    public LinkModel(string label, string url)
    {
        Label = label;
        Url = url;
    }
}