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
        public string Text { get; set; }
        public DiffContext Style { get; set; }
        public string LineNumber { get; set; }
        public string PrefixForStyle { get; set; }
        public List<LineDifferenceOffset> LineDiffs { get; set; } = new();

        public static DiffLineModel CreateBlank()
        {
            return new DiffLineModel { Style = DiffContext.Blank, Text = "", PrefixForStyle = "", LineNumber = "" };
        }

        public static DiffLineModel Create(string lineNumber, string s)
        {
            var viewModel = new DiffLineModel();
            viewModel.LineNumber = lineNumber;

            if (s.StartsWith("+"))
            {
                viewModel.Style = DiffContext.Added;
                viewModel.PrefixForStyle = "+";
                viewModel.Text = s.Substring(1);
            }
            else if (s.StartsWith("-"))
            {
                viewModel.Style = DiffContext.Deleted;
                viewModel.PrefixForStyle = "-";
                viewModel.Text = s.Substring(1);
            }
            else
            {
                viewModel.Style = DiffContext.Context;
                viewModel.PrefixForStyle = "";
                viewModel.Text = s.Length > 1 ? s.Substring(1) : s;
            }

            if (string.IsNullOrEmpty(viewModel.Text)) viewModel.Text = "";

            return viewModel;
        }

        public override string ToString()
        {
            return string.Format("{0}{1}", PrefixForStyle, Text);
        }
    }
}