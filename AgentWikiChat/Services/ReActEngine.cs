using System.Diagnostics;
using AgentWikiChat.Configuration;
using AgentWikiChat.Models;
using AgentWikiChat.Services.AI;
using AgentWikiChat.Services.Handlers;

namespace AgentWikiChat.Services;

/// <summary>
/// Motor ReAct (Reasoning + Acting) que ejecuta loops de herramientas múltiples.
/// Implementa el patrón: Thought → Action → Observation → Repeat hasta terminar.
/// </summary>
public class ReActEngine
{
    private readonly IToolCallingService _toolService;
    private readonly Dictionary<string, IToolHandler> _handlers;
    private readonly MemoryService _memory;
    private readonly AgentConfig _config;
    private readonly bool _debugMode;

    public ReActEngine(
        IToolCallingService toolService,
        Dictionary<string, IToolHandler> handlers,
        MemoryService memory,
        AgentConfig config,
        bool debugMode = false)
    {
        _toolService = toolService;
        _handlers = handlers;
        _memory = memory;
        _config = config;
        _debugMode = debugMode;
    }

    /// <summary>
    /// Ejecuta el loop ReAct completo para una consulta del usuario.
    /// </summary>
    /// <param name="userQuery">Consulta del usuario</param>
    /// <param name="historicalContext">Contexto histórico de la conversación</param>
    /// <returns>Resultado completo de la ejecución con métricas</returns>
    public async Task<AgentExecutionResult> ExecuteAsync(string userQuery, List<Message> historicalContext)
    {
        var result = new AgentExecutionResult
        {
            StartTime = DateTime.Now,
            Success = false
        };

        var stopwatch = Stopwatch.StartNew();
        
        // 🔧 FIX: Sanitizar el contexto al inicio para evitar mensajes assistant vacíos
        // Esto es crítico para Anthropic que requiere contenido no vacío
        var currentContext = SanitizeContext(historicalContext);

        // Para detectar loops: tracking de herramientas invocadas
        string? lastToolName = null;
        string? lastToolArgs = null;
        int consecutiveDuplicates = 0;

        try
        {
            LogInfo($"[INICIO] 🔄 ReAct Loop (máx {_config.MaxIterations} iteraciones)");

            for (int iteration = 1; iteration <= _config.MaxIterations; iteration++)
            {
                var step = new ReActStep { Iteration = iteration };
                var stepStopwatch = Stopwatch.StartNew();

                LogInfo($"\n{'=',-60}");
                LogInfo($"[ITERACION 🔄 {iteration}/{_config.MaxIterations}]");
                LogInfo($"{'=',-60}");

                // 🎯 MEJORA 1: Advertencia proactiva cuando quedan N iteraciones
                var iterationsRemaining = _config.MaxIterations - iteration + 1;
                if (iterationsRemaining == _config.IterationWarningThreshold && result.Steps.Any(s => !string.IsNullOrEmpty(s.ActionTool)))
                {
                    LogWarning($"⚠️  Advertencia: Solo quedan {iterationsRemaining} iteraciones. Prepará respuesta final.");
                    currentContext.Add(new Message("system",
                        $"⚠️ IMPORTANTE: Solo quedan {iterationsRemaining} iteraciones disponibles de {_config.MaxIterations}. " +
                        "Si ya tenés suficiente información, preparate para dar una respuesta final al usuario en la próxima iteración. " +
                        "Si necesitás usar más herramientas, usá solo las estrictamente necesarias."));
                }

                // 🎯 MEJORA 2: Última iteración reservada para respuesta final (si está configurado)
                bool isLastIteration = iteration == _config.MaxIterations;
                bool hasPreviousObservations = result.Steps.Any(s => !string.IsNullOrEmpty(s.Observation));

                if (_config.ReserveLastIterationForFinalAnswer && isLastIteration && hasPreviousObservations)
                {
                    LogWarning($"⏰ Última iteración alcanzada. Solicitando respuesta final...");
                    
                    // Forzar al LLM a dar una respuesta final basada en observaciones previas
                    currentContext.Add(new Message("system",
                        $"🛑 Has alcanzado la iteración final ({_config.MaxIterations}/{_config.MaxIterations}). " +
                        "NO puedes invocar más herramientas. " +
                        "Debes generar una respuesta final para el usuario AHORA basándote en toda la información recopilada. " +
                        "Resume lo que encontraste y presenta una respuesta completa y útil."));

                    // 🔧 Sanitizar contexto antes de enviar (crítico para Anthropic)
                    currentContext = SanitizeContext(currentContext);

                    // Llamar al LLM sin herramientas para forzar respuesta final
                    var finalResponse = await _toolService.SendMessageWithToolsAsync(
                        userQuery,
                        currentContext
                    );

                    if (finalResponse.TokenUsage != null)
                    {
                        step.TokenUsage = finalResponse.TokenUsage;
                        result.TokenMetrics.Add(finalResponse.TokenUsage);
                        if (_config.ShowIntermediateSteps || _debugMode)
                        {
                            LogTokens($"[TOKENS] 📊 Iteración {iteration}: {finalResponse.TokenUsage.FormatCompact()}");
                        }
                    }

                    step.IsComplete = true;
                    step.FinalAnswer = finalResponse.Content ?? "Sin respuesta final disponible";
                    step.DurationMs = stepStopwatch.ElapsedMilliseconds;
                    result.Steps.Add(step);

                    Console.WriteLine();
                    LogSuccess($"✅ Respuesta final generada en última iteración");

                    result.FinalAnswer = step.FinalAnswer;
                    result.Success = true;
                    result.TerminationReason = "Respuesta final generada al alcanzar límite de iteraciones";
                    break;
                }

                // Enviar mensaje al LLM con contexto actual
                // 🔧 Sanitizar contexto antes de cada llamada al LLM (crítico para Anthropic)
                currentContext = SanitizeContext(currentContext);
                
                var response = await _toolService.SendMessageWithToolsAsync(
                    userQuery,
                    currentContext
                );

                // 📊 Trackear tokens de esta iteración
                if (response.TokenUsage != null)
                {
                    step.TokenUsage = response.TokenUsage;
                    result.TokenMetrics.Add(response.TokenUsage);

                    // Mostrar métricas de tokens si está en modo debug o ShowIntermediateSteps
                    if (_config.ShowIntermediateSteps || _debugMode)
                    {
                        LogTokens($"[TOKENS] 📊 Iteración {iteration}: {response.TokenUsage.FormatCompact()}");
                    }
                }

                // Caso 1: El LLM respondió directamente sin tool calls (terminó)
                if (!response.HasToolCalls)
                {
                    step.IsComplete = true;
                    step.FinalAnswer = response.Content ?? "Sin respuesta";
                    step.DurationMs = stepStopwatch.ElapsedMilliseconds;
                    result.Steps.Add(step);

                    Console.WriteLine();
                    LogSuccess($"✅ Respuesta final obtenida (sin herramientas)");

                    result.FinalAnswer = step.FinalAnswer;
                    result.Success = true;
                    result.TerminationReason = "Respuesta directa del LLM";
                    break;
                }

                // Caso 2: El LLM invocó herramientas
                if (response.ToolCalls != null && response.ToolCalls.Any())
                {
                    // Agregar el mensaje del assistant con tool_calls al contexto
                    currentContext.Add(new Message("assistant", response.Content ?? string.Empty, response.ToolCalls));
                
                    foreach (var toolCall in response.ToolCalls)
                    {
                        step.ActionTool = toolCall.Function.Name;
                        step.ActionArguments = toolCall.Function.GetArgumentsAsString();

                        // Detectar loop: misma herramienta con mismos argumentos
                        if (_config.PreventDuplicateToolCalls &&
                             step.ActionTool == lastToolName &&
                        step.ActionArguments == lastToolArgs)
                        {
                            consecutiveDuplicates++;
                            LogWarning($"⚠️  Detectado: misma herramienta invocada {consecutiveDuplicates} veces consecutivas");

                            if (consecutiveDuplicates >= _config.MaxConsecutiveDuplicates)
                            {
                                LogWarning($"🔁 Loop detectado! Solicitando respuesta final...");

                                // Forzar al LLM a responder con la información disponible
                                currentContext.Add(new Message("system",
                                    "🔁 Detecté que estás invocando la misma herramienta repetidamente. " +
                                    "Por favor, genera una respuesta final para el usuario con la información que ya tenés. " +
                                    "NO invoques más herramientas."));

                                // 🔧 Sanitizar contexto antes de enviar (crítico para Anthropic)
                                currentContext = SanitizeContext(currentContext);

                                var loopResponse = await _toolService.SendMessageWithToolsAsync(userQuery, currentContext);
                            
                                result.FinalAnswer = loopResponse.Content ?? "Información procesada";
                                result.Success = true;
                                result.TerminationReason = $"Loop detectado - {consecutiveDuplicates} invocaciones duplicadas";
                                step.IsComplete = true;
                                step.FinalAnswer = result.FinalAnswer;
                                step.DurationMs = stepStopwatch.ElapsedMilliseconds;
                                result.Steps.Add(step);
                                return result;
                            }
                        }
                        else
                        {
                            // Resetear contador si es diferente
                            consecutiveDuplicates = 0;
                        }

                        // Actualizar tracking
                        lastToolName = step.ActionTool;
                        lastToolArgs = step.ActionArguments;

                        LogTool($"[TOOL] 🛠️  Herramienta invocada: {step.ActionTool}");
                        LogDebug($"📝 Argumentos: {step.ActionArguments}");

                        // Ejecutar handler
                        var observation = await ExecuteToolAsync(toolCall);
                        step.Observation = observation;

                        LogObservation($"👁️  Observación: {TruncateForDisplay(observation, 500)}");

                        // Agregar la observación al contexto
                        currentContext.Add(new Message("tool", observation, toolCall.Id));

                        // Guardar en memoria modular
                        _memory.AddToModule("react", "tool", $"{step.ActionTool}: {observation}");
                    }
                }

                step.DurationMs = stepStopwatch.ElapsedMilliseconds;
                result.Steps.Add(step);

                // Si no estamos en modo multi-tool loop, salir después de la primera tool
                if (!_config.EnableMultiToolLoop)
                {
                    result.FinalAnswer = step.Observation ?? "Ejecución completada";
                    result.Success = true;
                    result.TerminationReason = "Modo single-tool (multi-tool desactivado)";
                    break;
                }

                // Continuar el loop para permitir que el LLM procese la observación
            }

            // 🎯 MEJORA 3: Este código ya no debería alcanzarse, pero por seguridad
            if (!result.Success)
            {
                LogWarning($"⚠️  Límite de iteraciones alcanzado sin respuesta final explícita");
                
                // Intentar generar respuesta final con el contexto current
                var lastObservation = result.Steps.LastOrDefault()?.Observation;
                if (!string.IsNullOrEmpty(lastObservation))
                {
                    result.FinalAnswer = lastObservation;
                    result.Success = true;
                    result.TerminationReason = $"Límite de {_config.MaxIterations} iteraciones - usando última observación";
                }
                else
                {
                    result.FinalAnswer = "Se alcanzó el límite de iteraciones sin completar la tarea.";
                    result.TerminationReason = $"Límite de {_config.MaxIterations} iteraciones alcanzado";
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.FinalAnswer = $"Error durante la ejecución: {ex.Message}";
            result.TerminationReason = $"Excepción: {ex.GetType().Name}";
            LogError($"❌ Error: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.EndTime = DateTime.Now;
            result.TotalDurationMs = stopwatch.ElapsedMilliseconds;

            LogInfo($"\n[MÉTRICAS] 📊 Resumen de ejecución");
            LogInfo($"   ⏱️ Duración total: {result.TotalDurationMs}ms");
            LogInfo($"   🔄 Iteraciones: {result.TotalIterations}");
            LogInfo($"   🛠️ Herramientas usadas: {result.ToolCallsCount}");
            var estadoTexto = result.Success ? "Éxito" : "Fallo";
            LogInfo($"   ✅ Estado: {estadoTexto}");
            LogInfo($"   📝 Razón: {result.TerminationReason}");

            // 📊 Mostrar resumen de tokens
            if (result.TokenMetrics.CallCount > 0)
            {
                // Detectar si es un proveedor de pago para mostrar costos
                var providerName = _toolService.GetProviderName().ToLowerInvariant();
                var isPaidProvider = providerName.Contains("openai") ||
                                    providerName.Contains("anthropic") ||
                                    providerName.Contains("claude") ||
                                    providerName.Contains("gemini") ||
                                    providerName.Contains("google");

                LogInfo($"\n{result.TokenMetrics.FormatSummary(showCost: isPaidProvider)}");
                Console.WriteLine();
            }
        }

        return result;
    }

    /// <summary>
    /// Ejecuta una herramienta específica.
    /// </summary>
    private async Task<string> ExecuteToolAsync(ToolCall toolCall)
    {
        if (!_handlers.TryGetValue(toolCall.Function.Name, out var handler))
        {
            return $"⚠️ Error: No existe handler para la herramienta '{toolCall.Function.Name}'";
        }

        try
        {
            var parameters = new ToolParameters(toolCall.Function.GetArgumentsAsString());
            var result = await handler.HandleAsync(parameters, _memory);
            return result;
        }
        catch (Exception ex)
        {
            var errorMsg = $"❌ Error ejecutando {toolCall.Function.Name}: {ex.Message}";
            LogError(errorMsg);
            return errorMsg;
        }
    }

    /// <summary>
    /// Sanitiza el contexto para asegurar que todos los mensajes assistant tengan contenido no vacío.
    /// Crítico para Anthropic API que rechaza mensajes assistant vacíos.
    /// </summary>
    private List<Message> SanitizeContext(List<Message> context)
    {
        var sanitized = new List<Message>(context.Count); // Pre-allocate capacity

        foreach (var message in context)
        {
            // Si es un mensaje assistant con tool_calls pero sin contenido, agregar texto descriptivo
            if (message.Role == "assistant" && 
                string.IsNullOrWhiteSpace(message.Content) && 
                message.ToolCalls?.Any() == true)
            {
                sanitized.Add(new Message("assistant", 
                    "Procesando información con herramientas...", 
                    message.ToolCalls));
            }
            else
            {
                sanitized.Add(message);
            }
        }

        return sanitized;
    }

    #region Logging Helpers

    private void LogInfo(string message)
    {
        if (_config.ShowIntermediateSteps)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    private void LogSuccess(string message)
    {
        if (_config.ShowIntermediateSteps)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    private void LogTool(string message)
    {
        if (_config.ShowIntermediateSteps)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    private void LogObservation(string message)
    {
        if (_config.ShowIntermediateSteps)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    private void LogWarning(string message)
    {
        if (_config.ShowIntermediateSteps)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    private void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private void LogDebug(string message)
    {
        if (_debugMode && _config.VerboseMode)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    private void LogTokens(string message)
    {
        if (_config.ShowIntermediateSteps || _debugMode)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }

    private string TruncateForDisplay(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    #endregion
}
