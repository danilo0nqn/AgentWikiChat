namespace AgentWikiChat.Models;

/// <summary>
/// Métricas de uso de tokens para una llamada al LLM.
/// Incluye tokens de entrada (contexto + mensaje) y salida (respuesta).
/// </summary>
public class TokenUsage
{
    /// <summary>
    /// Tokens del prompt (contexto histórico + mensaje del usuario + herramientas).
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Tokens de la respuesta generada por el LLM.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Total de tokens usados (PromptTokens + CompletionTokens).
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// Nombre del modelo usado (ej: "gpt-4", "claude-3-opus", "llama3").
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Proveedor de IA (ej: "OpenAI", "Anthropic", "Ollama").
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Timestamp de la llamada.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Costo estimado de la llamada (en USD, si aplica).
    /// Solo para proveedores pagos con pricing conocido.
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Formatea el uso de tokens para display.
    /// </summary>
    public string FormatForDisplay(bool showCost = false)
    {
        var display = $"📊 Tokens: {TotalTokens:N0} total ({PromptTokens:N0} prompt + {CompletionTokens:N0} completion)";
        
        if (showCost && EstimatedCost.HasValue)
        {
            display += $" | 💰 ~${EstimatedCost:F6}";
        }

        return display;
    }

    /// <summary>
    /// Formatea para display compacto (una línea).
    /// </summary>
    public string FormatCompact()
    {
        return $"{TotalTokens:N0} tokens ({PromptTokens:N0}↑ / {CompletionTokens:N0}↓)";
    }
}

/// <summary>
/// Acumulador de métricas de tokens para toda una sesión o ejecución.
/// </summary>
public class TokenUsageAccumulator
{
    private readonly List<TokenUsage> _usages = new();
    private readonly object _lock = new();

    /// <summary>
    /// Agrega una nueva métrica de uso.
    /// </summary>
    public void Add(TokenUsage usage)
    {
        lock (_lock)
        {
            _usages.Add(usage);
        }
    }

    /// <summary>
    /// Obtiene el total de tokens usados en toda la sesión.
    /// </summary>
    public int TotalTokens
    {
        get
        {
            lock (_lock)
            {
                return _usages.Sum(u => u.TotalTokens);
            }
        }
    }

    /// <summary>
    /// Obtiene el total de tokens de prompt.
    /// </summary>
    public int TotalPromptTokens
    {
        get
        {
            lock (_lock)
            {
                return _usages.Sum(u => u.PromptTokens);
            }
        }
    }

    /// <summary>
    /// Obtiene el total de tokens de completion.
    /// </summary>
    public int TotalCompletionTokens
    {
        get
        {
            lock (_lock)
            {
                return _usages.Sum(u => u.CompletionTokens);
            }
        }
    }

    /// <summary>
    /// Obtiene el costo total estimado.
    /// </summary>
    public decimal TotalEstimatedCost
    {
        get
        {
            lock (_lock)
            {
                return _usages.Where(u => u.EstimatedCost.HasValue)
                              .Sum(u => u.EstimatedCost!.Value);
            }
        }
    }

    /// <summary>
    /// Obtiene el número de llamadas realizadas.
    /// </summary>
    public int CallCount
    {
        get
        {
            lock (_lock)
            {
                return _usages.Count;
            }
        }
    }

    /// <summary>
    /// Obtiene todas las métricas registradas.
    /// </summary>
    public List<TokenUsage> GetAll()
    {
        lock (_lock)
        {
            return _usages.ToList();
        }
    }

    /// <summary>
    /// Limpia todas las métricas.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _usages.Clear();
        }
    }

    /// <summary>
    /// Formatea un resumen completo de uso.
    /// </summary>
    public string FormatSummary(bool showCost = false)
    {
        lock (_lock)
        {
            if (_usages.Count == 0)
            {
                return "[METRICAS] 📊 Sin metricas de tokens disponibles";
            }

            var summary = $"[MÉTRICAS] 📊 Resumen de Tokens\n";
            summary += $"   🔢 Llamadas al LLM: {CallCount}\n";
            summary += $"   📤 Tokens de prompt: {TotalPromptTokens:N0}\n";
            summary += $"   📥 Tokens de completion: {TotalCompletionTokens:N0}\n";
            summary += $"   🔢 {TotalTokens:N0} tokens\n";

            if (showCost && TotalEstimatedCost > 0)
            {
                summary += $"   💰 Costo estimado: ${TotalEstimatedCost:F6}";
            }

            return summary;
        }
    }

    /// <summary>
    /// Formatea el resumen de forma compacta (una línea).
    /// </summary>
    public string FormatSummaryCompact(bool showCost = false)
    {
        lock (_lock)
        {
            var summary = $"[TOKENS] {TotalTokens:N0} tokens total ({CallCount} llamadas)";
            
            if (showCost && TotalEstimatedCost > 0)
            {
                summary += $" | [COSTO] ~${TotalEstimatedCost:F6}";
            }

            return summary;
        }
    }
}
