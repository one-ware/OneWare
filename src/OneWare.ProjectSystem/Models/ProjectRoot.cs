﻿using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.ProjectSystem.Models;

public abstract class ProjectRoot : ProjectFolder, IProjectRoot
{
    private bool _isActive;

    protected ProjectRoot(string rootFolderPath, bool defaultFolderAnimation) : base(Path.GetFileName(rootFolderPath),
        null, defaultFolderAnimation)
    {
        RootFolderPath = rootFolderPath;
        TopFolder = this;
    }

    public abstract string ProjectPath { get; }
    public abstract string ProjectTypeId { get; }
    public string RootFolderPath { get; }
    public List<IProjectFile> Files { get; } = new();
    public override string FullPath => RootFolderPath;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            SetProperty(ref _isActive, value);
            FontWeight = value ? FontWeight.Bold : FontWeight.Regular;
        }
    }

    public abstract bool IsPathIncluded(string path);
    public abstract void IncludePath(string path);
    public abstract void OnExternalEntryAdded(string path, FileAttributes attributes);

    public virtual void RegisterEntry(IProjectEntry entry)
    {
        if (entry is ProjectFile file) Files.Add(file);
    }

    public virtual void UnregisterEntry(IProjectEntry entry)
    {
        if (entry is ProjectFile file) Files.Remove(file);
    }
}