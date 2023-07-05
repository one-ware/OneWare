using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using OneWare.Shared;
using OneWare.Shared.Converters;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.ProjectSystem.Models;

public class ProjectFile : ProjectEntry, IProjectFile
{
    public string Extension => Path.GetExtension(FullPath);
    
    public ProjectFile(string header, IProjectFolder topFolder) : base(header, topFolder)
    {
        IDisposable? fileSubscription = null;

        this.WhenValueChanged(x => x.Header).Subscribe(x =>
        {
            fileSubscription?.Dispose();
            var observable = SharedConverters.FileExtensionIconConverterObservable.Convert(Extension, typeof(IImage), null, CultureInfo.CurrentCulture) as IObservable<object?>;
            fileSubscription = observable?.Subscribe(x =>
            {
                Icon = x as IImage;
            });
        });

        DoubleTabCommand =
            new RelayCommand(() => ContainerLocator.Container.Resolve<IDockService>().OpenFileAsync(this));
    }
}