using System.ComponentModel;

namespace OneWare.Settings.ViewModels;

/// <summary>
///     Marker interface for items in the Application Settings tree that can be filtered by a search query.
/// </summary>
public interface ISearchableSettingsItem : INotifyPropertyChanged
{
    /// <summary>
    ///     Gets a value indicating whether this item (or any of its children) matches the current search query.
    /// </summary>
    bool IsVisibleBySearch { get; }

    /// <summary>
    ///     Gets or sets a value indicating whether this item should appear expanded in the tree.
    /// </summary>
    bool IsExpanded { get; set; }
}
