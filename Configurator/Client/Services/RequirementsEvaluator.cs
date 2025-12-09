using Configurator.Shared;
using System.Text.RegularExpressions;

namespace Configurator.Client.Services;

public class RequirementsEvaluator
{
    private readonly Dictionary<string, MarlinSetting> _settings;

    public RequirementsEvaluator(IEnumerable<MarlinSetting> settings)
    {
        _settings = new Dictionary<string, MarlinSetting>();
        foreach (var s in settings)
        {
            if (!_settings.ContainsKey(s.Key))
            {
                _settings[s.Key] = s;
            }
        }
    }

    public bool IsMet(string requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement)) return true;

        // Normalize
        var expr = requirement.Trim();
        
        return Evaluate(expr);
    }

    private bool Evaluate(string expr)
    {
        expr = expr.Trim();
        
        // Remove outer parens if they surround the whole expression
        while (expr.StartsWith("(") && expr.EndsWith(")") && IsBalanced(expr.Substring(1, expr.Length - 2)))
        {
            expr = expr.Substring(1, expr.Length - 2).Trim();
        }

        // Handle OR (||) - split by || but respect parens
        var orParts = SplitBy(expr, "||");
        if (orParts.Count > 1)
        {
            return orParts.Any(Evaluate);
        }

        // Handle AND (&&)
        var andParts = SplitBy(expr, "&&");
        if (andParts.Count > 1)
        {
            return andParts.All(Evaluate);
        }

        // Handle NOT (!)
        if (expr.StartsWith("!"))
        {
            return !Evaluate(expr.Substring(1));
        }

        // Handle ANY(...)
        if (expr.StartsWith("ANY(") && expr.EndsWith(")"))
        {
            var content = expr.Substring(4, expr.Length - 5);
            var args = SplitBy(content, ",");
            return args.Any(arg => IsEnabled(arg.Trim()));
        }
        
        // Handle defined(...) or ENABLED(...)
        if ((expr.StartsWith("defined(") || expr.StartsWith("ENABLED(")) && expr.EndsWith(")"))
        {
            var start = expr.IndexOf('(') + 1;
            var key = expr.Substring(start, expr.Length - start - 1).Trim();
            return IsEnabled(key);
        }

        // Handle Comparisons (e.g. EXTRUDERS > 3)
        if (expr.Contains(">") || expr.Contains("<") || expr.Contains("=="))
        {
            if (expr.Contains(">="))
            {
                var parts = SplitBy(expr, ">=");
                if (parts.Count == 2) return GetValue(parts[0]) >= GetValue(parts[1]);
            }
            else if (expr.Contains("<="))
            {
                var parts = SplitBy(expr, "<=");
                if (parts.Count == 2) return GetValue(parts[0]) <= GetValue(parts[1]);
            }
            else if (expr.Contains(">"))
            {
                var parts = SplitBy(expr, ">");
                if (parts.Count == 2) return GetValue(parts[0]) > GetValue(parts[1]);
            }
            else if (expr.Contains("<"))
            {
                var parts = SplitBy(expr, "<");
                if (parts.Count == 2) return GetValue(parts[0]) < GetValue(parts[1]);
            }
             else if (expr.Contains("=="))
            {
                var parts = SplitBy(expr, "==");
                if (parts.Count == 2) return GetValue(parts[0]) == GetValue(parts[1]);
            }
        }

        // Fallback: assume it's a key checking if enabled (implicit defined)
        return IsEnabled(expr);
    }

    private bool IsEnabled(string key)
    {
        if (_settings.TryGetValue(key, out var s))
        {
            return s.Enabled;
        }
        // If it's not a setting, maybe it's a literal?
        // But defined(LITERAL) doesn't make sense unless it's a macro.
        // Assume false if not found.
        return false; 
    }

    private double GetValue(string keyOrVal)
    {
        keyOrVal = keyOrVal.Trim();
        if (double.TryParse(keyOrVal, out var val)) return val;
        
        if (_settings.TryGetValue(keyOrVal, out var s))
        {
            if (double.TryParse(s.Value, out var sVal)) return sVal;
        }
        return 0;
    }

    private List<string> SplitBy(string input, string delimiter)
    {
        var result = new List<string>();
        var current = "";
        var parenDepth = 0;
        
        for (int i = 0; i < input.Length; i++)
        {
            // Check for delimiter
            if (parenDepth == 0 && i + delimiter.Length <= input.Length && input.Substring(i, delimiter.Length) == delimiter)
            {
                result.Add(current);
                current = "";
                i += delimiter.Length - 1;
                continue;
            }
            
            if (input[i] == '(') parenDepth++;
            if (input[i] == ')') parenDepth--;
            
            current += input[i];
        }
        result.Add(current);
        return result;
    }

    private bool IsBalanced(string s)
    {
        int depth = 0;
        foreach (var c in s)
        {
            if (c == '(') depth++;
            if (c == ')') depth--;
            if (depth < 0) return false;
        }
        return depth == 0;
    }
}
