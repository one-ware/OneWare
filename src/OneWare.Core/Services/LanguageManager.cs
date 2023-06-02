using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using OneWare.Shared;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Core.Services;

internal class LanguageManager : ILanguageManager
{
        private readonly Dictionary<string, Type> _singleInstanceServerTypes = new();
        private readonly Dictionary<string, Type> _workspaceServerTypes = new();
        private readonly Dictionary<Type, ILanguageService> _singleInstanceServers = new();
        private readonly Dictionary<Type, Dictionary<string,ILanguageService>> _workspaceServers = new();

        private readonly Dictionary<string, string> _highlightingDefinitions = new();

        public void RegisterHighlighting(string path, params string[] supportedFileTypes)
        {
            foreach (var fileType in supportedFileTypes)
            {
                _highlightingDefinitions[fileType] = path;
            }
        }

        public IHighlightingDefinition? GetHighlighting(string fileExtension)
        {
            _highlightingDefinitions.TryGetValue(fileExtension, out var path);

            if (path == null) return null;

            try
            {
                using var s = new StreamReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
                using var reader = new XmlTextReader(s);
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
                return null;
            }
        }
        
        public void RegisterService(Type type, bool workspaceDependent, params string[] supportedFileTypes)
        {
            foreach (var s in supportedFileTypes)
            {
                if (!workspaceDependent) _singleInstanceServerTypes[s] = type;
                else
                {
                    _workspaceServerTypes[s] = type;
                    _workspaceServers.TryAdd(type, new Dictionary<string, ILanguageService>());
                }
            }
        }

        public ILanguageService? GetLanguageService(IFile file)
        {
            if (_workspaceServerTypes.TryGetValue(file.Extension, out var type2))
            {
                var workspace = (file is IProjectFile pf ? pf.Root.RootFolderPath : Path.GetDirectoryName(file.FullPath)) ?? "";
                if (_workspaceServers[type2].TryGetValue(workspace, out var service2)) return service2;
                if (ContainerLocator.Container.Resolve(type2, (typeof(string), workspace)) is not
                    ILanguageService newInstance)
                    throw new TypeLoadException(nameof(type2) + " is not " + nameof(ILanguageService));
                
                _workspaceServers[type2].Add(workspace, newInstance);
                return newInstance;
            }
            
            if (_singleInstanceServerTypes.TryGetValue(file.Extension, out var type))
            {
                if (_singleInstanceServers.TryGetValue(type, out var service)) return service;
                if (ContainerLocator.Container.Resolve(type) is not ILanguageService newInstance)
                    throw new TypeLoadException(nameof(type2) + " is not " + nameof(ILanguageService));
                
                _singleInstanceServers.Add(type, newInstance);
                return newInstance;
            }

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
            // var remove = _workspaceServers.SelectMany(x => x.Value.Where(x => x.Key == project.RootFolderPath));
            //
            // foreach (var r in remove)
            // {
            //     _workspaceServers.Remove(r.Key);
            //     _ = r.Value.DeactivateAsync();
            // }
        }
        
        public async Task CleanResourcesAsync()
        {
            await Task.WhenAll(_singleInstanceServers.Select(x => x.Value.DeactivateAsync()));
            await Task.WhenAll(_workspaceServers
                .SelectMany(x => x.Value.Select(b => b.Value.DeactivateAsync())));
        }
}