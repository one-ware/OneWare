using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Rechtes Panel: benutzerdefinierte Ausdruecke und ihre aufgeloesten Werte, benannt nach
///     der gleichnamigen Eclipse-Ansicht. Die Liste gehoert dem Benutzer und bleibt auch ohne
///     laufende Session bestehen.
/// </summary>
public partial class DebuggerExpressionsViewModel : ExtendedTool
{
    public const string IconKey = "VsCodeLight.Debug";

    [ObservableProperty] private ExpressionRow? _selectedExpression;

    public DebuggerExpressionsViewModel() : base(IconKey)
    {
        Id = "DebuggerExpressions";
        Title = "Expressions";
    }

    public ObservableCollection<ExpressionRow> Expressions { get; } = [];

    [RelayCommand]
    private void AddExpression()
    {
        var row = new ExpressionRow();
        Expressions.Add(row);
        SelectedExpression = row;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveExpression))]
    private void RemoveExpression()
    {
        if (SelectedExpression is null) return;
        Expressions.Remove(SelectedExpression);
        SelectedExpression = null;
    }

    private bool CanRemoveExpression()
    {
        return SelectedExpression is not null;
    }

    partial void OnSelectedExpressionChanged(ExpressionRow? value)
    {
        RemoveExpressionCommand.NotifyCanExecuteChanged();
    }
}
