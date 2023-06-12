using System.Globalization;
using System.Runtime.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Microsoft.VisualBasic;
using OneWare.Shared;
using OneWare.Shared.Converters;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Commands;
using Prism.Ioc;

namespace OneWare.ProjectExplorer.Models;

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