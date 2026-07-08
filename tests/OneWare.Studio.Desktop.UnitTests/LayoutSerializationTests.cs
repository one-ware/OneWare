using System;
using System.Collections.ObjectModel;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneWare.Core.Dock;
using Xunit;
namespace OneWare.Studio.Desktop.UnitTests;

/// <summary>
/// Regression tests for the corrupt-layout bug where a pinned dockable from an
/// uninstalled plugin wiped the whole layout.
///
/// Root cause: with <c>PreserveReferencesHandling</c> the first occurrence of an
/// object is written in full and later ones as <c>$ref</c>. A pinned dockable's
/// serialized <c>Owner</c> chain points up into the shared structural tree, and
/// pinned lists are serialized before <c>VisibleDockables</c>, so the entire
/// structure ended up nested inside the pinned dockable. When that dockable's type
/// could no longer be resolved, the token was skipped on read and every <c>$ref</c>
/// into the structure resolved to null — wiping the layout.
///
/// The fix: <see cref="OneWareContractResolver"/> suppresses <c>Owner</c> on write
/// (so it is never nested again), and <see cref="SilentErrorSerializationBinder"/>
/// substitutes a <see cref="MissingDockable"/> placeholder instead of null so an
/// already-saved corrupt layout still deserializes its nested structure.
/// </summary>
public class LayoutSerializationTests
{
    private static OneWareDockSerializer CreateSerializer()
    {
        // Empty provider: none of the Dock model types are DI-registered, so the
        // resolver falls back to default (parameterless) construction, which the
        // Dock.Model.Mvvm types support.
        var provider = new ServiceCollection().BuildServiceProvider();
        return new OneWareDockSerializer(provider, NullLogger.Instance);
    }

    // An old-format layout (Owner serialized) where the shared RightPane structure
    // is defined INSIDE a pinned dockable whose type no longer exists ("Ghost").
    // VisibleDockables references that same structure via $ref.
    private const string OldCorruptLayout =
        """
        {
          "$id": "1",
          "$type": "Dock.Model.Mvvm.Controls.RootDock, Dock.Model.Mvvm",
          "BottomPinnedDockables": [
            {
              "$id": "2",
              "$type": "Ghost.Removed.WizardViewModel, Ghost",
              "Id": "AIWizard",
              "Title": "AI Wizard",
              "Owner": {
                "$id": "3",
                "$type": "Dock.Model.Mvvm.Controls.ProportionalDock, Dock.Model.Mvvm",
                "Id": "RightPane",
                "Title": "RightPane",
                "Orientation": 1,
                "VisibleDockables": [
                  {
                    "$id": "4",
                    "$type": "Dock.Model.Mvvm.Controls.DocumentDock, Dock.Model.Mvvm",
                    "Id": "Documents",
                    "Title": "Documents",
                    "VisibleDockables": []
                  }
                ]
              }
            }
          ],
          "VisibleDockables": [
            { "$ref": "3" }
          ]
        }
        """;

    [Fact]
    public void Deserialize_salvages_structure_nested_inside_unresolved_pinned_dockable()
    {
        var serializer = CreateSerializer();

        var root = serializer.Deserialize<RootDock>(OldCorruptLayout);

        Assert.NotNull(root);

        // The shared RightPane structure must survive even though it was physically
        // nested inside the now-unresolvable pinned dockable.
        Assert.NotNull(root!.VisibleDockables);
        var structure = Assert.IsType<ProportionalDock>(root.VisibleDockables![0]);
        Assert.Equal("RightPane", structure.Id);
        Assert.NotNull(structure.VisibleDockables);
        Assert.Single(structure.VisibleDockables!);
        Assert.Equal("Documents", structure.VisibleDockables![0].Id);
    }

    [Fact]
    public void Deserialize_substitutes_placeholder_for_unresolved_type()
    {
        var serializer = CreateSerializer();

        var root = serializer.Deserialize<RootDock>(OldCorruptLayout);

        Assert.NotNull(root!.BottomPinnedDockables);
        Assert.Single(root.BottomPinnedDockables!);
        Assert.IsType<MissingDockable>(root.BottomPinnedDockables![0]);
    }

    [Fact]
    public void Serialize_does_not_emit_owner_back_reference()
    {
        var serializer = CreateSerializer();

        var document = new DocumentDock { Id = "Documents", Title = "Documents" };
        var structure = new ProportionalDock
        {
            Id = "RightPane",
            Title = "RightPane",
            VisibleDockables = new ObservableCollection<IDockable> { document }
        };
        document.Owner = structure; // back-reference that must NOT be serialized
        var root = new RootDock
        {
            Id = "Root",
            VisibleDockables = new ObservableCollection<IDockable> { structure }
        };
        structure.Owner = root;

        var json = serializer.Serialize(root);

        Assert.DoesNotContain("\"Owner\"", json, StringComparison.Ordinal);
    }
}
