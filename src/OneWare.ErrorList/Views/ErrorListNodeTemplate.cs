using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using OneWare.ErrorList.ViewModels;

namespace OneWare.ErrorList.Views;

/// <summary>
///     Picks the cell template for file groups and problems in the description column.
/// </summary>
public class ErrorListNodeTemplate : IDataTemplate
{
    public IDataTemplate? FileTemplate { get; set; }

    [Content] public IDataTemplate? ProblemTemplate { get; set; }

    public Control? Build(object? param)
    {
        return param switch
        {
            ErrorListFileNode => FileTemplate?.Build(param),
            ErrorListProblemNode => ProblemTemplate?.Build(param),
            _ => null
        };
    }

    public bool Match(object? data)
    {
        return data is ErrorListNode;
    }
}
