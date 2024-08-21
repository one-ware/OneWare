using OneWare.Essentials.Models;

namespace OneWare.Settings;

public class SettingCategory
{
    public Dictionary<string, SettingSubCategory> SettingSubCategories { get; } = new();
    public int Priority { get; set; }
    public string? IconKey { get; set; }
}

public class SettingSubCategory
{
    public List<Setting> Settings { get; } = new();
    public int Priority { get; set; }
    public string? IconKey { get; set; }
}