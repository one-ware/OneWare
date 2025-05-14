using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.Essentials.Services;
using Autofac;  // Add this import for Autofac

namespace OneWare.Essentials.Models;

public class ExternalFile : ObservableObject, IFile
{
    private readonly IFileIconService _fileIconService;
    private IImage? _icon;

    // Constructor now accepts IFileIconService through dependency injection
    public ExternalFile(string fullPath, IFileIconService fileIconService)
    {
        _fileIconService = fileIconService ?? throw new ArgumentNullException(nameof(fileIconService));
        FullPath = fullPath;

        IDisposable? fileSubscription = null;

        this.WhenValueChanged(x => x.FullPath).Subscribe(x =>
        {
            fileSubscription?.Dispose();
            var observable = _fileIconService.GetFileIcon(Extension); // Use the injected service
            fileSubscription = observable?.Subscribe(icon => { Icon = icon; });
        });
    }

    public string Extension => Path.GetExtension(FullPath);
    public string FullPath { get; set; }

    public string Name
    {
        get => Path.GetFileName(FullPath);
        set => FullPath = Path.Combine(Path.GetDirectoryName(FullPath)!, value);
    }

    public bool LoadingFailed { get; set; }
    public DateTime LastSaveTime { get; set; }

    public IImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }
}
