using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.Essentials.Converters;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.Essentials.Models;

public class ExternalFile : ObservableObject, IFile
{
    public string Extension => Path.GetExtension(FullPath);
    public string FullPath { get; set; }

    public string Name
    {
        get => Path.GetFileName(FullPath);
        set => FullPath = Path.Combine(Path.GetDirectoryName(FullPath)!, value);
    }
    
    public bool LoadingFailed { get; set; }
    public DateTime LastSaveTime { get; set; }
    
    private IImage? _icon;

    public IImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public ExternalFile(string fullPath)
    {
        FullPath = fullPath;
        
        IDisposable? fileSubscription = null;
        
        this.WhenValueChanged(x => x.FullPath).Subscribe(x =>
        {
            fileSubscription?.Dispose();
            var observable = ContainerLocator.Container.Resolve<IFileIconService>().GetFileIcon(Extension);
            fileSubscription = observable?.Subscribe(icon =>
            {
                Icon = icon;
            });
        });
    }
}