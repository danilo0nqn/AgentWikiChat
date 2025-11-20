namespace AgentWikiChat.Models;

/// <summary>
/// Representa una herramienta (función) que cualquier LLM puede invocar.
/// Formato unificado basado en el estándar de OpenAI Function Calling.
/// Compatible con: Ollama, OpenAI, Anthropic, LM Studio, etc.
/// </summary>
public class ToolDefinition
{
    public string Type { get; set; } = "function";
    public FunctionDefinition Function { get; set; } = new();
}

/// <summary>
/// Definición de una función/herramienta.
/// </summary>
public class FunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FunctionParameters Parameters { get; set; } = new();
}

/// <summary>
/// Parámetros de una función con JSON Schema.
/// </summary>
public class FunctionParameters
{
    public string Type { get; set; } = "object";
    public Dictionary<string, PropertyDefinition> Properties { get; set; } = new();
    public List<string> Required { get; set; } = new();
}

/// <summary>
/// Definición de una propiedad de parámetro.
/// </summary>
public class PropertyDefinition
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string>? Enum { get; set; }
}
