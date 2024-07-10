using Avalonia;
using Avalonia.LogicalTree;

namespace OneWare.Essentials.Extensions;

public static class LogicalExtensions
{
    /// <summary>
    /// Finds first data context ancestor of given type.
    /// </summary>
    /// <typeparam name="T">Datacontext type.</typeparam>
    /// <param name="logical">The logical.</param>
    /// <param name="includeSelf">If given logical should be included in search.</param>
    /// <returns>First ancestor of given type.</returns>
    public static T? FindLogicalAncestorWithDataContextType<T>(this ILogical? logical, bool includeSelf = false) where T : class
    {
        if (logical is null)
        {
            return null;
        }

        var parent = includeSelf ? logical : logical.LogicalParent;

        while (parent != null)
        {
            if (parent is StyledElement {DataContext: T dataContext})
            {
                return dataContext;
            }

            parent = parent.LogicalParent;
        }

        return null;
    }
}