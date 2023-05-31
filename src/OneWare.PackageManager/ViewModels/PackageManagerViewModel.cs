using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.PackageManager.Models;
using Prism.Ioc;
using ReactiveUI;
using OneWare.PackageManager.Views;
using VHDPlus.Shared;
using VHDPlus.Shared.Services;

namespace OneWare.PackageManager.ViewModels
{
    public class PackageManagerViewModel : ReactiveObject
    {
        private static HttpClient _httpClient;

        public readonly string PackageDatabasePath;

        public readonly IdeUpdateManagerModel VhdPlusUpdaterModel;

        private string _searchText;

        private PackageBase _selectedPackage;
        
        public ObservableCollection<PackageBase> UpdatePackages { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set => this.RaiseAndSetIfChanged(ref _searchText, value);
        }

        public PackageBase SelectedPackage
        {
            get => _selectedPackage;
            set => this.RaiseAndSetIfChanged(ref _selectedPackage, value);
        }

        private IPaths _paths;
        public PackageManagerViewModel(IPaths paths)
        {
            _paths = paths;
            
            PackageDatabasePath = Path.Combine(paths.PackagesDirectory, "packages.json");
            VhdPlusUpdaterModel = new IdeUpdateManagerModel();
            
            Init();
        }

        public void Init()
        {
            UpdatePackages.Clear();
            
            // if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) UpdatePackages.Add(VhdPlusUpdaterModel);
            //
            // var vhdPlusLibrariesPackage = new LibraryPackage
            // {
            //     PackageHeader = "VHDPlus Libraries",
            //     PackageDescription = "Official VHDPlus Libraries",
            //     Icon = Application.Current.FindResource("VsImageLib.Folder16X") as IImage,
            //     License = "MIT",
            //     LicenseUrl = "https://github.com/leonbeier/VHDPlus_Libraries_and_Examples/blob/master/LICENSE",
            //     PackageName = "vhdpluslibraries",
            //     CustomDestinationFolder = Path.Combine(_paths.LibrariesDirectory, "VHDPlus Libraries")
            // };
            //
            // UpdatePackages.Add(vhdPlusLibrariesPackage);
            //
            // var arrowUsbPackageWindows = new ZipInstallerPackage
            // {
            //     PackageHeader = "Arrow USB Programmer",
            //     PackageDescription =
            //         "One time installation of Quartus support libraries is needed. There are no custom USB drivers needed, WHQL certified Drivers from FTDI Chip are used.",
            //     Icon =  Application.Current.FindResource("BoxIcons.RegularUsb") as IImage,
            //     License = "Arrow USB Blaster DLL End User License Agreement",
            //     LicenseUrl = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "PackageLicenses",
            //         "ArrowUSBProgrammerLicense.txt"),
            //     PackageName = "arrowusbprogrammer"
            // };
            //
            // var arrowUsbPackageLinux = new ZipScriptPackage
            // {
            //     PackageHeader = "Arrow USB Programmer",
            //     PackageDescription =
            //         "One time installation of Quartus support libraries is needed. There are no custom USB drivers needed, WHQL certified Drivers from FTDI Chip are used.",
            //     Icon = Application.Current.FindResource("BoxIcons.RegularUsb") as IImage,
            //     License = "Arrow USB Blaster DLL End User License Agreement",
            //     LicenseUrl = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "PackageLicenses",
            //         "ArrowUSBProgrammerLicense.txt"),
            //     PackageName = "arrowusbprogrammer",
            //     ScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "scripts",
            //         "installArrowUSB.sh"),
            //     WarningText = "Quartus needs to be installed first!"
            // };
            //
            // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //     UpdatePackages.Add(arrowUsbPackageWindows);
            //
            // else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            //     UpdatePackages.Add(arrowUsbPackageLinux);
            //
            // var cppLs = new LanguageServerPackage<LanguageServiceCpp>
            // {
            //     PackageHeader = "C++ Language Support",
            //     PackageDescription =
            //         "clangd understands your C++ code and adds smart features to your editor: code completion, compile errors, go-to-definition and more.",
            //     Icon = Application.Current.FindResource("VsImageLib.Cpp16X") as IImage,
            //     License = "Apache 2.0",
            //     LicenseUrl = "https://github.com/clangd/clangd/blob/master/LICENSE",
            //     PackageName = "clangd",
            //     PathSetter = ExpressionUtils.CreateSetter<OldSettings, string>(x => x.CppLspPath),
            //     LspActivator = ExpressionUtils.CreateSetter<OldSettings, bool>(x => x.CppLspActivated),
            //     LspValid = () => Tools.Exists(Global.Options.CppLspPath),
            // };
            // UpdatePackages.Add(cppLs);
            //
            // UpdatePackages.Add(new LanguageServerPackage<LanguageServiceVhdl>
            // {
            //     PackageHeader = "VHDL Language Support",
            //     PackageDescription =
            //         "Analyses your vhdl code and adds smart features to VHDPlus IDE: syntax errors, find references and more.",
            //     Icon = Application.Current.FindResource("VhdlFileIcon") as IImage,
            //     License = "Mozilla Public License 2.0",
            //     LicenseUrl = "https://github.com/kraigher/rust_hdl/blob/master/LICENSE.txt",
            //     PackageName = "rusthdl",
            //     PathSetter = ExpressionUtils.CreateSetter<OldSettings, string>(x => x.VhdlLspPath),
            //     LspActivator = ExpressionUtils.CreateSetter<OldSettings, bool>(x => x.VhdlLspActivated),
            //     LspValid = () => Tools.Exists(Global.Options.VhdlLspPath),
            // });
            //
            // UpdatePackages.Add(new LanguageServerPackage<LanguageServiceVerilog>
            // {
            //     PackageHeader = "Verilog/SystemVerilog Language Support",
            //     PackageDescription =
            //         "Analyses your verilog code and adds smart features to VHDPlus IDE: syntax errors, find references and more.",
            //     Icon = Application.Current.FindResource("VerilogFileIcon") as IImage,
            //     License = "Apache 2.0",
            //     LicenseUrl = "https://github.com/chipsalliance/verible/blob/master/LICENSE",
            //     PackageName = "verible",
            //     PathSetter = ExpressionUtils.CreateSetter<OldSettings, string>(x => x.VerilogLspPath),
            //     LspActivator = ExpressionUtils.CreateSetter<OldSettings, bool>(x => x.VerilogLspActivated),
            //     LspValid = () => Tools.Exists(Global.Options.VerilogLspPath),
            // });
            //
            // UpdatePackages.Add(new LanguageServerPackage<LanguageServiceSystemVerilog>
            // {
            //     PackageHeader = "SystemVerilog Language Support",
            //     PackageDescription =
            //         "Analyses your system-verilog code and adds smart features to VHDPlus IDE: syntax errors, find references and more.",
            //     Icon = Application.Current.FindResource("SystemVerilogFileIcon") as IImage,
            //     License = "MIT",
            //     LicenseUrl = "https://github.com/dalance/svls/blob/master/LICENSE",
            //     PackageName = "svls",
            //     PathSetter = ExpressionUtils.CreateSetter<OldSettings, string>(x => x.SystemVerilogLspPath),
            //     LspActivator = ExpressionUtils.CreateSetter<OldSettings, bool>(x => x.SystemVerilogLspActivated),
            //     LspValid = () => Tools.Exists(Global.Options.SystemVerilogLspPath),
            // });
            //
            // UpdatePackages.Add(new SetPathPackage
            // {
            //     PackageHeader = "GTKWave",
            //     PackageDescription =
            //         "GTKWave is a fully featured GTK+ based wave viewer for Unix and Win32 which reads LXT, LXT2, VZT, FST, and GHW files as well as standard Verilog VCD/EVCD files and allows their viewing.",
            //     Icon = Application.Current.FindResource("Material.Pulse") as IImage,
            //     License = "GPL-2.0",
            //     LicenseUrl = "https://github.com/gtkwave/gtkwave/blob/master/LICENSE",
            //     PackageName = "gtkwave",
            //     PathSetter = ExpressionUtils.CreateSetter<OldSettings, string>(x => x.GtkWavePath),
            //     AfterDownload = (() =>
            //     {
            //         Global.Options.UseGtkWave = true;
            //         Global.Options.GhdlUseGhw = true;
            //     })
            // });
            //
            // UpdatePackages.Add(new SetPathPackage
            // {
            //     PackageHeader = "GHDL",
            //     PackageDescription =
            //         "GHDL is an open-source simulator for the VHDL language. GHDL allows you to compile and execute your VHDL code directly in your PC.",
            //     Icon = Application.Current.FindResource("Material.Pulse") as IImage,
            //     License = "GPL-2.0",
            //     LicenseUrl = "https://github.com/ghdl/ghdl/blob/master/COPYING.md",
            //     PackageName = "ghdl",
            //     PathSetter = ExpressionUtils.CreateSetter<OldSettings, string>(x => x.GhdlPath)
            // });
            //
            // var arduinoCli = new SetPathPackage
            // {
            //     PackageHeader = "Arduino CLI",
            //     PackageDescription =
            //         "Allows programming your Arduino compatible microcontrollers conveniently inside VHDPlus IDE",
            //     Icon = Application.Current.FindResource("SimpleIcons.Arduino") as IImage,
            //     License = "GNU GENERAL PUBLIC LICENSE",
            //     LicenseUrl = "https://github.com/arduino/arduino-cli/blob/master/LICENSE.txt",
            //     PackageName = "arduinocli",
            //     PathSetter = ExpressionUtils.CreateSetter<OldSettings, string>(x => x.ArduinoCliPath)
            // };
            // UpdatePackages.Add(arduinoCli);
            //
            // var arduinoLs = new LanguageServerPackage<LanguageServiceArduino>
            // {
            //     PackageHeader = "Arduino Language Support",
            //     PackageDescription =
            //         "Analyses your arduino code and adds smart features to VHDPlus IDE: syntax errors, find references and more.",
            //     Icon = Application.Current.FindResource("SimpleIcons.Arduino") as IImage,
            //     License = "Apache 2.0",
            //     LicenseUrl = "https://github.com/arduino/arduino-language-server/blob/main/LICENSE.txt",
            //     PackageName = "arduinols",
            //     PathSetter = ExpressionUtils.CreateSetter<OldSettings, string>(x => x.ArduinoLsPath),
            //     LspActivator = ExpressionUtils.CreateSetter<OldSettings, bool>(x => x.ArduinoLspActivated),
            //     LspValid = () => Tools.Exists(Global.Options.ArduinoLsPath),
            //     Requirements = new List<PackageBase>(){cppLs, arduinoCli}
            // };
            // UpdatePackages.Add(arduinoLs);

            LoadPackageDatabase();
        }

