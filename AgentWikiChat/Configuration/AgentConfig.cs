namespace AgentWikiChat.Configuration;

/// <summary>
/// Configuración del comportamiento del agente ReAct.
/// </summary>
public class AgentConfig
{
    /// <summary>
    /// Máximo número de iteraciones permitidas en el loop ReAct.
    /// Previene loops infinitos.
    /// </summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>
    /// Timeout por iteración en segundos.
    /// </summary>
    public int IterationTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Habilitar el patrón ReAct explícito (Thought -> Action -> Observation).
    /// Si es false, usa el modo original de single tool call.
    /// </summary>
    public bool EnableReActPattern { get; set; } = true;

    /// <summary>
    /// Habilitar el loop de múltiples herramientas.
    /// Permite al agente usar varias herramientas en secuencia.
    /// </summary>
    public bool EnableMultiToolLoop { get; set; } = true;

    /// <summary>
    /// Mostrar pasos intermedios en la consola (para debugging).
    /// </summary>
    public bool ShowIntermediateSteps { get; set; } = true;

    /// <summary>
    /// Permitir que el agente se auto-corrija si detecta errores.
    /// </summary>
    public bool EnableSelfCorrection { get; set; } = true;

    /// <summary>
    /// Modo verbose para logging detallado.
    /// </summary>
    public bool VerboseMode { get; set; } = false;

    /// <summary>
    /// Prevenir que el agente invoque la misma herramienta con los mismos argumentos consecutivamente.
    /// Ayuda a evitar loops infinitos.
    /// </summary>
    public bool PreventDuplicateToolCalls { get; set; } = true;

    /// <summary>
    /// Número máximo de veces que se puede invocar la misma herramienta consecutivamente.
    /// Solo aplica si PreventDuplicateToolCalls está activado.
    /// </summary>
    public int MaxConsecutiveDuplicates { get; set; } = 2;

    /// <summary>
    /// Reservar la última iteración exclusivamente para que el LLM genere una respuesta final.
    /// Cuando está activado, en la última iteración no se permiten tool calls, forzando una respuesta.
    /// </summary>
    public bool ReserveLastIterationForFinalAnswer { get; set; } = true;

    /// <summary>
    /// Número de iteraciones antes del límite en las que se debe advertir al LLM.
    /// Por ejemplo, si es 2, se advertirá cuando queden 2 iteraciones.
    /// </summary>
    public int IterationWarningThreshold { get; set; } = 2;
}
