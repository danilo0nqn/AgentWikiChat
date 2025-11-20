using Microsoft.Extensions.Configuration;
using System.Text;

namespace AgentWikiChat.Services.VersionControl;

/// <summary>
/// Clase base abstracta para handlers de sistemas de control de versiones.
/// Proporciona funcionalidad común para todos los proveedores.
/// </summary>
public abstract class BaseVersionControlHandler : IVersionControlHandler
{
    protected readonly IConfiguration Configuration;
    protected readonly string RepositoryUrl;
    protected readonly string Username;
    protected readonly string Password;
    protected readonly string WorkingCopyPath;
    protected readonly int CommandTimeout;
    protected readonly bool EnableLogging;
    protected readonly bool DebugMode;

    public abstract string ProviderName { get; }

    protected BaseVersionControlHandler(IConfiguration configuration)
    {
        Configuration = configuration;
        var config = configuration.GetSection("Repository");

        RepositoryUrl = config.GetValue<string>("RepositoryUrl")
            ?? throw new InvalidOperationException("Repository:RepositoryUrl no configurada en appsettings.json");

        Username = config.GetValue<string>("Username") ?? "";
        Password = config.GetValue<string>("Password") ?? "";
        WorkingCopyPath = config.GetValue<string>("WorkingCopyPath") ?? "";
        CommandTimeout = config.GetValue("CommandTimeout", 60);
        EnableLogging = config.GetValue("EnableLogging", true);
        DebugMode = configuration.GetValue("Ui:Debug", true);
    }

    public abstract bool IsClientInstalled();
    public abstract string GetClientVersion();
    public abstract Task<bool> TestConnectionAsync();
    public abstract Task<string> ExecuteReadOnlyOperationAsync(string operation, Dictionary<string, string> parameters);
    public abstract bool IsOperationAllowed(string operation);
    public abstract IEnumerable<string> GetAllowedOperations();
    public abstract string GetInstallationInstructions();
    public abstract string GetErrorSuggestions(string errorMessage);

    #region Logging Helpers

    protected void LogDebug(string message)
    {
        if (DebugMode && EnableLogging)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[DEBUG] {message}");
            Console.ResetColor();
        }
    }

    protected void LogWarning(string message)
    {
        if (EnableLogging)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARN] {message}");
            Console.ResetColor();
        }
    }

    protected void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }

    protected void LogInfo(string message)
    {
        if (EnableLogging)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[INFO] {message}");
            Console.ResetColor();
        }
    }

    protected string TruncateForDisplay(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "\n\n... (truncado)";
    }

    #endregion

    #region Formatting Helpers

    protected string FormatResult(string operation, string result, Dictionary<string, string> parameters)
    {
        var output = new StringBuilder();

        output.AppendLine($"📋 **Resultado de {ProviderName} - {operation.ToUpper()}**\n");
        output.AppendLine($"**Repositorio**: `{RepositoryUrl}`");

        if (parameters.ContainsKey("path") && !string.IsNullOrEmpty(parameters["path"]))
            output.AppendLine($"**Path**: `{parameters["path"]}`");

        if (parameters.ContainsKey("revision") && !string.IsNullOrEmpty(parameters["revision"]) && parameters["revision"] != "HEAD")
            output.AppendLine($"**Revisión**: `{parameters["revision"]}`");

        output.AppendLine();
        output.AppendLine("**Resultado:**");
        output.AppendLine("```");
        output.AppendLine(TruncateForDisplay(result, 5000));
        output.AppendLine("```");

        return output.ToString();
    }

    #endregion
}
