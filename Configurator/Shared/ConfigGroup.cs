namespace Configurator.Shared;

public class ConfigGroup
{
    public string Name { get; set; } = string.Empty;
    public List<MarlinSetting> Settings { get; set; } = new();
    public List<ConfigGroup> SubGroups { get; set; } = new();
}
