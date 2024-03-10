using System.Globalization;
using Avalonia.Media;
using DynamicData.Binding;
using OneWare.Essentials.Converters;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.ProjectSystem.Models;

public class ProjectFile : ProjectEntry, IProjectFile
{
    public DateTime LastSaveTime { get; set; } = DateTime.MinValue;
    public string Extension => Path.GetExtension(FullPath);
    
    public ProjectFile(string header, IProjectFolder topFolder) : base(header, topFolder)
    {
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