namespace AgentWikiChat.Models;

/// <summary>
/// Resultado completo de la ejecución del agente con métricas y trazabilidad.
/// </summary>
public class AgentExecutionResult
{
    /// <summary>
    /// Respuesta final del agente para el usuario.
    /// </summary>
    public string FinalAnswer { get; set; } = string.Empty;

/// <summary>
    /// Lista de pasos ReAct ejecutados durante el proceso.
    /// </summary>
    public List<ReActStep> Steps { get; set; } = new();

 /// <summary>
    /// Indica si el agente completó exitosamente la tarea.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Razón por la que terminó (ej: "Objetivo alcanzado", "Límite de iteraciones", "Error").
    /// </summary>
 public string TerminationReason { get; set; } = string.Empty;

    /// <summary>
    /// Número total de iteraciones realizadas.
    /// </summary>
    public int TotalIterations => Steps.Count;

    /// <summary>
    /// Número de herramientas invocadas.
    /// </summary>
    public int ToolCallsCount => Steps.Count(s => !string.IsNullOrEmpty(s.ActionTool));

    /// <summary>
    /// Duración total de la ejecución en milisegundos.
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Timestamp de inicio de la ejecución.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Timestamp de finalización de la ejecución.
    /// </summary>
    public DateTime EndTime { get; set; }
}
