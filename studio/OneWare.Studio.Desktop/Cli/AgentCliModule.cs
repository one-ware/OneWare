using System;
using System.Collections.Generic;
using System.CommandLine;
using OneWare.Essentials.Services;

namespace OneWare.Studio.Desktop.Cli;

internal sealed class AgentCliModule(CliModuleLoader loader, IPaths paths, string? homeDirectory = null) : OneWareCliModuleBase
{
    public override IReadOnlyList<Command> RegisterCommands(IServiceProvider serviceProvider)
    {
        var agent = new Command("agent", "Manage skills for external coding agents");
        var init = new Command("init", "Synchronize extension skills for external agents");
        init.SetAction(_ =>
        {
            try
            {
                var synchronizer = new ExternalSkillSynchronizer(
                    paths.DocumentsDirectory, paths.PluginsDirectory, paths.PackagesDirectory,
                    paths.AppName, homeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Console.Out, Console.Error);
                return synchronizer.Synchronize(loader.GetExternalSkillProviders());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unable to synchronize external agent skills: {ex.Message}");
                return 1;
            }
        });
        agent.Subcommands.Add(init);
        return [agent];
    }
}

internal sealed record ExternalSkillProviderSource(
    string PluginPath, IExternalAgentSkillProvider Provider, string? BuiltInId = null, string? Version = null);
