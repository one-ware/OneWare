using OneWare.Essentials.Models;

namespace OneWare.Essentials.Helpers;

public class ProjectSettingBuilder
{
    private string? _key;
    private TitledSetting? _setting;
    private Func<IProjectRootWithFile, bool>? _activationFunction;
    private string? _category;
    private int _displayOrder = 0;

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
        if (_setting == null) throw new InvalidOperationException("Setting must be provided.");
        
        _setting.Priority = _displayOrder;
        _activationFunction ??= _ => true;

        return new ProjectSetting(
            _key,
            _setting,
            _activationFunction,
            _category,
            _displayOrder
        );
    }
}
