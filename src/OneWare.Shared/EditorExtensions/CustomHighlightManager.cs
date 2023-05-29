using System.Text.RegularExpressions;
using AvaloniaEdit.Highlighting;
using DynamicData;

namespace OneWare.Shared.EditorExtensions
{
    public class CustomHighlightManager
    {
        private readonly Dictionary<HighlightingColor, HighlightingRule> _customRules = new();

        private readonly IHighlightingDefinition _definition;

        public CustomHighlightManager(IHighlightingDefinition highlightingDefinition)
        {
            _definition = highlightingDefinition;
        }

        /// <summary>
        ///     Sets hightlights for defined color
        /// </summary>
        public void SetHightlights(string[] words, HighlightingColor color)
        {
            if (words == null || words.Length == 0) return;
            var reg = $@"\b({string.Join("|", words)})\b";

            if (!_customRules.ContainsKey(color))
            {
                var newRule = new HighlightingRule
                {
                    Color = color
                };
                //Add key
                _definition.MainRuleSet.Rules.Add(newRule);
                _customRules.Add(color, newRule);
            }
            
            _customRules[color].Regex = new Regex(reg, RegexOptions.IgnoreCase);
        }

        public void ClearHighlights()
        {
            _definition.MainRuleSet.Rules.RemoveMany(_customRules.Select(x => x.Value));
        }
    }
}