        public void Initialize()
        {
            _httpClient = new HttpClient();
            foreach (var package in UpdatePackages) package.Initialize(_httpClient);
        }

        public async Task CheckForUpdateAsync()
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Log("Checking for updates...", ConsoleColor.DarkGray);

            var ideUpdate = !RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                            await VhdPlusUpdaterModel.CheckForUpdateAsync();
            /*if (ideUpdate)
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var model = new CustomNotificationViewModel
                    {
                        Title = "New update for VHDPlus IDE available!\n" + VhdPlusUpdaterModel.NewVersion +
                                " is ready for download!",
                        ButtonText = "Launch Updater",
                        Image = App.Current.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage
                    };
                    model.ButtonClicked += ButtonClicked;

                    void ButtonClicked(object? sender, EventArgs e)
                    {
                        var updater = new PackageManagerWindow();
                        updater.Show();
                        model.ButtonClicked -= ButtonClicked;
                    }

                    Global.NotificationManager.Show(model);
                }, DispatcherPriority.Background);

            if (!ideUpdate) ContainerLocator.Container.Resolve<ILogger>()?.Log("No IDE update found!", ConsoleColor.Gray);*/

            var packageUpdate = false;
            foreach (var package in UpdatePackages)
            {
                if (package.PackageName == "vhdplus") continue;
                var up = await package.CheckForUpdateAsync();
                if (up) packageUpdate = true;
            }

