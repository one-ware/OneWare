using System.Globalization;
using Avalonia.Media;
using DynamicData.Binding;
using OneWare.Shared.Converters;
using OneWare.Shared.Models;

namespace OneWare.ProjectSystem.Models;

public class ProjectFile : ProjectEntry, IProjectFile
{
    public DateTime LastSaveTime { get; set; } = DateTime.MinValue;
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
    }
}