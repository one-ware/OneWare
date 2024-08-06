using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Helpers;

public static class HardwareGuiCreator
{
    public static async Task<HardwareGuiViewModel> CreateGuiAsync(string guiPath, IHardwareModel hardwareModel)
    {
        var vm = new HardwareGuiViewModel();
        
        try
        {
            await using var stream = guiPath.StartsWith("avares://") ? AssetLoader.Open(new Uri(guiPath)) : File.OpenRead(guiPath);
            using var document = await JsonDocument.ParseAsync(stream);
            var gui = document.RootElement;

            vm.Width = gui.GetProperty("width").GetInt32();
            vm.Height = gui.GetProperty("height").GetInt32();
            
            if (gui.TryGetProperty("offset", out var marginProperty))
            {
                var margin = marginProperty.GetString();
                vm.Margin = Thickness.Parse(margin!);
            }

            foreach (var element in gui.GetProperty("elements").EnumerateArray())
            {
                var x = element.GetProperty("x").GetInt32();
                var y = element.GetProperty("y").GetInt32();
                var width = element.TryGetProperty("width", out var widthProperty)
                    ? widthProperty.GetInt32()
                    : 0;
                var height = element.TryGetProperty("height", out var heightProperty)
                    ? heightProperty.GetInt32()
                    : 0;
                var rotation = element.TryGetProperty("rotation", out var rotationProperty)
                    ? rotationProperty.GetDouble()
                    : 0;
                var color = element.TryGetProperty("color", out var colorProperty)
                    ? new BrushConverter().ConvertFromString(colorProperty.GetString() ?? string.Empty) as IBrush
                    : null;
                var bind = element.TryGetProperty("bind", out var bindProperty)
                    ? bindProperty.GetString()
                    : null;

                switch (element.GetProperty("type").GetString()?.ToLower())
                {
                    case "image":
                    {
                        var path = element.GetProperty("src").GetString();

                        var fullPath = Path.Combine(Path.GetDirectoryName(guiPath)!, path!);

                        IImage? image = null;

                        switch (Path.GetExtension(fullPath).ToLower())
                        {
                            case ".svg":
                                var svg = SvgSource.Load(fullPath);
                                image = new SvgImage
                                {
                                    Source = svg
                                };
                                break;
                            case ".jpg":
                            case ".png":
                                image = new Bitmap(fullPath);
                                break;
                        }

                        vm.AddElement(new FpgaGuiElementImageViewModel(x, y, width, height)
                        {
                            Rotation = rotation,
                            Image = image
                        });

                        break;
                    }
                    case "ellipse":
                    {
                        vm.AddElement(new FpgaGuiElementEllipseViewModel(x, y, width, height, color!)
                        {
                            Rotation = rotation,
                        });
                        break;
                    }
                    case "rect":
                    {
                        vm.AddElement(new FpgaGuiElementRectViewModel(x, y, width, height)
                        {
                            Color = color,
                            Rotation = rotation,
                            CornerRadius = element.TryGetProperty("cornerRadius", out var cornerRadiusProperty)
                                ? CornerRadius.Parse(cornerRadiusProperty.GetString()!)
                                : default,
                            BoxShadow = element.TryGetProperty("boxShadow", out var boxShadowProperty)
                                ? BoxShadows.Parse(boxShadowProperty.GetString()!)
                                : default
                        });
                        break;
                    }
                    case "pmod":
                    {
                        vm.AddElement(new FpgaGuiElementPmodViewModel(x, y)
                        {
                            Rotation = rotation,
                            Bind = bind,
                            Parent = hardwareModel,
                        });
                        break;
                    }
                    case "cruvi_ls":
                    {
                        vm.AddElement(new FpgaGuiElementCruviLsViewModel(x, y)
                        {
                            Rotation = rotation,
                            Bind = bind,
                            Parent = hardwareModel,
                        });
                        break;
                    }
                    case "cruvi_hs":
                    {
                        vm.AddElement(new FpgaGuiElementCruviHsViewModel(x, y)
                        {
                            Rotation = rotation,
                            Bind = bind,
                            Parent = hardwareModel
                        });
                        break;
                    }
                    case "pin":
                    {
                        color ??= Brushes.Yellow;

                        vm.AddElement(new FpgaGuiElementPinViewModel(x, y, width, height)
                        {
                            Color = color,
                            Rotation = rotation,
                            Bind = bind,
                            Parent = hardwareModel
                        });
                        break;
                    }
                    case "text":
                    {
                        var fontWeightStr = element.TryGetProperty("fontWeight", out var fontWeightProperty)
                            ? fontWeightProperty.GetString()
                            : null;

                        if (!Enum.TryParse<FontWeight>(fontWeightStr ?? "Normal", true, out var fontWeight))
                        {
                            fontWeight = FontWeight.Normal;
                        }

                        var text = element.GetProperty("text").GetString();

                        var fontSize = element.TryGetProperty("fontSize", out var fontSizeProperty)
                            ? fontSizeProperty.GetInt32()
                            : 12;

                        vm.AddElement(new FpgaGuiElementTextViewModel(x, y, text!)
                        {
                            Rotation = rotation,
                            Color = color,
                            FontWeight = fontWeight,
                            FontSize = fontSize
                        });
                        break;
                    }
                }
            }
            
            vm.Initialize();
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
        
        return vm;
    }
}