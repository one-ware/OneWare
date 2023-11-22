using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.SDK.Converters;

namespace OneWare.SDK.Models;

public class ExternalFile : ObservableObject, IFile
{
    public string Extension => Path.GetExtension(FullPath);
    public string FullPath { get; set; }
    public string Header => Path.GetFileName(FullPath);
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
            var observable = SharedConverters.FileExtensionIconConverterObservable.Convert(Extension, typeof(IImage), null, CultureInfo.CurrentCulture) as IObservable<object?>;
            fileSubscription = observable?.Subscribe(x =>
            {
                Icon = x as IImage;
            });
        });
    }
}