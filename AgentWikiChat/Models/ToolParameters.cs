using System.Text.Json;

namespace AgentWikiChat.Models;

/// <summary>
/// Representa los parámetros estructurados extraídos por el LLM para un handler.
/// </summary>
public class ToolParameters
{
    private readonly Dictionary<string, JsonElement> _parameters = new();

    public ToolParameters(string jsonArguments)
    {
        if (!string.IsNullOrWhiteSpace(jsonArguments))
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonArguments);
            if (parsed != null)
            {
                _parameters = parsed;
            }
        }
    }

    public ToolParameters(Dictionary<string, JsonElement> parameters)
    {
        _parameters = parameters;
    }

    public string GetString(string key, string defaultValue = "")
    {
        if (_parameters.TryGetValue(key, out var value))
        {
            return value.GetString() ?? defaultValue;
        }
        return defaultValue;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        if (_parameters.TryGetValue(key, out var value) && value.TryGetInt32(out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        if (_parameters.TryGetValue(key, out var value))
        {
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
        }
        return defaultValue;
    }

    public T? GetObject<T>(string key) where T : class
    {
        if (_parameters.TryGetValue(key, out var value))
        {
            return JsonSerializer.Deserialize<T>(value.GetRawText());
        }
        return null;
    }

    public bool Has(string key) => _parameters.ContainsKey(key);

    public Dictionary<string, JsonElement> GetAll() => _parameters;
}
