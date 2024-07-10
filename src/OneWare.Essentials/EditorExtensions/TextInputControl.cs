using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace OneWare.Essentials.EditorExtensions
{
    public class TextInputControl : TemplatedControl
    {
        private readonly string _initValue;

        public TextInputControl(string initValue)
        {
            this._initValue = initValue;
            this.InitializeIfNeeded();
        }

        public TextBox? Input { get; private set; }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            var input = e.NameScope.Find<TextBox>("inputBox");
            Input = input ?? throw new NullReferenceException(nameof(input));
            Input.Text = _initValue;
            Input.Focus();
            Input.SelectAll();
        }
    }
}