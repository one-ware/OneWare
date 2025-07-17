using System.Text.Json;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;
using OneWare.UniversalFpgaProjectSystem.Views.FpgaGuiElements;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Helpers;

public static class HardwareGuiCreator
{
    public static readonly Dictionary<string, IBrush> ColorShortcuts = new()
    {
        { "GND", Brushes.Cyan },
        { "1V2", Brushes.Yellow },
        { "3V3", Brushes.LightCoral },
        { "5V", Brushes.Red },
        { "VIN", Brushes.DarkRed },
        { "TX", Brushes.Blue },
        { "RX", Brushes.DarkRed },
    };

    public static async Task<HardwareGuiViewModel> CreateGuiAsync(string guiPath, IHardwareModel hardwareModel)
    {
        var vm = new HardwareGuiViewModel();

        try
        {
            await using var stream = guiPath.StartsWith("avares://")
                ? AssetLoader.Open(new Uri(guiPath))
                : File.OpenRead(guiPath);
            using var document = await JsonDocument.ParseAsync(stream);
            var gui = document.RootElement;

            vm.Width = gui.GetProperty("width").GetInt32();
            vm.Height = gui.GetProperty("height").GetInt32();

            if (gui.TryGetProperty("offset", out var marginProperty))
            {
                var margin = marginProperty.GetString();
                vm.Margin = Thickness.Parse(margin!);
            }

            if (gui.TryGetProperty("elements", out var elementsProperty))
                foreach (var element in elementsProperty.EnumerateArray())
                {
                    var x = element.GetProperty("x").GetDouble();
                    var y = element.GetProperty("y").GetDouble();
                    var width = element.TryGetProperty("width", out var widthProperty)
                        ? widthProperty.GetDouble()
                        : 0;
                    var height = element.TryGetProperty("height", out var heightProperty)
                        ? heightProperty.GetDouble()
                        : 0;
                    var rotation = element.TryGetProperty("rotation", out var rotationProperty)
                        ? rotationProperty.GetDouble()
                        : 0;
                    var color = element.TryGetProperty("color", out var colorProperty)
                        ? (ColorShortcuts.ContainsKey(colorProperty.GetString() ?? "")
                            ? ColorShortcuts[colorProperty.GetString()!]
                            : new BrushConverter().ConvertFromString(colorProperty.GetString() ?? string.Empty) as
                                IBrush)
                        : null;

                    var foreground = element.TryGetProperty("textColor", out var textColorProperty)
                        ? new BrushConverter().ConvertFromString(textColorProperty.GetString() ?? string.Empty) as
                            IBrush
                        : null;

                    var bind = element.TryGetProperty("bind", out var bindProperty)
                        ? bindProperty.GetString()
                        : null;

                    var connectorStyle = element.TryGetProperty("connectorStyle", out var connectorStyleProperty)
                        ? connectorStyleProperty.GetString()
                        : "default";

                    var fontSize = element.TryGetProperty("fontSize", out var fontSizeProperty)
                        ? fontSizeProperty.GetInt32()
                        : 10;

                    switch (element.GetProperty("type").GetString()?.ToLower())
                    {
                        case "gui":
                        {
                            var relativePath = element.GetProperty("src").GetString();
                            var path = Path.Combine(Path.GetDirectoryName(guiPath)!, relativePath!);
                            var child = await CreateGuiAsync(path, hardwareModel);
                            
                            vm.AddElement(new FpgaGuiElementChildViewModel(x, y)
                            {
                                Child = child,
                                Rotation = rotation
                            });
                            break;
                        }
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

                            vm.AddElement(new FpgaGuiElementImageViewModel(x, y, width == 0 ? double.NaN : width,
                                height == 0 ? double.NaN : height)
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
                            var fontWeightStr = element.TryGetProperty("fontWeight", out var fontWeightProperty)
                                ? fontWeightProperty.GetString()
                                : null;

                            if (!Enum.TryParse<FontWeight>(fontWeightStr ?? "Normal", true, out var fontWeight))
                            {
                                fontWeight = FontWeight.Normal;
                            }

                            var text = element.TryGetProperty("text", out var textProperty)
                                ? textProperty.GetString()
                                : null;

                            vm.AddElement(new FpgaGuiElementRectViewModel(x, y, width, height)
                            {
                                Color = color,
                                Rotation = rotation,
                                CornerRadius = element.TryGetProperty("cornerRadius", out var cornerRadiusProperty)
                                    ? CornerRadius.Parse(cornerRadiusProperty.GetString()!)
                                    : default,
                                BoxShadow = element.TryGetProperty("boxShadow", out var boxShadowProperty)
                                    ? BoxShadows.Parse(boxShadowProperty.GetString()!)
                                    : default,
                                FontWeight = fontWeight,
                                FontSize = fontSize,
                                Text = text,
                                Foreground = foreground
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
                                ConnectorStyle = connectorStyle
                            });
                            break;
                        }
                        case "cruvils":
                        case "cruvi_ls":
                        {
                            vm.AddElement(new FpgaGuiElementCruviLsViewModel(x, y)
                            {
                                Rotation = rotation,
                                Bind = bind,
                                Parent = hardwareModel,
                                ConnectorStyle = connectorStyle
                            });
                            break;
                        }
                        case "cruvihs":
                        case "cruvi_hs":
                        {
                            vm.AddElement(new FpgaGuiElementCruviHsViewModel(x, y)
                            {
                                Rotation = rotation,
                                Bind = bind,
                                Parent = hardwareModel,
                                ConnectorStyle = connectorStyle
                            });
                            break;
                        }
                        case "pinarray":
                        {
                            color ??= Brushes.YellowGreen;

                            var pinWidth = element.TryGetProperty("pinWidth", out var pinWidthProperty)
                                ? pinWidthProperty.GetInt32()
                                : 10;
                            var pinHeight = element.TryGetProperty("pinHeight", out var pinHeightProperty)
                                ? pinHeightProperty.GetInt32()
                                : 10;
                            
                            var labelPosition = element.TryGetProperty("labelPosition", out var labelPositionProperty)
                                ? labelPositionProperty.GetString()
                                : null;

                            Enum.TryParse<PinLabelPosition>(labelPosition, true, out var labelPositionEnum);

                            if (labelPosition == null && element.TryGetProperty("flipLabel", out var flipLabelProperty))
                            {
                                if (flipLabelProperty.GetBoolean())
                                {
                                    labelPositionEnum = PinLabelPosition.After;
                                }
                            }
                            
                            var isHorizontal = element.TryGetProperty("horizontal", out var horizontalProperty) &&
                                               horizontalProperty.GetBoolean();

                            var list = new List<FpgaGuiElementPinViewModel>();

                            foreach (var pin in element.GetProperty("pins").EnumerateArray())
                            {
                                var bindPin = pin.TryGetProperty("bind", out var bindPinProperty)
                                    ? bindPinProperty.GetString()
                                    : null;

                                var labelPin = pin.TryGetProperty("label", out var labelPinProperty)
                                    ? labelPinProperty.GetString()
                                    : null;

                                var colorPin = pin.TryGetProperty("color", out var colorPinProperty)
                                    ? (ColorShortcuts.ContainsKey(colorPinProperty.GetString() ?? "")
                                        ? ColorShortcuts[colorPinProperty.GetString()!]
                                        : new BrushConverter().ConvertFromString(colorPinProperty.GetString() ??
                                            string.Empty) as IBrush)
                                    : null;

                                list.Add(new FpgaGuiElementPinViewModel(0, 0, isHorizontal ? pinHeight : pinWidth,
                                    isHorizontal ? pinWidth : pinHeight)
                                {
                                    Color = colorPin ?? color,
                                    Bind = bindPin,
                                    Text = labelPin,
                                    LabelPosition = labelPositionEnum,
                                    Parent = hardwareModel,
                                    Foreground = foreground,
                                    FontSize = fontSize,
                                    Rotation = isHorizontal ? 90 : 0
                                });
                            }

                            vm.AddElement(new FpgaGuiElementPinArrayViewModel(x, y)
                            {
                                Parent = hardwareModel,
                                Pins = list.ToArray(),
                                Rotation = rotation,
                                IsHorizontal = isHorizontal
                            });
                            break;
                        }
                        case "pinblock":
                        {
                            color ??= Brushes.YellowGreen;

                            var pinWidth = element.TryGetProperty("pinWidth", out var pinWidthProperty)
                                ? pinWidthProperty.GetInt32()
                                : 10;
                            var pinHeight = element.TryGetProperty("pinHeight", out var pinHeightProperty)
                                ? pinHeightProperty.GetInt32()
                                : 10;

                            var list = new List<FpgaGuiElementPinViewModel>();

                            foreach (var pin in element.GetProperty("pins").EnumerateArray())
                            {
                                var bindPin = pin.TryGetProperty("bind", out var bindPinProperty)
                                    ? bindPinProperty.GetString()
                                    : null;

                                var labelPin = pin.TryGetProperty("label", out var labelPinProperty)
                                    ? labelPinProperty.GetString()
                                    : null;

                                var colorPin = pin.TryGetProperty("color", out var colorPinProperty)
                                    ? (ColorShortcuts.ContainsKey(colorPinProperty.GetString() ?? "")
                                        ? ColorShortcuts[colorPinProperty.GetString()!]
                                        : new BrushConverter().ConvertFromString(colorPinProperty.GetString() ??
                                            string.Empty) as IBrush)
                                    : null;

                                list.Add(new FpgaGuiElementPinViewModel(0, 0, pinWidth, pinHeight)
                                {
                                    Color = colorPin ?? color,
                                    Bind = bindPin,
                                    Text = labelPin,
                                    LabelPosition = PinLabelPosition.Inside,
                                    Parent = hardwareModel,
                                    Foreground = foreground,
                                    FontSize = fontSize,
                                });
                            }

                            vm.AddElement(new FpgaGuiElementPinBlockViewModel(x, y)
                            {
                                Parent = hardwareModel,
                                Pins = list.ToArray(),
                                Rotation = rotation,
                                Width = width
                            });
                            break;
                        }
                        case "pin":
                        {
                            color ??= Brushes.YellowGreen;

                            var label = element.TryGetProperty("label", out var labelProperty)
                                ? labelProperty.GetString()
                                : null;

                            var labelPosition = element.TryGetProperty("labelPosition", out var labelPositionProperty)
                                ? labelPositionProperty.GetString()
                                : null;

                            Enum.TryParse<PinLabelPosition>(labelPosition, true, out var labelPositionEnum);

                            if (labelPosition == null && element.TryGetProperty("flipLabel", out var flipLabelProperty))
                            {
                                if (flipLabelProperty.GetBoolean())
                                {
                                    labelPositionEnum = PinLabelPosition.After;
                                }
                            }

                            vm.AddElement(new FpgaGuiElementPinViewModel(x, y, width, height)
                            {
                                Color = color,
                                Rotation = rotation,
                                Bind = bind,
                                Text = label,
                                LabelPosition = labelPositionEnum,
                                Parent = hardwareModel,
                                FontSize = fontSize,
                                Foreground = foreground
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

                            vm.AddElement(new FpgaGuiElementTextViewModel(x, y)
                            {
                                Rotation = rotation,
                                FontWeight = fontWeight,
                                FontSize = fontSize,
                                Text = text,
                                Foreground = foreground
                            });
                            break;
                        }
                        case "usb":
                        {
                            var bindRx = element.TryGetProperty("rxBind", out var bindRxProperty)
                                ? bindRxProperty.GetString()
                                : null;

                            var bindTx = element.TryGetProperty("txBind", out var bindTxProperty)
                                ? bindTxProperty.GetString()
                                : null;

                            var flipLabel = element.TryGetProperty("flipLabel", out var flipLabelProperty) &&
                                            flipLabelProperty.GetBoolean();

                            vm.AddElement(new FpgaGuiElementUsbViewModel(x, y)
                            {
                                Rotation = rotation,
                                BindRx = bindRx,
                                BindTx = bindTx,
                                Parent = hardwareModel,
                                FlipLabel = flipLabel,
                            });
                            break;
                        }
                    }
                }

            vm.Initialize();
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogError(e, e.Message);
        }

        return vm;
    }
}