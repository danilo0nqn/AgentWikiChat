namespace AgentWikiChat.Models;

/// <summary>
/// Representa un paso en el ciclo ReAct (Reasoning + Acting).
/// El agente piensa (Thought), actúa (Action) y observa (Observation).
/// </summary>
public class ReActStep
{
    /// <summary>
    /// Número de iteración en el loop (1, 2, 3, ...)
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// Razonamiento del agente antes de actuar.
    /// </summary>
    public string? Thought { get; set; }

    /// <summary>
    /// Nombre de la herramienta invocada (si aplica).
    /// </summary>
  public string? ActionTool { get; set; }

    /// <summary>
    /// Argumentos de la herramienta invocada.
    /// </summary>
    public string? ActionArguments { get; set; }

  /// <summary>
    /// Resultado de la ejecución de la herramienta.
  /// </summary>
    public string? Observation { get; set; }

    /// <summary>
    /// Indica si este paso completó el objetivo (respuesta final).
 /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Respuesta final del agente (si IsComplete = true).
    /// </summary>
    public string? FinalAnswer { get; set; }

    /// <summary>
    /// Timestamp del paso.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Duración de la ejecución del paso en milisegundos.
    /// </summary>
    public long DurationMs { get; set; }
}
