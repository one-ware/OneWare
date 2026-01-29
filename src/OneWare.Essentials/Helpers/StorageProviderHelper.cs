using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace OneWare.Essentials.Helpers;

public static class StorageProviderHelper
{
    public static async Task<string?> SelectFolderAsync(TopLevel owner, string title, string? startDir)
    {
        var startUpLocation = startDir == null
            ? null
            : await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startDir));
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = startUpLocation,
            AllowMultiple = false
        });

        if (result.Count != 1) return null;
        return result[0].TryGetLocalPath();
    }

    public static async Task<IEnumerable<string>> SelectFoldersAsync(TopLevel owner, string title, string? startDir)
    {
        var folders = new List<string>();

        var startUpLocation = startDir == null
            ? null
            : await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startDir));
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = startUpLocation,
            AllowMultiple = true
        });

        foreach (var r in result)
            if (r.TryGetLocalPath() is { } t)
                folders.Add(t);

        return folders;
    }

    [Obsolete("Use SelectSaveFileAsync with more options instead")]
    public static Task<string?> SelectSaveFileAsync(TopLevel owner, string title, string? startDir,
        string defaultExtension, params FilePickerFileType[] filters)
    {
        return SelectSaveFileAsync(owner, title, startDir, defaultExtension, null, true, filters);
    }

    public static async Task<string?> SelectSaveFileAsync(TopLevel owner, string title, string? startDir,
        string defaultExtension, string? suggestedFileName, bool showOverwritePrompt,
        params FilePickerFileType[] filters)
    {
        var startUpLocation = startDir == null
            ? null
            : await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startDir));
        var result = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedStartLocation = startUpLocation,
            FileTypeChoices = filters,
            DefaultExtension = defaultExtension,
            SuggestedFileName = suggestedFileName,
            ShowOverwritePrompt = showOverwritePrompt
        });

        if (result == null) return null;
        return result.TryGetLocalPath();
    }

    public static async Task<string?> SelectFileAsync(TopLevel owner, string title, string? startDir,
        params FilePickerFileType[]? filters)
    {
        var startUpLocation = startDir == null
            ? null
            : await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startDir));
        var result = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = startUpLocation,
            AllowMultiple = false,
            FileTypeFilter = filters
        });

        if (result.Count != 1) return null;
        return result[0].TryGetLocalPath();
    }

    public static async Task<List<string>> SelectFilesAsync(TopLevel owner, string title, string? startDir,
        params FilePickerFileType[] filters)
    {
        var files = new List<string>();
        var startUpLocation = startDir == null
            ? null
            : await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(startDir));
        var result = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = startUpLocation,
            AllowMultiple = true,
            FileTypeFilter = filters
        });

        foreach (var r in result)
            if (r.TryGetLocalPath() is { } t)
                files.Add(t);

        return files;
    }
}