using LibGit2Sharp;
using OneWare.Essentials.Models;
using OneWare.SourceControl.ViewModels;

namespace OneWare.SourceControl.Models
{
    public class SourceControlModel
    {
        public SourceControlViewModel Parent { get; }
        public StatusEntry Status { get; }
        public IProjectFile? File { get; }
        
        public SourceControlModel(SourceControlViewModel parent, StatusEntry change, IProjectFile? file = null)
        {
            Parent = parent;
            Status = change;
            File = file;
        }
    }
}