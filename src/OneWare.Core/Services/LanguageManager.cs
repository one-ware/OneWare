using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OneWare.Shared;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Core.Services;

internal class LanguageManager : ILanguageManager
{
        private readonly List<ILanguageService> _singleInstanceServers = new();
        private readonly List<ILanguageService> _allServers = new();

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

        public void RegisterService(Type type, bool workspaceDependent)
        {
            if (!workspaceDependent)
            {
                if (ContainerLocator.Current.Resolve(type) is not ILanguageService service) throw new NullReferenceException(nameof(service));
                _singleInstanceServers.Add(service);
            }
        }

        public ILanguageService? GetLanguageService(IFile file) //TODO introduce projectType
        {
            var existing =
                _singleInstanceServers.FirstOrDefault(x => x.SupportedFileExtensions.Contains(file.Extension));
            
            return existing;
        }

        public void AddProject(IProjectRoot project)
        {
            //AllServers.Add(new LanguageServiceVhdl(project.ProjectPath));
            //AllServers.Add(new LanguageServiceVerilog(project.ProjectPath));
            //AllServers.Add(new LanguageServiceSystemVerilog(project.ProjectPath));
        }
        
        public void RemoveProject(IProjectRoot project)
        {
            var remove = _allServers.Where(x => x.Workspace == project.FullPath).ToArray();

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