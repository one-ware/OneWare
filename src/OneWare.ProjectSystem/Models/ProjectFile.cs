using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ProjectSystem.Models;

public class ProjectFile : ProjectEntry, IProjectFile
{
    private readonly IFileIconService _fileIconService;
    private IDisposable? _fileSubscription;

    public ProjectFile(string header, IProjectFolder topFolder, IFileIconService fileIconService)
        : base(header, topFolder)
    {
        _fileIconService = fileIconService ?? throw new ArgumentNullException(nameof(fileIconService));

        // Listen to changes in FullPath and update the file icon when it changes
        this.WhenValueChanged(x => x.FullPath).Subscribe(x =>
        {
            _fileSubscription?.Dispose();
            var observable = _fileIconService.GetFileIcon(Extension);
            _fileSubscription = observable?.Subscribe(icon => { Icon = icon; });
        });
    }

    public DateTime LastSaveTime { get; set; } = DateTime.MinValue;
    public string Extension => Path.GetExtension(FullPath);
}
