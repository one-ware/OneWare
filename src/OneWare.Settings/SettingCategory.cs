namespace OneWare.Settings;

public class SettingCategory
{
    public Dictionary<string, SettingSubCategory> SettingSubCategories { get; } = new();
    public int Priority { get; set; }
    public string? IconKey { get; set; }
}

public class SettingSubCategory
{
    public List<TitledSetting> Settings { get; } = new();
    public int Priority { get; set; }
    public string? IconKey { get; set; }
}