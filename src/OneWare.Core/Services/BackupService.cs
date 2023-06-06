using System.Text.Json;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.ViewModels.Windows;
using OneWare.ProjectExplorer;
using Prism.Ioc;
using OneWare.Shared;
using OneWare.Shared.Enums;
using OneWare.Shared.Extensions;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

namespace OneWare.Core.Services
{
    public class BackupService
    {
        public const string KeyBackupServiceEnable = "BackupService_Enable";
        public const string KeyBackupServiceInterval = "BackupService_Interval";

        private readonly string _backupFolder;
        private readonly string _backupRegistryFile;

        private DispatcherTimer? _timer;

        private readonly IDockService _dockService;
        private readonly ILogger _logger;
        private readonly IWindowService _windowService;
        private readonly ISettingsService _settingsService;

        private List<BackupFile> _backups = new();

        public BackupService(IPaths paths, IDockService dockService, ISettingsService settingsService,
            ILogger logger, IWindowService windowService)
        {
            _dockService = dockService;
            _logger = logger;
            _windowService = windowService;
            _settingsService = settingsService;

            _backupFolder = Path.Combine(paths.AppDataDirectory, "Backups");
            _backupRegistryFile = Path.Combine(_backupFolder, "BackupRegistry.json");
        }

        public void Init()
        {
            LoadAutoSaveFile();
            _settingsService.GetSettingObservable<int>(KeyBackupServiceInterval).Subscribe(SetupTimer);
        }

        private void SetupTimer(int seconds)
        {
            _timer?.Stop();
            _timer = new DispatcherTimer(new TimeSpan(0, 0, seconds), DispatcherPriority.Normal, TimerCallback);
            _timer.Start();
        }

        private void TimerCallback(object? sender, EventArgs args)
        {
            if (_settingsService.GetSettingValue<bool>(KeyBackupServiceEnable))
            {
                Save();
            }
        }

        public void LoadAutoSaveFile()
        {
            if (File.Exists(_backupRegistryFile))
                try
                {
                    using var stream = File.OpenRead(_backupRegistryFile);
                    var backups = JsonSerializer.Deserialize<BackupFile[]>(stream);

                    if (backups != null)
                    {
                        _backups = backups.ToList();
                    }
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Warning("Loading BackupRegistry failed", e);
                }
        }

        private void SaveAutoSaveFile()
        {
            try
            {
                if (!Directory.Exists(_backupFolder)) Directory.CreateDirectory(_backupFolder);
                using var stream = File.OpenWrite(_backupRegistryFile);
                stream.SetLength(0);
                JsonSerializer.Serialize(stream, _backups.ToArray(), new JsonSerializerOptions()
                {
                    WriteIndented = true
                });
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Saving BackupRegistry failed", e);
            }
        }

        private void RemoveBackup(BackupFile backup)
        {
            try
            {
                _backups.Remove(backup);
                if (File.Exists(backup.BackupPath)) File.Delete(backup.BackupPath);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
            }
        }

        private void Save()
        {
            var closedFiles =
                _backups.Where(x => !_dockService.OpenFiles.Any(b => b.Key.FullPath.EqualPaths(x.RealPath)));

            foreach (var closedFile in closedFiles.ToList())
            {
                RemoveBackup(closedFile);
            }

            Directory.CreateDirectory(_backupFolder);

            foreach (var doc in _dockService.OpenFiles)
            {
                if (doc.Value is EditViewModel { IsDirty: true } evm)
                    try
                    {
                        var backup = _backups.FirstOrDefault(x => x.RealPath.EqualPaths(doc.Key.FullPath));

                        if (backup == null)
                        {
                            backup = new BackupFile(doc.Key.FullPath, Path.Combine(_backupFolder,
                                ProjectExplorerHelpers.CheckNameFile(Path.Combine(_backupFolder,
                                    Path.GetFileName(doc.Key.FullPath)))), DateTime.Now);
                            _backups.Add(backup);
                        }

                        if (!_backups.Contains(backup)) _backups.Add(backup);

                        Tools.WriteTextFile(backup.BackupPath, evm.CurrentDocument.Text);
                    }
                    catch (Exception e)
                    {
                        ContainerLocator.Container.Resolve<ILogger>()?.Error($"Backup failed for {doc.Value.Title}:\n{e.Message}", e);
                    }
            }

            SaveAutoSaveFile();
        }

        public void CleanUp()
        {
            foreach (var backup in _backups)
            {
                RemoveBackup(backup);
            }
            SaveAutoSaveFile();
        }

        /// <summary>
        ///     ONLY CALL AFTER FILES LOADED!
        /// </summary>
        public async Task SearchForBackupAsync(IFile file)
        {
            foreach (var backup in _backups)
                if (backup.RealPath.EqualPaths(file.FullPath))
                {
                    if (backup.SaveTime > file.LastSaveTime)
                    {
                        var dockable = await _dockService.OpenFileAsync(file);

                        if(dockable is not EditViewModel evm) continue;
                        
                        var result = await _windowService.ShowYesNoAsync("Warning",
                            $"There is a more recent version of {file.Header} stored in backups! Do you  want to restore it?", 
                            MessageBoxIcon.Warning, _dockService.GetWindowOwner(dockable));

                        if (result == MessageBoxStatus.Yes)
                            try
                            {
                                //Restoration
                                var backupText = await File.ReadAllTextAsync(backup.BackupPath);

                                evm.CurrentDocument.Text = backupText;

                                _logger.Log("File " + file.Header + " restored from backup!",
                                    ConsoleColor.Green, true, Brushes.Green);
                            }
                            catch (Exception e)
                            {
                                _logger.Error(
                                    "Restoring file " + file.Header +
                                    " failed! More information can be found in the program log", e, true, true);
                            }

                        _backups.Remove(backup);
                        SaveAutoSaveFile();
                    }

                    break;
                }
        }
    }

    public class BackupFile
    {
        public string RealPath { get; }

        public string BackupPath { get; }

        public DateTime SaveTime { get; set; }

        public BackupFile(string realPath, string backupPath, DateTime saveTime)
        {
            RealPath = realPath;
            BackupPath = backupPath;
            SaveTime = saveTime;
        }
    }
}