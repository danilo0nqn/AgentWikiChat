using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentWikiChat.Models;

/// <summary>
/// Representa la respuesta de Ollama cuando decide invocar una herramienta.
/// </summary>
public class ToolCallResponse
{
    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public class ToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public ToolCallFunction Function { get; set; } = new();
}

public class ToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; } // Ollama puede enviar objeto o string

    /// <summary>
    /// Obtiene los argumentos como string JSON válido.
    /// Maneja tanto string como objetos JSON.
    /// </summary>
    public string GetArgumentsAsString()
    {
        if (Arguments.ValueKind == JsonValueKind.String)
        {
            // Si es un string, devolverlo directamente
            return Arguments.GetString() ?? "{}";
        }
        else if (Arguments.ValueKind == JsonValueKind.Object)
        {
            // Si es un objeto, serializarlo a string
            return Arguments.GetRawText();
        }

        return "{}";
    }
}
