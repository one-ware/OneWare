using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Prism.Ioc;
using OneWare.Shared;
using OneWare.Shared.Services;

namespace OneWare.Core.Data
{
    public static class IoHelpers
    {
        // http://stackoverflow.com/a/14933880/2061103
        public static async Task DeleteRecursivelyWithMagicDustAsync(string destinationDir)
        {
            const int magicDust = 10;
            for (var gnomes = 1; gnomes <= magicDust; gnomes++)
            {
                try
                {
                    Directory.Delete(destinationDir, true);
                }
                catch (DirectoryNotFoundException)
                {
                    return; // good!
                }
                catch (IOException)
                {
                    if (gnomes == magicDust) throw;
                    // System.IO.IOException: The directory is not empty
                    ContainerLocator.Container.Resolve<ILogger>()?.Log(
                        $"Gnomes prevent deletion of {destinationDir}! Applying magic dust, attempt #{gnomes}.");

                    // see http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true for more magic
                    await Task.Delay(100);
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    if (gnomes == magicDust) throw;
                    // Wait, maybe another software make us authorized a little later
                    ContainerLocator.Container.Resolve<ILogger>()?.Log(
                        $"Gnomes prevent deletion of {destinationDir}! Applying magic dust, attempt #{gnomes}.");

                    // see http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true for more magic
                    await Task.Delay(100);
                    continue;
                }

                return;
            }
            // depending on your use case, consider throwing an exception here
        }

        public static async Task BetterExtractZipToDirectoryAsync(string src, string dest)
        {
            try
            {
                ZipFile.ExtractToDirectory(src, dest);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(100);
                ZipFile.ExtractToDirectory(src, dest);
            }
        }

        public static void EnsureContainingDirectoryExists(string fileNameOrPath)
        {
            var fullPath = Path.GetFullPath(fileNameOrPath); // No matter if relative or absolute path is given to this.
            var dir = Path.GetDirectoryName(fullPath);
            if (dir == null) throw new NullReferenceException(nameof(dir));
            EnsureDirectoryExists(dir);
        }

        /// <summary>
        ///     It's like Directory.CreateDirectory, but does not fail when root is given.
        /// </summary>
        public static void EnsureDirectoryExists(string dir)
        {
            if (!string.IsNullOrWhiteSpace(dir)) // If root is given, then do not worry.
                Directory.CreateDirectory(dir); // It does not fail if it exists.
        }

        public static void OpenFolderInFileExplorer(string dirPath)
        {
            if (Directory.Exists(dirPath))
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? "explorer.exe"
                        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                            ? "open"
                            : "xdg-open",
                    Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"\"{dirPath}\"" : dirPath,
                    CreateNoWindow = true
                }))
                {
                }
        }

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (var dir in source.GetDirectories()) CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));

            foreach (var file in source.GetFiles())
            {
                file.CopyTo(Path.Combine(target.FullName, file.Name));
                Tools.ChmodFile(Path.Combine(target.FullName, file.Name));
            }
        }
    }
}