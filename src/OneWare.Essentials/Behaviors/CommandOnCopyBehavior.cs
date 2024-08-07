﻿using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OneWare.Essentials.Behaviors;

public class CommandOnCopyBehavior : CommandBasedBehavior
{
    private CompositeDisposable? Disposables { get; set; }

    protected override void OnAttached()
    {
        if (AssociatedObject == null) return;
        Disposables = new CompositeDisposable();

        base.OnAttached();

        var keymap = Application.Current!.PlatformSettings!.HotkeyConfiguration;

        Disposables.Add(AssociatedObject.AddDisposableHandler(
            InputElement.KeyDownEvent,
            (sender, e) =>
            {
                if (keymap.Copy.Any(g => g.Matches(e)))
                {
                    CommandParameter = TopLevel.GetTopLevel(AssociatedObject);
                    e.Handled = ExecuteCommand();
                }
            },
            RoutingStrategies.Tunnel));
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        Disposables?.Dispose();
    }
}