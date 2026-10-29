using System;
using System.Collections.Generic;
using System.IO;
using OneWare.Essentials.Services;

namespace OneWare.Studio.Desktop.Cli;

internal sealed class StudioExternalSkillProvider : IExternalAgentSkillProvider
{
    public StudioExternalSkillProvider()
    {
    }

    public IReadOnlyCollection<string> GetSkillDirectories() =>
        [Path.Combine(AppContext.BaseDirectory, "ExternalSkills")];
}
