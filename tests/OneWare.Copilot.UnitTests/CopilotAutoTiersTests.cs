#pragma warning disable GHCP001 // Auto tier metadata is experimental in the SDK.
using System.Collections.Generic;
using System.Linq;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using OneWare.Copilot.Services;
using Xunit;

namespace OneWare.Copilot.UnitTests;

public sealed class CopilotAutoTiersTests
{
    private static AutoTierDescriptor Tier(string id, string type = "auto", string? displayName = null,
        bool enabled = true, bool? pickerVisible = null) => new()
    {
        Id = id,
        Type = type,
        DisplayName = displayName!,
        Description = $"{id} description",
        Status = new AutoTierStatus { Enabled = enabled, PickerVisible = pickerVisible }
    };

    [Fact]
    public void Build_WithoutMetadata_OffersTheBuiltInTiers()
    {
        Assert.Same(CopilotAutoTiers.Fallback, CopilotAutoTiers.Build(null));
        Assert.Same(CopilotAutoTiers.Fallback,
            CopilotAutoTiers.Build(new AutoTierMetadata { Tiers = new List<AutoTierDescriptor>() }));
    }

    [Fact]
    public void Fallback_UsesTheRuntimeTierIds()
    {
        var ids = CopilotAutoTiers.Fallback.Select(x => x.Id ?? "").ToArray();

        Assert.Equal(["", AutoTier.Fast.Value, AutoTier.Efficiency.Value, AutoTier.Balance.Value,
            AutoTier.Intelligence.Value], ids);
    }

    [Fact]
    public void Build_OffersEnabledAutoAndFusionTiersInProviderOrder()
    {
        var metadata = new AutoTierMetadata
        {
            DefaultTier = "balance",
            Tiers = new List<AutoTierDescriptor>
            {
                Tier("fast"),
                Tier("efficiency", displayName: "Eco"),
                Tier("balance"),
                Tier("hydrafusion", "fusion", "HydraFusion"),
                Tier("hydrafusion-max", "fusion", "HydraFusion Max", enabled: false),
                Tier("hidden", pickerVisible: false),
                Tier("other", "byok")
            }
        };

        var options = CopilotAutoTiers.Build(metadata);

        Assert.Equal(["Default", "Efficiency", "Balance", "HydraFusion"], options.Select(x => x.Label));
        Assert.Equal([false, false, false, true], options.Select(x => x.IsFusion));
        Assert.Contains("Balance", options[0].Description);
    }

    [Fact]
    public void Build_UsesTheIdWhenDisplayNamesCollide()
    {
        var options = CopilotAutoTiers.Build(new AutoTierMetadata
        {
            Tiers = new List<AutoTierDescriptor> { Tier("a", displayName: "Same"), Tier("b", displayName: "Same") }
        });

        Assert.Equal(["Default", "Same", "b"], options.Select(x => x.Label));
    }

    [Fact]
    public void ResolveAndDescribe_RoundTripLabelsAndIds()
    {
        var options = CopilotAutoTiers.Build(new AutoTierMetadata
        {
            Tiers = new List<AutoTierDescriptor> { Tier("intelligence"), Tier("hydrafusion", "fusion", "HydraFusion") }
        });

        Assert.Equal("hydrafusion", CopilotAutoTiers.ResolveId(options, "HydraFusion"));
        Assert.Null(CopilotAutoTiers.ResolveId(options, "Default"));
        Assert.Null(CopilotAutoTiers.ResolveId(options, "Unknown"));

        Assert.Equal("HydraFusion", CopilotAutoTiers.Describe(options, "hydrafusion"));
        Assert.Equal("Default", CopilotAutoTiers.Describe(options, null));
        Assert.Equal("Fast", CopilotAutoTiers.Describe(options, "fast"));
        Assert.Equal("new-tier", CopilotAutoTiers.Describe(options, "new-tier"));
    }

    [Fact]
    public void WithRuntimeFeatureFlag_KeepsFlagsThatAreAlreadyEnabled()
    {
        var environment = new Dictionary<string, string>
        {
            ["PATH"] = "/usr/bin",
            [CopilotSessionConfiguration.EnabledFeatureFlagsVariable] = "SHELL_SPAWN_BACKEND, dynamic_auto_tiers"
        };

        var result = CopilotSessionConfiguration.WithRuntimeFeatureFlag(environment,
            CopilotSessionConfiguration.DynamicAutoTiersFeatureFlag, false);

        Assert.Equal("/usr/bin", result["PATH"]);
        Assert.Equal("SHELL_SPAWN_BACKEND,dynamic_auto_tiers",
            result[CopilotSessionConfiguration.EnabledFeatureFlagsVariable]);

        var added = CopilotSessionConfiguration.WithRuntimeFeatureFlag(new Dictionary<string, string>(),
            CopilotSessionConfiguration.DynamicAutoTiersFeatureFlag, false);
        Assert.Equal("DYNAMIC_AUTO_TIERS", added[CopilotSessionConfiguration.EnabledFeatureFlagsVariable]);
    }

    [Fact]
    public void ApplyFusion_EnablesTheHydraFusionSessionFlags()
    {
        var config = new SessionConfig { FeatureFlags = new Dictionary<string, bool> { ["OTHER"] = true } };

        CopilotSessionConfiguration.ApplyFusion(config);

        Assert.True(config.FeatureFlags["OTHER"]);
        Assert.All(CopilotSessionConfiguration.FusionFeatureFlags, x => Assert.True(config.FeatureFlags[x]));
    }
}
