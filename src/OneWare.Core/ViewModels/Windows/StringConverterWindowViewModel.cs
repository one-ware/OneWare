using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Core.ViewModels.Windows;

public class StringConverterWindowViewModel : ObservableObject
{
    private TextDocument _resultText = new("");
    private TextDocument _sourceText = new("");

    public TextDocument SourceText
    {
        get => _sourceText;
        set => SetProperty(ref _sourceText, value);
    }

    public TextDocument ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }


    public void Convert(string method)
    {
        try
        {
            var convertString = SourceText.Text;
            var returnString = "";
            var typeString = "";

            switch (method)
            {
                case "StringToHEX":

                    returnString = "x\"";
                    foreach (var c in convertString) returnString += System.Convert.ToByte(c).ToString("x2");
                    returnString += "\"";

                    typeString = convertString.Length + " chars: STD_LOGIC_VECTOR (" +
                                 (8 * convertString.Length - 1) + " downto 0)";

                    ResultText.Text = typeString + "\n\n" + returnString;
                    break;

                case "StringToROM":
                    var charNum = 0;
                    foreach (var c in convertString)
                    {
                        returnString += "                                                   " + charNum +
                                        " => x\"" + System.Convert.ToByte(c).ToString("x2") + "\",\n";
                        charNum++;
                    }

                    returnString +=
                        "                                                   others => x\"00\"\n                                               );";
                    typeString =
                        "TYPE byte_array_type IS ARRAY (natural range <>) OF STD_LOGIC_VECTOR (7 downto 0);\nCONSTANT example : byte_array_type (0 to " +
                        (charNum - 1) + ") := (\n";
                    ResultText.Text = typeString + returnString;

                    break;
                case "HEXToString":
                    if (convertString.IndexOf("x") == 0) convertString = convertString.Substring(1);
                    if (convertString.IndexOf("\"") == 0) convertString = convertString.Substring(1);
                    returnString = "";
                    var lastChar = ' ';
                    foreach (var c in convertString)
                        if (c != '\"')
                        {
                            if (lastChar != ' ')
                            {
                                returnString +=
                                    char.ConvertFromUtf32(System.Convert.ToInt32("" + lastChar + c, 16));
                                lastChar = ' ';
                            }
                            else
                            {
                                lastChar = c;
                            }
                        }

                    typeString = "String (nicht synthetisierbar)";

                    ResultText.Text = typeString + "\n\n" + returnString;

                    break;

                case "ROMToString":

                    var end = false;
                    var startChar = convertString.IndexOf("=>") + 2;
                    end |= startChar == 1;
                    var others = convertString.IndexOf("others", startChar, StringComparison.OrdinalIgnoreCase);
                    startChar = convertString.IndexOf("\"", startChar) + 1;
                    end |= startChar == 0;
                    end |= startChar > others;
                    for (; startChar < convertString.Length && convertString[startChar] < 33; startChar++)
                    {
                    }

                    end |= startChar > convertString.Length - 2;

                    returnString = "";
                    while (!end)
                    {
                        returnString += char.ConvertFromUtf32(
                            System.Convert.ToInt32("" + convertString[startChar] + convertString[startChar + 1],
                                16));

                        startChar = convertString.IndexOf("=>", startChar) + 2;
                        end |= startChar == 1;
                        others = convertString.IndexOf("others", startChar, StringComparison.OrdinalIgnoreCase);
                        startChar = convertString.IndexOf("\"", startChar) + 1;
                        end |= startChar == 0;
                        end |= startChar > others;
                        for (; startChar < convertString.Length && convertString[startChar] < 33; startChar++)
                        {
                        }

                        end |= startChar > convertString.Length - 2;
                    }

                    typeString = "String (nicht synthetisierbar)";
                    ResultText.Text = typeString + "\n\n" + returnString;

                    break;
            }
        }
        catch (Exception e)
        {
            ResultText.Text = "Error while converting \n" + e.Message;
        }
    }
}