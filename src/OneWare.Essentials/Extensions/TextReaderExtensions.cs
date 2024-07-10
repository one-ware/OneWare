using System.Text;

namespace OneWare.Essentials.Extensions;

public static class TextReaderExtensions
{
    public static long ProcessWords(this TextReader reader, int maxLength, Func<string, bool> processWord)
    {
        var wordBuilder = new StringBuilder();
        
        //read Definition
        for (var i = 0; i < maxLength; i++)
        {
            var ci = reader.Read();
            if (ci == -1) return i; //TODO send last word
            
            var character = (char)ci;
            if (character is not (' ' or '\n' or '\r')) wordBuilder.Append(character);
            else
            {
                //Process word
                if(!processWord.Invoke(wordBuilder.ToString())) return i;

                wordBuilder.Clear();
            }
        }

        return maxLength - 1;
    }
}