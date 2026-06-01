using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

public sealed partial class DebugVariableViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<DebugVariableViewModel> _children = new();

    [ObservableProperty]
    private string? _displayName;

    [ObservableProperty]
    private bool? _isExpanded;

    [ObservableProperty]
    private string? _typeName;

    [ObservableProperty]
    private string? _value;

    public static DebugVariableViewModel FromModel(DebugVariable variable)
    {
        var parsed = ParseValue(variable.Name, variable.Value ?? "-", true);
        if (parsed == null)
        {
            parsed = new DebugVariableViewModel
            {
                DisplayName = variable.Name,
                Value = variable.Value,
                TypeName = variable.TypeName
            };
        }

        if (!string.IsNullOrWhiteSpace(variable.TypeName) && string.IsNullOrWhiteSpace(parsed.TypeName))
            parsed.TypeName = variable.TypeName;

        if (variable.Children.Count > 0)
        {
            parsed.Children.Clear();
            foreach (var child in variable.Children)
                parsed.Children.Add(FromModel(child));
        }

        return parsed;
    }

    private static DebugVariableViewModel? ParseValue(string name, string value, bool expand = false)
    {
        var stack = new Stack<DebugVariableViewModel>();
        DebugVariableViewModel? result = null;
        var insideString = false;
        var sb = new StringBuilder();

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"' && (i == 0 || value[i - 1] != '\\'))
                insideString = !insideString;

            if (!insideString)
            {
                if (c == '{')
                {
                    var newChild = new DebugVariableViewModel();
                    if (stack.Count > 0)
                    {
                        newChild.DisplayName = sb.ToString().Split('=')[0].Trim();
                        sb.Clear();
                        stack.Peek().Children.Add(newChild);
                        if (expand)
                            stack.Peek().IsExpanded = true;
                    }
                    else
                    {
                        newChild.DisplayName = name;
                    }

                    stack.Push(newChild);
                    continue;
                }

                if (c == ',' || c == '}')
                {
                    if (sb.Length == 0 || stack.Count == 0)
                        continue;

                    var newChild = new DebugVariableViewModel();
                    stack.Peek().Children.Add(newChild);
                    if (expand)
                        stack.Peek().IsExpanded = true;

                    FillValue(newChild, sb.ToString());
                    sb.Clear();
                    if (c == '}')
                        result = stack.Pop();
                    continue;
                }
            }

            sb.Append(c);
        }

        if (sb.Length > 0)
        {
            result = new DebugVariableViewModel();
            FillValue(result, name + " = " + sb);
        }

        return result;
    }

    private static void FillValue(DebugVariableViewModel viewModel, string valueString)
    {
        var pair = valueString.Split(" = ", 2, StringSplitOptions.None);
        viewModel.DisplayName = pair[0].TrimStart();
        viewModel.Value = pair.Length > 1 ? pair[1] : null;
    }
}
