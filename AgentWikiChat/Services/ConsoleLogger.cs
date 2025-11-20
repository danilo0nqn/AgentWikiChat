using System.Text;

namespace AgentWikiChat.Services;

/// <summary>
/// Captura toda la salida de la consola y la guarda en un archivo de log.
/// Cada sesión genera un archivo único con timestamp.
/// Versión mejorada que captura Write y WriteLine correctamente.
/// </summary>
public class ConsoleLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly StringBuilder _logBuffer;
    private readonly StringBuilder _lineBuffer;  // 📝 Buffer para acumular escrituras parciales
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly bool _enabled;
    private readonly StreamWriter? _fileWriter;
    private bool _disposed;
    private readonly object _lockObject = new object();  // 🔒 Para thread-safety

    public ConsoleLogger(string logDirectory, string filePrefix, bool enabled = true)
    {
        _enabled = enabled;
        _logBuffer = new StringBuilder();
        _lineBuffer = new StringBuilder();
        _originalOut = Console.Out;
        _originalError = Console.Error;

        if (!_enabled)
        {
            _logFilePath = string.Empty;
            return;
        }

        // Crear directorio si no existe
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        // Generar nombre de archivo único con timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _logFilePath = Path.Combine(logDirectory, $"{filePrefix}_{timestamp}.log");

        try
        {
            // Crear archivo de log
            _fileWriter = new StreamWriter(_logFilePath, false, Encoding.UTF8)
            {
                AutoFlush = true
            };

            // Escribir encabezado
            WriteHeader();

            // Redirigir Console.Out a nuestro logger
            var logger = new ConsoleLogWriter(this, _originalOut);
            Console.SetOut(logger);
        }
        catch (Exception ex)
        {
            _originalOut.WriteLine($"❌ Error al inicializar ConsoleLogger: {ex.Message}");
            _enabled = false;
        }
    }

    private void WriteHeader()
    {
        if (!_enabled || _fileWriter == null) return;

        _fileWriter.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
        _fileWriter.WriteLine("                      AgentWikiChat PRO - Session Log                      ");
        _fileWriter.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
        _fileWriter.WriteLine();
        _fileWriter.WriteLine($"Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _fileWriter.WriteLine($"Log File: {Path.GetFileName(_logFilePath)}");
        _fileWriter.WriteLine($"Machine: {Environment.MachineName}");
        _fileWriter.WriteLine($"User: {Environment.UserName}");
        _fileWriter.WriteLine($"OS: {Environment.OSVersion}");
        _fileWriter.WriteLine($".NET Version: {Environment.Version}");
        _fileWriter.WriteLine();
        _fileWriter.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
        _fileWriter.WriteLine();
    }

    private void WriteFooter()
    {
        if (!_enabled || _fileWriter == null) return;

        // 🔄 Flush cualquier contenido pendiente en el buffer
        lock (_lockObject)
        {
            if (_lineBuffer.Length > 0)
            {
                FlushLineBuffer();
            }
        }

        _fileWriter.WriteLine();
        _fileWriter.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
        _fileWriter.WriteLine($"Session Ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _fileWriter.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
    }

    /// <summary>
    /// Escribe una línea completa en el log.
    /// </summary>
    public void LogLine(string? value)
    {
        if (!_enabled || _fileWriter == null) return;

        lock (_lockObject)
        {
            try
            {
                // Guardar en buffer (para referencia futura si es necesario)
                _logBuffer.AppendLine(value);

                // Escribir timestamp + contenido
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                _fileWriter.WriteLine($"[{timestamp}] {value}");
            }
            catch
            {
                // Ignorar errores de escritura para no interrumpir la aplicación
            }
        }
    }

    /// <summary>
    /// 📝 Escribe contenido parcial (sin salto de línea) en el buffer.
    /// </summary>
    internal void WritePartial(string? value)
    {
        if (!_enabled || _fileWriter == null || value == null) return;

        lock (_lockObject)
        {
            _lineBuffer.Append(value);
        }
    }

    /// <summary>
    /// 🔄 Flushea el buffer de línea actual al archivo.
    /// </summary>
    internal void FlushLineBuffer()
    {
        if (!_enabled || _fileWriter == null) return;

        lock (_lockObject)
        {
            if (_lineBuffer.Length > 0)
            {
                try
                {
                    var content = _lineBuffer.ToString();
                    _logBuffer.AppendLine(content);

                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    _fileWriter.WriteLine($"[{timestamp}] {content}");

                    _lineBuffer.Clear();
                }
                catch
                {
                    // Ignorar errores de escritura
                }
            }
        }
    }

    /// <summary>
    /// Obtiene el path del archivo de log.
    /// </summary>
    public string GetLogFilePath() => _logFilePath;

    /// <summary>
    /// Obtiene el contenido completo del log desde el buffer.
    /// </summary>
    public string GetLogContent() => _logBuffer.ToString();

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            // Escribir footer
            WriteFooter();

            // Restaurar Console.Out original
            Console.SetOut(_originalOut);
            Console.SetError(_originalError);

            // Cerrar archivo
            _fileWriter?.Dispose();

            if (_enabled && !string.IsNullOrEmpty(_logFilePath))
            {
                _originalOut.WriteLine();
                _originalOut.WriteLine($"✅ Log guardado en: {_logFilePath}");
            }
        }
        catch (Exception ex)
        {
            _originalOut.WriteLine($"❌ Error al cerrar ConsoleLogger: {ex.Message}");
        }

        _disposed = true;
    }

    /// <summary>
    /// TextWriter personalizado que intercepta escrituras a Console.Out.
    /// Versión mejorada que captura Write y WriteLine correctamente.
    /// </summary>
    private class ConsoleLogWriter : TextWriter
    {
        private readonly ConsoleLogger _logger;
        private readonly TextWriter _originalWriter;

        public ConsoleLogWriter(ConsoleLogger logger, TextWriter originalWriter)
        {
            _logger = logger;
            _originalWriter = originalWriter;
        }

        public override Encoding Encoding => Encoding.UTF8;

        // 📝 Capturar Write de un solo carácter
        public override void Write(char value)
        {
            _originalWriter.Write(value);
            _logger.WritePartial(value.ToString());
            
            // Si es newline, flush
            if (value == '\n')
            {
                _logger.FlushLineBuffer();
            }
        }

        // 📝 Capturar Write de string
        public override void Write(string? value)
        {
            _originalWriter.Write(value);
            
            if (!string.IsNullOrEmpty(value))
            {
                _logger.WritePartial(value);
                
                // Si contiene newline, flush
                if (value.Contains('\n'))
                {
                    _logger.FlushLineBuffer();
                }
            }
        }

        // Capturar WriteLine
        public override void WriteLine(string? value)
        {
            _originalWriter.WriteLine(value);
            
            // Agregar contenido pendiente del buffer
            _logger.WritePartial(value);
            _logger.FlushLineBuffer();
        }

        // Capturar WriteLine vacío
        public override void WriteLine()
        {
            _originalWriter.WriteLine();
            _logger.FlushLineBuffer();
        }
    }
}