            /*if (packageUpdate)
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var model = new CustomNotificationViewModel
                    {
                        Title = "New package updates available!", ButtonText = "Launch Package Manager",
                        Image = App.Current.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage
                    };
                    model.ButtonClicked += ButtonClicked;

                    void ButtonClicked(object? sender, EventArgs e)
                    {
                        var updater = new PackageManagerWindow();
                        updater.Show();
                        model.ButtonClicked -= ButtonClicked;
                    }

                    Global.NotificationManager.Show(model);
                    //Global.MainWindowViewModel.UpdateAvailable = true;
                }, DispatcherPriority.Background);

            if (string.IsNullOrEmpty(UpdatePackages.FirstOrDefault(x => x.PackageName == "vhdpluslibraries")?
                    .InstalledVersion))
            {
                if (!Directory.Exists(Path.Combine(Global.LibrariesDirectory, "VHDPlus Libraries")))
                {
                    var model = new CustomNotificationViewModel
                    {
                        Title = "Official Libraries available!",
                        Message = "VHDPlus Libraries offer support for Hardware, easy NIOS integration and much more.",
                        ButtonText = "Install", Image = App.Current.FindResource("VsImageLib2019.StatusUpdateGrey16X") as IImage
                    };
                    model.ButtonClicked += ButtonClicked;

                    void ButtonClicked(object? sender, EventArgs e)
                    {
                        var updater = new PackageManagerWindow();
                        updater.Show();
                        Global.PackageManagerViewModel.SelectedPackage =
                            Global.PackageManagerViewModel.UpdatePackages.FirstOrDefault(x => x.PackageName == "vhdpluslibraries");
                        model.ButtonClicked -= ButtonClicked;
                    }

                    await Dispatcher.UIThread.InvokeAsync(() => { Global.NotificationManager.Show(model); });
                }
            }

            if (!packageUpdate) ContainerLocator.Container.Resolve<ILogger>()?.Log("No package update found!", ConsoleColor.Gray);*/
        }

