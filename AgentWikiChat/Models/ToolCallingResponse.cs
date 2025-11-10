namespace AgentWikiChat.Models;

/// <summary>
/// Respuesta unificada de cualquier servicio de Tool Calling.
/// Abstrae las diferencias entre proveedores (Ollama, OpenAI, LM Studio, etc.)
/// </summary>
public class ToolCallingResponse
{
    /// <summary>
    /// Contenido textual de la respuesta (si no hay tool calls).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Lista de herramientas que el LLM decidió invocar.
    /// </summary>
    public List<ToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// Rol del mensaje (generalmente "assistant").
    /// </summary>
    public string Role { get; set; } = "assistant";

    /// <summary>
    /// Indica si la respuesta está completa (usado por algunos proveedores).
    /// </summary>
    public bool Done { get; set; } = true;

    /// <summary>
    /// Metadata adicional del proveedor (opcional).
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Verifica si hay tool calls en la respuesta.
    /// </summary>
    public bool HasToolCalls => ToolCalls != null && ToolCalls.Any();
}
