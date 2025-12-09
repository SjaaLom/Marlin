using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Configurator.Shared;

namespace Configurator.Server.Services;

public interface ISchemaService
{
    Task<List<ConfigGroup>> GetSchemaAsync();
    Task<bool> IsPythonInstalledAsync();
}

public class SchemaService : ISchemaService
{
    private readonly IWebHostEnvironment _env;
    private readonly string _workspaceRoot;

    public SchemaService(IWebHostEnvironment env)
    {
        _env = env;
        // Hardcoded for now based on user context
        _workspaceRoot = @"C:\Users\hilmar\OneDrive\Makers\Marlin\Marlin";
    }

    public async Task<bool> IsPythonInstalledAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<ConfigGroup>> GetSchemaAsync()
    {
        if (!await IsPythonInstalledAsync())
        {
            throw new Exception("Python is not installed or not found in PATH.");
        }

        // schema.py is in buildroot/share/PlatformIO/scripts/schema.py
        var scriptPath = Path.Combine(_workspaceRoot, "buildroot/share/PlatformIO/scripts/schema.py");
        if (!File.Exists(scriptPath))
        {
             throw new FileNotFoundException($"Schema script not found at {scriptPath}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{scriptPath}\" json",
            WorkingDirectory = _workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) throw new Exception("Failed to start python process.");
        
        // We don't strictly need to read stdout as schema.py writes to a file, 
        // but it's good for debugging.
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Schema generation failed: {error}");
        }

        // schema.py writes to schema.json in the working directory
        var jsonPath = Path.Combine(_workspaceRoot, "schema.json");
        if (!File.Exists(jsonPath))
        {
            throw new Exception("schema.json was not generated.");
        }

        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        File.Delete(jsonPath); // Cleanup

        return ParseSchema(jsonContent);
    }

    private List<ConfigGroup> ParseSchema(string jsonContent)
    {
        var root = new List<ConfigGroup>();
        using var doc = JsonDocument.Parse(jsonContent);
        
        // The schema has "basic" (Configuration.h) and "advanced" (Configuration_adv.h)
        foreach (var fileProp in doc.RootElement.EnumerateObject())
        {
            var fileKey = fileProp.Name; // basic or advanced
            var fileName = fileKey == "basic" ? "Configuration.h" : "Configuration_adv.h";
            
            var fileGroup = new ConfigGroup { Name = fileName };
            
            foreach (var sectionProp in fileProp.Value.EnumerateObject())
            {
                var sectionName = sectionProp.Name;
                var sectionGroup = new ConfigGroup { Name = sectionName };

                foreach (var defineProp in sectionProp.Value.EnumerateObject())
                {
                    var defineName = defineProp.Name;
                    var defineObj = defineProp.Value;

                    if (defineObj.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in defineObj.EnumerateArray())
                        {
                            sectionGroup.Settings.Add(MapSetting(defineName, item, fileName));
                        }
                    }
                    else
                    {
                        sectionGroup.Settings.Add(MapSetting(defineName, defineObj, fileName));
                    }
                }
                
                fileGroup.SubGroups.Add(sectionGroup);
            }
            root.Add(fileGroup);
        }

        return root;
    }

    private MarlinSetting MapSetting(string key, JsonElement element, string fileName)
    {
        var setting = new MarlinSetting
        {
            Key = key,
            File = fileName,
            Section = element.TryGetProperty("section", out var s) ? s.GetString() ?? "" : "",
            Enabled = element.TryGetProperty("enabled", out var e) && e.GetBoolean(),
            Requires = element.TryGetProperty("requires", out var r) ? r.GetString() ?? "" : "",
            Description = element.TryGetProperty("comment", out var c) ? c.GetString() ?? "" : "",
            Units = element.TryGetProperty("units", out var u) ? u.GetString() ?? "" : "",
            Type = element.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string"
        };

        if (element.TryGetProperty("value", out var v))
        {
            setting.Value = v.ToString();
        }

        if (element.TryGetProperty("options", out var o))
        {
            var optStr = o.GetString();
            if (!string.IsNullOrEmpty(optStr))
            {
                setting.Options = ParseOptions(optStr);
            }
        }

        return setting;
    }

    private Dictionary<string, string> ParseOptions(string optStr)
    {
        var options = new Dictionary<string, string>();
        
        // 1. Try parsing as JSON array of strings (e.g. ['A', 'B'])
        try 
        {
            var validJson = optStr.Replace("'", "\"");
            var list = JsonSerializer.Deserialize<List<string>>(validJson);
            if (list != null)
            {
                foreach (var item in list)
                {
                    options[item] = item;
                }
                return options;
            }
        }
        catch { /* Not a list of strings */ }

        // 2. Try parsing as JSON array of numbers (e.g. [-1, 0, 1])
        try
        {
            // No quote replacement needed for numbers, but brackets might be there
            var list = JsonSerializer.Deserialize<List<int>>(optStr);
            if (list != null)
            {
                foreach (var item in list)
                {
                    options[item.ToString()] = item.ToString();
                }
                return options;
            }
        }
        catch { /* Not a list of numbers */ }

        // 3. Try parsing custom Key:Value format (e.g. [ 1:'Description', 2:'Desc' ])
        // Regex to match:  key : 'value'
        // Keys can be integers or negative integers. Values are single-quoted strings.
        var regex = new Regex(@"(?<key>-?\d+)\s*:\s*'(?<val>[^']*)'");
        var matches = regex.Matches(optStr);
        if (matches.Count > 0)
        {
            foreach (Match match in matches)
            {
                var key = match.Groups["key"].Value;
                var val = match.Groups["val"].Value;
                // Combine key and description for display, or just use description?
                // User usually wants to see "1 - Description"
                // But the value to save is "1".
                options[key] = val; 
            }
            return options;
        }

        return options;
    }
}
