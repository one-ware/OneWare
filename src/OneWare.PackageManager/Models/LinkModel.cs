namespace OneWare.PackageManager.Models;

public class LinkModel
{
    public LinkModel(string label, string url)
    {
        Label = label;
        Url = url;
    }

    public string Label { get; }
    public string Url { get; }
}