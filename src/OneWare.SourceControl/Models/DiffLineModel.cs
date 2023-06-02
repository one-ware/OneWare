using Avalonia.Styling;
using OneWare.SourceControl.EditorExtensions;

namespace OneWare.SourceControl.Models
{
    public enum DiffContext
    {
        Added,
        Deleted,
        Context,
        Blank
    }

    public class DiffLineModel
    {
        public string Text { get; }
        public DiffContext Style { get; }
        public string LineNumber { get; }
        public string PrefixForStyle { get; }
        public List<LineDifferenceOffset> LineDiffs { get; } = new();

        public DiffLineModel(string text, DiffContext style, string lineNumber, string prefixForStyle)
        {
            Text = text;
            Style = style;
            LineNumber = lineNumber;
            PrefixForStyle = prefixForStyle;
        }

        public static DiffLineModel CreateBlank()
        {
            return new DiffLineModel("", DiffContext.Blank, "", "");
        }

        public static DiffLineModel Create(string lineNumber, string s)
        {
            if (s.StartsWith("+"))
            {
                return new DiffLineModel(s[1..], DiffContext.Added, lineNumber, "+");
            }
            if (s.StartsWith("-"))
            {
                return new DiffLineModel(s[1..], DiffContext.Deleted, lineNumber, "-");
            }
            return new DiffLineModel(s.Length > 1 ? s.Substring(1) : s, DiffContext.Context, lineNumber, "");
        }

        public override string ToString()
        {
            return string.Format("{0}{1}", PrefixForStyle, Text);
        }
    }
}