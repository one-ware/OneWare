using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Models;

public class ExternalFile : ObservableObject, IFile
{
    private readonly IFileIconService _fileIconService;
    private IImage? _icon;

    public ExternalFile(string fullPath, IFileIconService fileIconService)
    {
        FullPath = fullPath;

        IDisposable? fileSubscription = null;
        _fileIconService = fileIconService;

        this.WhenValueChanged(x => x.FullPath).Subscribe(x =>
        {
            fileSubscription?.Dispose();
            var observable = _fileIconService.GetFileIcon(Extension);
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