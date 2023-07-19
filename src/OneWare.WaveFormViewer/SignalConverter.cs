using System.Text;
using OneWare.Shared.Services;
using OneWare.WaveFormViewer.Enums;
using Prism.Ioc;

namespace OneWare.WaveFormViewer;

public class SignalConverter
{
    public static string ConvertSignal(string input, SignalDataType type)
    {
        switch (type)
        {
            case SignalDataType.Decimal:
                if (input.Length > 0 && input[0] == 'b') return ConvertBinary(input[1..], 1);
                break;
            case SignalDataType.Hex:
                if (input.Length > 0 && input[0] == 'b') return "0x" + ConvertBinary(input[1..], 0);
                break;
            case SignalDataType.Ascii:
                if (input.Length > 0 && input[0] == 'b') return "\"" + ConvertBinary(input[1..], 2) + "\"";
                break;
        }

        return input;
    }
    
    public static string ConvertBinary(string val, int type)
    {
        try
        {
            var l = Convert.ToInt64(val, 2);

            if (type == 1)
            {
                val = l.ToString();
            }
            else
            {
                val = l.ToString("X");
                if (type == 2)
                {
                    var sb = new StringBuilder();

                    for (var i = 0; i < val.Length - 1; i += 2)
                    {
                        var hs = val.Substring(i, 2);
                        sb.Append(Convert.ToChar(Convert.ToUInt32(hs, 16)));
                    }

                    val = sb.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(ex.Message, ex);
            val = "";
        }

        return val;
    }
}