using AgentWikiChat.Models;

namespace AgentWikiChat.Services.Handlers;

/// <summary>
/// Interfaz para handlers que soportan Tool Calling con cualquier proveedor.
/// Los handlers implementan esta interfaz para definir qué herramienta manejan
/// y cómo procesarla con parámetros estructurados.
/// </summary>
public interface IToolHandler
{
    /// <summary>
    /// Nombre de la herramienta que este handler maneja (debe coincidir con el tool name).
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// Definición de la herramienta para que el LLM sepa cuándo y cómo invocarla.
    /// Formato agnóstico compatible con todos los proveedores.
    /// </summary>
    ToolDefinition GetToolDefinition();

    /// <summary>
    /// Ejecuta el handler con los parámetros estructurados extraídos por el LLM.
    /// </summary>
    /// <param name="parameters">Parámetros estructurados de la tool call</param>
    /// <param name="memory">Servicio de memoria para contexto</param>
    /// <returns>Respuesta generada por el handler</returns>
    Task<string> HandleAsync(ToolParameters parameters, MemoryService memory);
}
