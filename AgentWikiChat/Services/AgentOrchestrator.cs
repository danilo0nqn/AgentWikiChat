using AgentWikiChat.Configuration;
using AgentWikiChat.Models;
using AgentWikiChat.Services.AI;
using AgentWikiChat.Services.Handlers;

namespace AgentWikiChat.Services;

/// <summary>
/// Orquestador principal que coordina el flujo entre el usuario, el servicio de IA y los handlers.
/// Usa IToolCallingService para soportar múltiples proveedores (Ollama, OpenAI, LM Studio, Anthropic).
/// Responsable de gestionar la memoria conversacional completa.
/// Ahora incluye soporte para ReAct Pattern con Multi-Tool Chain Loop.
/// </summary>
public class AgentOrchestrator
{
    private readonly IToolCallingService _toolService;
    private readonly Dictionary<string, IToolHandler> _handlers;
    private readonly List<ToolDefinition> _registeredTools;  // ← Nueva propiedad
    private readonly MemoryService _memory;
    private readonly AgentConfig _config;
    private readonly ReActEngine _reactEngine;
    private readonly bool _debugMode;

    public AgentOrchestrator(
        IToolCallingService toolService,
        IEnumerable<IToolHandler> handlers,
        MemoryService memory,
        AgentConfig config,
        bool debugMode = false)
    {
        _toolService = toolService;
        _memory = memory;
        _config = config;
        _debugMode = debugMode;

        // Registrar todos los handlers y sus tools
        _handlers = new Dictionary<string, IToolHandler>();
        var allTools = new List<ToolDefinition>();

        foreach (var handler in handlers)
        {
            // Verificar si el handler tiene múltiples tools
            if (handler.GetType().GetMethod("GetAllToolDefinitions") != null)
            {
                // Handler con múltiples tools (ej: WikipediaHandler)
                var getAllMethod = handler.GetType().GetMethod("GetAllToolDefinitions");
                var tools = (List<ToolDefinition>)getAllMethod!.Invoke(handler, null)!;

                foreach (var tool in tools)
                {
                    _handlers[tool.Function.Name] = handler;
                    allTools.Add(tool);
                }
            }
            else
            {
                // Handler con una sola tool (comportamiento por defecto)
                _handlers[handler.ToolName] = handler;
                allTools.Add(handler.GetToolDefinition());
            }
        }

        // Guardar las tools registradas para PrintAvailableHandlers
        _registeredTools = allTools;

        // Registrar todas las tools en el servicio de IA
        _toolService.RegisterTools(allTools);

        // Inicializar ReActEngine
        _reactEngine = new ReActEngine(_toolService, _handlers, _memory, _config, _debugMode);

        if (_debugMode)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"✅ Registradas {allTools.Count} herramientas en {_toolService.GetProviderName()}");
            Console.WriteLine($"🧠 Motor ReAct: {(_config.EnableReActPattern ? "ACTIVADO" : "DESACTIVADO")}");
            Console.WriteLine($"🔗 Multi-Tool Loop: {(_config.EnableMultiToolLoop ? "ACTIVADO" : "DESACTIVADO")}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Procesa una consulta del usuario, gestionando la memoria y coordinando con el LLM y handlers.
    /// Ahora soporta Multi-Tool Chain Loop con patrón ReAct.
    /// </summary>
    /// <param name="userQuery">Consulta del usuario</param>
    /// <returns>Respuesta generada (por LLM directo o handler)</returns>
    public async Task<string> ProcessQueryAsync(string userQuery)
    {
        // 1. Crear snapshot del contexto histórico ANTES de agregar el mensaje actual
        var historicalContext = _memory.Global.ToList();

        // 2. Agregar mensaje del usuario a la memoria
        _memory.AddToGlobal("user", userQuery);

        // 3. Decidir si usar ReAct Loop o modo legacy
        string finalResponse;

        if (_config.EnableReActPattern && _config.EnableMultiToolLoop)
        {
            // NUEVO: Usar ReActEngine para multi-tool loop
            if (_debugMode)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"🧠 Usando ReAct Engine (Multi-Tool Loop)");
                Console.ResetColor();
            }

            var executionResult = await _reactEngine.ExecuteAsync(userQuery, historicalContext);

            // Agregar todos los pasos a la memoria modular
            foreach (var step in executionResult.Steps)
            {
                if (!string.IsNullOrEmpty(step.ActionTool))
                {
                    _memory.AddToModule("react", "tool", $"[{step.Iteration}] {step.ActionTool}: {step.Observation}");
                }
            }

            finalResponse = executionResult.FinalAnswer;

            // Mostrar métricas si está en debug
            if (_debugMode)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n📊 Métricas: {executionResult.TotalIterations} iteraciones, " +
               $"{executionResult.ToolCallsCount} herramientas, " +
       $"{executionResult.TotalDurationMs}ms");
                Console.ResetColor();
            }
        }
        else
        {
            // LEGACY: Modo original (single tool call)
            if (_debugMode)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"🔍 Usando modo legacy (single tool call)");
                Console.ResetColor();
            }

            var response = await _toolService.SendMessageWithToolsAsync(
                userQuery,
                historicalContext
            );

            if (response.HasToolCalls)
            {
                finalResponse = await HandleToolCallAsync(response.ToolCalls!.First());
            }
            else
            {
                if (_debugMode)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("💬 LLM respondió directamente sin invocar tools");
                    Console.ResetColor();
                }

                finalResponse = response.Content ?? "No se recibió respuesta del servicio de IA.";
            }
        }

        // 4. Agregar respuesta final a la memoria
        _memory.AddToGlobal("assistant", finalResponse);

        return finalResponse;
    }

    /// <summary>
    /// Maneja la invocación de una tool call, ejecutando el handler correspondiente.
    /// (Método legacy, usado solo cuando ReAct está desactivado)
    /// </summary>
    private async Task<string> HandleToolCallAsync(ToolCall toolCall)
    {
        if (_debugMode)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🛠️  {_toolService.GetProviderName()} invocó tool: {toolCall.Function.Name}");
            Console.WriteLine($"📋 Argumentos: {toolCall.Function.GetArgumentsAsString()}");
            Console.ResetColor();
        }

        // Buscar handler correspondiente
        if (!_handlers.TryGetValue(toolCall.Function.Name, out var handler))
        {
            return $"⚠️ No hay handler configurado para la tool: {toolCall.Function.Name}";
        }

        if (_debugMode)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"⚙️  Ejecutando handler: {handler.GetType().Name}");
            Console.ResetColor();
        }

        // Ejecutar handler con parámetros estructurados
        var parameters = new ToolParameters(toolCall.Function.GetArgumentsAsString());
        var handlerResponse = await handler.HandleAsync(parameters, _memory);

        return handlerResponse;
    }

    /// <summary>
    /// Imprime las herramientas disponibles en la consola.
    /// </summary>
    public void PrintAvailableHandlers()
    {
        Console.WriteLine("\n🛠️  Herramientas disponibles:");
        foreach (var tool in _registeredTools)
        {
            Console.WriteLine($"   - {tool.Function.Name}: {tool.Function.Description}");
        }
        Console.WriteLine();
    }
}
