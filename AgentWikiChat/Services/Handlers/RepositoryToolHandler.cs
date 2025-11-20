using AgentWikiChat.Models;
using Microsoft.Extensions.Configuration;
using AgentWikiChat.Services.VersionControl;

namespace AgentWikiChat.Services.Handlers;

/// <summary>
/// Handler genérico para operaciones con repositorios de control de versiones.
/// SEGURIDAD: Solo permite operaciones de lectura. Bloqueado: commit, delete, update, etc.
/// Usa el patrón de factory para soportar múltiples proveedores (SVN, Git, GitHub, etc.) con patrón multi-provider.
/// </summary>
public class RepositoryToolHandler : IToolHandler
{
    private readonly IVersionControlHandler _versionControlHandler;
    private readonly RepositoryProviderConfig _providerConfig;
    private readonly bool _debugMode;

    public RepositoryToolHandler(IConfiguration configuration)
    {
        _debugMode = configuration.GetValue("Ui:Debug", true);

        // Crear handler específico usando factory (multi-provider)
        _versionControlHandler = VersionControlHandlerFactory.CreateHandler(configuration);
        
        // Obtener configuración del proveedor activo
        _providerConfig = VersionControlHandlerFactory.GetActiveProviderConfig(configuration);

        LogDebug($"[Repository] Inicializado - Provider: {_providerConfig.Name} ({_versionControlHandler.ProviderName})");
    }

    public string ToolName => $"{_versionControlHandler.ProviderName.ToLower()}_operation";

    public ToolDefinition GetToolDefinition()
    {
        var allowedOps = _versionControlHandler.GetAllowedOperations().ToList();

        return new ToolDefinition
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = ToolName,
                Description = $"Ejecuta operaciones de SOLO LECTURA en repositorios {_versionControlHandler.ProviderName} ({_providerConfig.Name}) - Operaciones: {string.Join(", ", allowedOps)}. NO permite modificaciones (commit, delete, add, update). Usa esta herramienta para consultar historial, ver archivos, obtener información del repositorio.",
                Parameters = new FunctionParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertyDefinition>
                    {
                        ["operation"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = $"Operación {_versionControlHandler.ProviderName} a ejecutar: 'log' (historial), 'info' (información), 'list' (listar archivos), 'cat' (ver contenido), 'diff' (diferencias), 'blame' (autoría), 'status' (estado)",
                            Enum = allowedOps
                        },
                        ["path"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = "Ruta del archivo o directorio en el repositorio (opcional, por defecto raíz). Ejemplo: '/trunk/src/MyFile.cs' (SVN) o 'src/MyFile.cs' (Git)"
                        },
                        ["revision"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = "Número de revisión o rango (ej: '1234', 'HEAD', '1000:1100' para SVN; 'HEAD', 'commit-hash', 'branch-name' para Git). Por defecto HEAD."
                        },
                        ["limit"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = "Límite de resultados para comandos como log (ej: '10', '50'). Por defecto 10."
                        }
                    },
                    Required = new List<string> { "operation" }
                }
            }
        };
    }

    public async Task<string> HandleAsync(ToolParameters parameters, MemoryService memory)
    {
        var operation = parameters.GetString("operation");
        var path = parameters.GetString("path", "");
        var revision = parameters.GetString("revision", "HEAD");
        var limit = parameters.GetString("limit", "10");

        if (string.IsNullOrWhiteSpace(operation))
        {
            return "⚠️ Error: La operación no puede estar vacía.";
        }

        // Verificar si el cliente está instalado
        if (!_versionControlHandler.IsClientInstalled())
        {
            return _versionControlHandler.GetInstallationInstructions();
        }

        LogDebug($"[{_versionControlHandler.ProviderName}] Operación recibida: {operation}, Path: {path}, Revision: {revision}");

        // 🔒 VALIDACIÓN DE SEGURIDAD
        if (!_versionControlHandler.IsOperationAllowed(operation))
        {
            var allowedOps = string.Join(", ", _versionControlHandler.GetAllowedOperations());
            LogError($"[{_versionControlHandler.ProviderName}] Operación rechazada: {operation}");
            return $"🔒 **Operación Rechazada por Seguridad**\n\n" +
                   $"La operación '{operation}' no está permitida.\n\n" +
                   $"✅ **Operaciones permitidas**: {allowedOps}\n\n" +
                   $"ℹ️ **Recuerda**: Solo se permiten operaciones de solo lectura.";
        }

        try
        {
            // Ejecutar operación
            var operationParams = new Dictionary<string, string>
            {
                ["path"] = path,
                ["revision"] = revision,
                ["limit"] = limit
            };

            var result = await _versionControlHandler.ExecuteReadOnlyOperationAsync(operation, operationParams);

            // Guardar en memoria modular
            memory.AddToModule(_versionControlHandler.ProviderName.ToLower(), "system",
                $"{_providerConfig.Name}: {operation} ejecutado: {path} @ {revision}");

            return FormatResult(operation, result, operationParams);
        }
        catch (Exception ex)
        {
            LogError($"[{_versionControlHandler.ProviderName}] Error: {ex.Message}");

            // Error específico si el cliente no se encuentra
            if (ex.Message.Contains("cannot find the file") || ex.Message.Contains("no puede encontrar el archivo"))
            {
                return _versionControlHandler.GetInstallationInstructions();
            }

            // Mensajes de error más descriptivos
            var errorMessage = ex.Message;
            var suggestions = _versionControlHandler.GetErrorSuggestions(errorMessage);

            return $"❌ **Error en {_providerConfig.Name} ({_versionControlHandler.ProviderName})**\n\n" +
                   $"**Mensaje**: {errorMessage}\n\n" +
                   suggestions;
        }
    }

    #region Formatting

    private string FormatResult(string operation, string result, Dictionary<string, string> parameters)
    {
        return _versionControlHandler.GetType()
            .GetMethod("FormatResult", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_versionControlHandler, new object[] { operation, result, parameters }) as string
            ?? result;
    }

    #endregion

    #region Logging Helpers

    private void LogDebug(string message)
    {
        if (_debugMode && _providerConfig.EnableLogging)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[DEBUG] {message}");
            Console.ResetColor();
        }
    }

    private void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }

    #endregion
}
