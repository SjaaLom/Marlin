using System.Text.RegularExpressions;
using Configurator.Shared;

namespace Configurator.Server.Services;

public interface IConfigurationService
{
    Task SaveConfigurationAsync(List<MarlinSetting> settings);
}

public class ConfigurationService : IConfigurationService
{
    private readonly string _workspaceRoot;

    public ConfigurationService()
    {
        _workspaceRoot = @"C:\Users\hilmar\OneDrive\Makers\Marlin\Marlin";
    }

    public async Task SaveConfigurationAsync(List<MarlinSetting> settings)
    {
        var backupDir = Path.Combine(_workspaceRoot, "backups");
        if (!Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var configPath = Path.Combine(_workspaceRoot, "Marlin/Configuration.h");
        var advConfigPath = Path.Combine(_workspaceRoot, "Marlin/Configuration_adv.h");

        if (File.Exists(configPath)) File.Copy(configPath, Path.Combine(backupDir, $"Configuration-{timestamp}.h"));
        if (File.Exists(advConfigPath)) File.Copy(advConfigPath, Path.Combine(backupDir, $"Configuration_adv-{timestamp}.h"));

        var settingsByFile = settings.GroupBy(s => s.File);

        foreach (var fileGroup in settingsByFile)
        {
            var filePath = Path.Combine(_workspaceRoot, "Marlin", fileGroup.Key);
            if (!File.Exists(filePath)) continue;

            var lines = (await File.ReadAllLinesAsync(filePath)).ToList();
            
            var settingsByKey = fileGroup.GroupBy(s => s.Key);

            foreach (var keyGroup in settingsByKey)
            {
                var key = keyGroup.Key;
                var settingsForKey = keyGroup.ToList();
                
                // Find all matches in the file
                var matches = new List<int>();
                var regex = new Regex($@"^(\s*//)?\s*#define\s+{Regex.Escape(key)}(\s+.*)?$");
                
                for (int i = 0; i < lines.Count; i++)
                {
                    if (regex.IsMatch(lines[i])) matches.Add(i);
                }

                // Update matches
                // We assume the order of settings in the list matches the order of occurrences in the file.
                // This is generally true if the schema was generated from the file.
                for (int i = 0; i < Math.Min(settingsForKey.Count, matches.Count); i++)
                {
                    var lineIndex = matches[i];
                    var setting = settingsForKey[i];
                    
                    // Regex to capture: Indent, Prefix (//), Key, and Rest of line
                    var matchRegex = new Regex(@"^(?<indent>\s*)(?<prefix>//\s*)?#define\s+(?<key>[A-Za-z0-9_]+)(?<rest>.*)$");
                    var match = matchRegex.Match(lines[lineIndex]);
                    
                    if (!match.Success || match.Groups["key"].Value != setting.Key)
                    {
                        continue; 
                    }

                    var indent = match.Groups["indent"].Value;
                    var prefix = match.Groups["prefix"].Value;
                    var rest = match.Groups["rest"].Value;

                    // Parse 'rest' to separate value and comment
                    string preComment = rest;
                    string comment = "";
                    
                    var commentIdx = rest.IndexOf("//");
                    if (commentIdx >= 0)
                    {
                        preComment = rest.Substring(0, commentIdx);
                        comment = rest.Substring(commentIdx);
                    }

                    // Parse preComment into Separator, Value, Trailing
                    var valMatch = Regex.Match(preComment, @"^(?<sep>\s*)(?<val>.*?)(?<trail>\s*)$");
                    
                    var separator = valMatch.Groups["sep"].Value;
                    var originalVal = valMatch.Groups["val"].Value;
                    var trailing = valMatch.Groups["trail"].Value;

                    // Determine new Prefix
                    var newPrefix = setting.Enabled ? "" : "//";
                    if (!setting.Enabled && string.IsNullOrWhiteSpace(prefix))
                    {
                        newPrefix = "//";
                    }
                    else if (!setting.Enabled)
                    {
                        newPrefix = prefix;
                    }

                    // Determine new Value
                    var newValue = setting.Value;
                    
                    // Handle Boolean Casing
                    if (setting.Type == "bool" || newValue.Equals("true", StringComparison.OrdinalIgnoreCase) || newValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        newValue = newValue.ToLowerInvariant();
                    }

                    // Check for numerical equivalence to preserve original formatting
                    if (!string.IsNullOrEmpty(originalVal) && IsNumericallyEquivalent(originalVal, newValue))
                    {
                        newValue = originalVal;
                    }
                    else
                    {
                        // Value changed or original was empty.
                        // Handle Float Suffix logic based on original value style or default
                        if (setting.Type == "float")
                        {
                            bool originalHadF = !string.IsNullOrEmpty(originalVal) && originalVal.Trim().EndsWith("f", StringComparison.OrdinalIgnoreCase);
                            bool newHasF = newValue.Trim().EndsWith("f", StringComparison.OrdinalIgnoreCase);

                            if (originalHadF && !newHasF)
                            {
                                // Original had F, new doesn't -> Add it
                                if (double.TryParse(newValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                                {
                                    newValue += "f";
                                }
                            }
                            else if (string.IsNullOrEmpty(originalVal) && !newHasF)
                            {
                                // New value, no original context. Default to adding 'f' as per requirement.
                                    if (double.TryParse(newValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                                    {
                                        newValue += "f";
                                    }
                            }
                        }
                    }

                    // Construct new line parts
                    var newSeparator = separator;
                    if (string.IsNullOrEmpty(newSeparator) && !string.IsNullOrEmpty(newValue))
                    {
                        // If we are adding a value where there was none, ensure a space
                        newSeparator = " ";
                    }

                    string valuePart;
                    if (string.IsNullOrEmpty(newValue))
                    {
                        // Value is empty.
                        if (string.IsNullOrEmpty(originalVal))
                        {
                            valuePart = separator; 
                        }
                        else
                        {
                            valuePart = separator;
                        }
                    }
                    else
                    {
                        // Value is present.
                        valuePart = newSeparator + newValue + trailing;
                    }

                    var newLine = $"{indent}{newPrefix}#define {setting.Key}{valuePart}{comment}";

                    lines[lineIndex] = newLine;
                }
            }

            await File.WriteAllLinesAsync(filePath, lines);
        }
    }

    private bool IsNumericallyEquivalent(string original, string current)
    {
        if (original == current) return true;
        
        // If one is empty and other is not, not equivalent
        if (string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(current)) return false;

        // Check if original has leading zeros and current doesn't
        // e.g. original "02010300", current "2010300"
        var origTrimmed = original.TrimStart('0');
        var currTrimmed = current.TrimStart('0');

        if (origTrimmed == "" && currTrimmed == "") return true; // Both are zero(s)
        if (origTrimmed == currTrimmed) return true;

        // Check for float equivalence (ignoring 'f' suffix)
        var origFloatStr = original.TrimEnd('f', 'F');
        var currFloatStr = current.TrimEnd('f', 'F');

        if (double.TryParse(origFloatStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double origVal) &&
            double.TryParse(currFloatStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double currVal))
        {
            // Use a small epsilon for float comparison
            if (Math.Abs(origVal - currVal) < 0.000001)
            {
                return true;
            }
        }

        return false;
    }
}
