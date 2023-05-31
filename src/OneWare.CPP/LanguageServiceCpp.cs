using System.Diagnostics;
using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Shared;
using OneWare.Shared.Extensions;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Cpp
{
    public class LanguageServiceCpp : LanguageServiceBase, ILanguageService
    {
        public LanguageServiceCpp() : base("CPP LS")
        {
            // Global.Options.WhenAnyValue(x => x.CppLspNiosMode).Subscribe(x =>
            // {
            //     if (IsActivated) _ = RestartAsync();
            // });
            //
            // Global.Options.WhenAnyValue(x => x.CppLspActivated).Subscribe(x =>
            // {
            //     //Check if file is open
            //     var anyFile = MainDock.OpenDocuments.Any(
            //         keyValuePair => SupportedFileTypes.Contains(keyValuePair.Key.Type) && Path.GetFullPath(keyValuePair.Key.FullPath).StartsWith(Path.GetFullPath(workspace)));
            //     if (anyFile && x && !IsActivated) _ = ActivateAsync();
            //     else if (!x && IsActivated) _ = DeactivateAsync();
            // });
        }

        public ITypeAssistance GetTypeAssistance(IEditor editor)
        {
            return new TypeAssistanceCpp(editor, this);
        }

        public override async Task ActivateAsync()
        {
            if (IsActivated) return;
            IsActivated = true;

            var cppPath = @"C:\Users\Hendrik\VHDPlus\Packages\clangd\clangd_15.0.1\bin\clangd.exe";
            
            // var minGw = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            //     ? "H-x86_64-mingw32"
            //     : "H-x86_64-pc-linux-gnu";
            //
            // if (!Tools.Exists(Global.Options.CppLspPath))
            // {
            //     ContainerLocator.Container.Resolve<ILogger>()?.Error("C++ language server not found!", null, false);
            //     return;
            // }
            //
            // var queryDriverPath =
            //     $"{Path.Combine(Path.GetDirectoryName(Global.Options.QuartusPath) ?? "").ToLinuxPath()}/nios2eds/bin/gnu/{minGw}/bin/";
            // var queryDriver = queryDriverPath + "nios2-elf*";
            //
            // var arguments = (Global.Options.CppLspNiosMode ? $"--query-driver={queryDriver}" : "") + " --log=error";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = cppPath,
                Arguments = "--log=error",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            process.ErrorDataReceived += (o, i) => ContainerLocator.Container.Resolve<ILogger>()?.Log("C++ LS: " + i.Data, ConsoleColor.Yellow);
            try
            {
                process.Start();
                process.BeginErrorReadLine();
                await InitAsync(process, x =>
                {
                    x.WithInitializationOptions(new CustomCppInitialisationOptions
                    {
                        FallbackFlags = new Container<string>
                        (
                            ""
                        )
                    });
                });

            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }
    }

    public class CustomCppInitialisationOptions
    {
        public Container<string> FallbackFlags { get; set; }

        public string CompilationDatabasePath { get; set; }
    }
}