        public void SavePackageDatabase(string? path = null)
        {
            path ??= PackageDatabasePath;

            var database = UpdatePackages.Where(x => x.PackageName != "vhdplus" && x.InstalledVersion != null)
                .Select(x => new PackageDatabase
                {
                    PackageName = x.PackageName, Version = x.InstalledVersion, EntryPoint = x.EntryPoint
                })
                .ToArray();

            var output = JsonSerializer.Serialize(database, database.GetType(), new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });

            try
            {
                File.WriteAllText(path, output);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Saving package database failed!\n" + e.Message, e);
            }
        }

        public void LoadPackageDatabase(string? path = null)
        {
            path ??= PackageDatabasePath;
            try
            {
                if (File.Exists(path))
                {
                    var file = File.ReadAllText(path);
                    var database = JsonSerializer.Deserialize<PackageDatabase[]>(file);

                    if (database == null) return;
                    
                    foreach (var package in database)
                    foreach (var up in UpdatePackages)
                        if (up.PackageName == package.PackageName)
                        {
                            up.InstalledVersion = package.Version;
                            up.EntryPoint = package.EntryPoint;
                            up.UpdateStatus = UpdateStatus.Installed;
                        }
                }
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Loading package database failed\n" + e.Message, e);
            }
        }

        public void OnSearch()
        {
            var searchResult = UpdatePackages.FirstOrDefault(x =>
                x.PackageHeader.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            SelectedPackage = searchResult;
        }
    }
}