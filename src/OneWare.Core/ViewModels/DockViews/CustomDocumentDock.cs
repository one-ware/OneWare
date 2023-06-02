using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Adapters;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Core;

namespace OneWare.Core.ViewModels.DockViews
{
  /// <summary>Dock base class.</summary>
  [DataContract(IsReference = true)]
  public class CustomDocumentDock : DockableBase, IDocumentDock
  {
    internal readonly INavigateAdapter _navigateAdapter;
    private IList<IDockable>? _visibleDockables;
    private IDockable? _activeDockable;
    private IDockable? _defaultDockable;
    private IDockable? _focusedDockable;
    private double _proportion = double.NaN;
    private DockMode _dock;
    private bool _isCollapsable = true;
    private bool _isActive;

    /// <summary>
    /// Initializes new instance of the <see cref="T:Dock.Model.Mvvm.Core.DockBase" /> class.
    /// </summary>
    public CustomDocumentDock()
    {
      this._navigateAdapter = (INavigateAdapter) new NavigateAdapter((IDock) this);
      this.GoBack = (ICommand) new RelayCommand((Action) (() => this._navigateAdapter.GoBack()));
      this.GoForward = (ICommand) new RelayCommand((Action) (() => this._navigateAdapter.GoForward()));
      this.Navigate = (ICommand) new RelayCommand<object>((Action<object>) (root => this._navigateAdapter.Navigate(root, true)));
      this.Close = (ICommand) new RelayCommand((Action) (() => this._navigateAdapter.Close()));
    }
    
    public IList<IDockable>? VisibleDockables
    {
      get => this._visibleDockables;
      set => this.SetProperty<IList<IDockable>>(ref this._visibleDockables, value, nameof (VisibleDockables));
    }
    
    public IDockable? ActiveDockable
    {
      get => this._activeDockable;
      set
      {
        this.SetProperty<IDockable>(ref this._activeDockable, value, nameof (ActiveDockable));
        this.Factory?.InitActiveDockable(value, (IDock) this);
        this.OnPropertyChanged("CanGoBack");
        this.OnPropertyChanged("CanGoForward");
      }
    }
    
    public IDockable? DefaultDockable
    {
      get => this._defaultDockable;
      set => this.SetProperty<IDockable>(ref this._defaultDockable, value, nameof (DefaultDockable));
    }
    
    public IDockable? FocusedDockable
    {
      get => this._focusedDockable;
      set
      {
        this.SetProperty<IDockable>(ref this._focusedDockable, value, nameof (FocusedDockable));
        this.Factory?.OnFocusedDockableChanged(value);
      }
    }

    /// <inheritdoc />
    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public double Proportion
    {
      get => this._proportion;
      set => this.SetProperty<double>(ref this._proportion, value, nameof (Proportion));
    }

    /// <inheritdoc />
    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public DockMode Dock
    {
      get => this._dock;
      set => this.SetProperty<DockMode>(ref this._dock, value, nameof (Dock));
    }

    /// <inheritdoc />
    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public bool IsActive
    {
      get => this._isActive;
      set => this.SetProperty<bool>(ref this._isActive, value, nameof (IsActive));
    }

    /// <inheritdoc />
    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public bool IsCollapsable
    {
      get => this._isCollapsable;
      set => this.SetProperty<bool>(ref this._isCollapsable, value, nameof (IsCollapsable));
    }

    /// <inheritdoc />
    [IgnoreDataMember]
    public bool CanGoBack => this._navigateAdapter.CanGoBack;

    /// <inheritdoc />
    [IgnoreDataMember]
    public bool CanGoForward => this._navigateAdapter.CanGoForward;

    /// <inheritdoc />
    [IgnoreDataMember]
    public ICommand GoBack { get; }

    /// <inheritdoc />
    [IgnoreDataMember]
    public ICommand GoForward { get; }

    /// <inheritdoc />
    [IgnoreDataMember]
    public ICommand Navigate { get; }

    /// <inheritdoc />
    [IgnoreDataMember]
    public ICommand Close { get; }
    
    private bool _canCreateDocument;

    /// <inheritdoc />
    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public bool CanCreateDocument
    {
      get => this._canCreateDocument;
      set => this.SetProperty<bool>(ref this._canCreateDocument, value, nameof (CanCreateDocument));
    }

    /// <inheritdoc />
    [IgnoreDataMember]
    public ICommand? CreateDocument { get; set; }
  }
}
