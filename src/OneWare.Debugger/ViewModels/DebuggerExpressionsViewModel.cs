using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Models;

namespace OneWare.Debugger.ViewModels;

/// <summary>
///     Reiter "Expressions" im Debugging-Panel: benutzerdefinierte Ausdruecke und ihre
///     aufgeloesten Werte. Die Liste gehoert dem Benutzer und bleibt auch ohne laufende
///     Session bestehen.
/// </summary>
public partial class DebuggerExpressionsViewModel : ObservableObject
{
    [ObservableProperty] private ExpressionRow? _selectedExpression;

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
