namespace Configurator.Shared;

public class MarlinSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string Description { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public Dictionary<string, string>? Options { get; set; }
    public bool Enabled { get; set; } = true;
    public string Requires { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty; // To know where it belongs
    public string File { get; set; } = "Configuration.h"; // Configuration.h or Configuration_adv.h
}
