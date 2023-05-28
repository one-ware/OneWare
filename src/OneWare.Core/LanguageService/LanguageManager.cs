using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OneWare.Shared;
using OneWare.Shared.Services;

namespace OneWare.Core.LanguageService;

internal class LanguageManager : ILanguageManager
{
        private readonly List<LanguageServiceBase> _singleInstanceServers = new();
        private readonly List<LanguageServiceBase> _allServers = new();

        static LanguageManager()
        {
            //AllServers.Add(new LanguageServiceCpp(App.Paths.ProjectsDirectory));
            //AllServers.Add(new LanguageServicePython(App.Paths.ProjectsDirectory));
            //AllServers.Add(new LanguageServiceHdp(App.Paths.ProjectsDirectory));
        }

        public IEnumerable<T> GetServices<T>()
        {
            return _allServers.Where(x => x is T).Cast<T>();
        }

        public void RegisterService<T>(bool workspaceDependent)
        {
            //if(!workspaceDependent) SingleInstanceServers.Add(new Language);
        }

        public LanguageServiceBase? GetLanguageService(IFile file) //TODO introduce projectType
        {
            var existing =
                _singleInstanceServers.FirstOrDefault(x => x.SupportedFileExtensions.Contains(file.Extension));
            
            
            if (existing != null) return existing;
            return null;
        }

        public void AddProject(IProjectRoot project)
        {
            //AllServers.Add(new LanguageServiceVhdl(project.ProjectPath));
            //AllServers.Add(new LanguageServiceVerilog(project.ProjectPath));
            //AllServers.Add(new LanguageServiceSystemVerilog(project.ProjectPath));
        }
        
        public void RemoveProject(IProjectRoot project)
        {
            var remove = _allServers.Where(x => x.WorkspaceDependent && x.Workspace == project.FullPath).ToArray();

            foreach (var r in remove)
            {
                _allServers.Remove(r);
                _ = r.DeactivateAsync();
            }
        }
        
        public async Task CleanResourcesAsync()
        {
            await Task.WhenAll(_allServers.Select(x => x.DeactivateAsync()));
        }
}