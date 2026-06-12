using OneWare.Essentials.Models;

namespace OneWare.Essentials.Helpers;

public class ProjectSettingBuilder
{
    private Func<IProjectRootWithFile, bool>? _activationFunction;
    private string? _category;
    private int _displayOrder;
    private string? _key;
    private TitledSetting? _setting;
    private Func<IProjectRootWithFile, Task<TitledSetting>>? _factory;

    public ProjectSettingBuilder WithKey(string key)
    {
        _key = key;
        return this;
    }

    public ProjectSettingBuilder WithSetting(TitledSetting setting)
    {
        _setting = setting;
        return this;
    }

    /// <summary>
    /// Register a factory that creates a fresh <see cref="TitledSetting"/> for each project.
    /// The factory receives the project root so it can read the current value and build
    /// dynamic option lists.
    /// </summary>
    public ProjectSettingBuilder WithFactory(Func<IProjectRootWithFile, Task<TitledSetting>> factory)
    {
        _factory = factory;
        return this;
    }

    public ProjectSettingBuilder WithActivation(Func<IProjectRootWithFile, bool> activation)
    {
        _activationFunction = activation;
        return this;
    }

    public ProjectSettingBuilder WithCategory(string category)
    {
        _category = category;
        return this;
    }

    public ProjectSettingBuilder WithDisplayOrder(int displayOrder)
    {
        _displayOrder = displayOrder;
        return this;
    }

    public ProjectSetting Build()
    {
        if (_key == null) throw new InvalidOperationException("Key must be provided.");
        if (_setting == null && _factory == null)
            throw new InvalidOperationException("Either WithSetting or WithFactory must be provided.");

        _activationFunction ??= _ => true;

        if (_factory != null)
            return new ProjectSetting(_key, _factory, _activationFunction, _category, _displayOrder);

        _setting!.Priority = _displayOrder;
        return new ProjectSetting(_key, _setting, _activationFunction, _category, _displayOrder);
    }
}