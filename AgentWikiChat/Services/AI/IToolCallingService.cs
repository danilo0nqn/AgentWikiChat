using AgentWikiChat.Models;

namespace AgentWikiChat.Services.AI;

/// <summary>
/// Interfaz unificada para servicios de IA con soporte de Tool Calling.
/// Implementaciones: Ollama, OpenAI, LM Studio, Anthropic, etc.
/// </summary>
public interface IToolCallingService
{
    /// <summary>
    /// Registra una herramienta para que el LLM pueda invocarla.
    /// </summary>
    void RegisterTool(ToolDefinition tool);

    /// <summary>
    /// Registra múltiples herramientas.
    /// </summary>
    void RegisterTools(IEnumerable<ToolDefinition> tools);

    /// <summary>
    /// Obtiene la lista de herramientas registradas.
    /// </summary>
    IReadOnlyList<ToolDefinition> GetRegisteredTools();

    /// <summary>
    /// Envía un mensaje con contexto y herramientas disponibles.
    /// El LLM decidirá si necesita invocar alguna herramienta.
    /// </summary>
    /// <param name="message">Mensaje del usuario</param>
    /// <param name="context">Historial de mensajes previos</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Respuesta unificada con posibles tool calls</returns>
    Task<ToolCallingResponse> SendMessageWithToolsAsync(
        string message,
        IEnumerable<Message> context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nombre del proveedor para debug/logging.
    /// </summary>
    string GetProviderName();
}
