# ?? Sistema de Logging de Sesiones - AgentWikiChat PRO

## Descripción

El sistema de logging de sesiones captura **toda la interacción de la consola** (entrada del usuario, respuestas del bot, mensajes de sistema, errores, etc.) y la guarda en archivos de log únicos por cada ejecución.

## ?? Características

### ? Captura Completa
- **Entrada del usuario**: Todas las preguntas y comandos
- **Respuestas del bot**: Respuestas completas del agente
- **Mensajes de herramientas**: Salida de Wikipedia, SQL, etc.
- **Logs de debug**: Si el modo debug está activado
- **Errores**: Stack traces y mensajes de error
- **Métricas**: Tiempos de ejecución, contadores

### ? Archivo Único por Sesión
Cada ejecución genera un archivo con nombre único:
```
session_20250106_143052.log
session_20250106_154823.log
session_20250106_161445.log
```

### ? Metadata Incluida
Cada log contiene:
- Timestamp de inicio y fin de sesión
- Información del sistema (OS, .NET version, usuario, máquina)
- Timestamp por cada línea de log
- Formato legible y estructurado

## ?? Configuración

### appsettings.json

```json
{
  "Logging": {
    "EnableSessionLogging": true,
    "LogDirectory": "Logs/Sessions",
    "LogFilePrefix": "session",
    "IncludeTimestamp": true,
    "SaveOnExit": true
  }
}
```

### Parámetros

| Parámetro | Descripción | Por defecto |
|-----------|-------------|-------------|
| `EnableSessionLogging` | Habilita/deshabilita el logging | `true` |
| `LogDirectory` | Directorio donde se guardan los logs | `"Logs/Sessions"` |
| `LogFilePrefix` | Prefijo del nombre de archivo | `"session"` |
| `IncludeTimestamp` | Incluye timestamp en cada línea | `true` |
| `SaveOnExit` | Guarda automáticamente al salir | `true` |

## ?? Estructura de Archivos

### Directorio de Logs

```
AgentWikiChat/
??? Logs/
?   ??? Sessions/
?       ??? session_20250106_143052.log
?       ??? session_20250106_154823.log
?       ??? session_20250106_161445.log
```

### Formato de Nombre de Archivo

```
{LogFilePrefix}_{YYYYMMDD}_{HHMMSS}.log

Ejemplos:
- session_20250106_143052.log
- chat_20250106_154823.log
- agent_20250106_161445.log
```

## ?? Formato del Log

### Estructura Completa

```
??????????????????????????????????????????????????????????????????????????????
?                      AgentWikiChat PRO - Session Log                      ?
??????????????????????????????????????????????????????????????????????????????

Session Started: 2025-01-06 14:30:52
Log File: session_20250106_143052.log
Machine: DESKTOP-ABC123
User: Juan
OS: Microsoft Windows NT 10.0.19045.0
.NET Version: 9.0.0

?????????????????????????????????????????????????????????????????????????????

[14:30:52.123] === AgentWikiChat PRO v3.3.0 ===
[14:30:52.125] ?? Proveedor IA: LM Studio (Local)
[14:30:52.126] ?? Sistema: Multi-Provider Tool Calling + ReAct Pattern
[14:30:52.127] ?? Session Logging: ? ACTIVADO
[14:30:52.128] ?? Log Directory: Logs/Sessions
[14:30:52.129] ?? Comandos: /salir, /memoria, /limpiar, /tools, /config
[14:30:52.130] 
[14:30:55.234]  ?? Tú> ¿Cuántas tablas hay en la base de datos?
[14:30:55.456]  ?? Bot: 
[14:30:55.457] ?? Iniciando ReAct Loop (máx 10 iteraciones)
[14:30:56.123] [DEBUG] [SqlServer] Query recibida: SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
[14:30:56.456] ?? Resultado de la Consulta SQL
[14:30:56.457] En la base de datos hay 42 tablas.
[14:30:56.500] 
[14:30:56.501] ? Tiempo: 1.04s | ?? Mensajes en memoria: 4
[14:31:10.234]  ?? Tú> /salir
[14:31:10.235] ?? ¡Hasta luego!

?????????????????????????????????????????????????????????????????????????????
Session Ended: 2025-01-06 14:31:10
??????????????????????????????????????????????????????????????????????????????
```

### Formato de Timestamps

Cada línea incluye timestamp con milisegundos:
```
[HH:mm:ss.fff] Contenido del mensaje
```

Ejemplos:
```
[14:30:52.123] Mensaje del sistema
[14:30:55.234] Entrada del usuario
[14:30:56.789] Respuesta del bot
```

## ?? Uso

### Inicio Automático

El logging se inicia automáticamente al ejecutar el programa:

```bash
dotnet run
```

Verás en la consola:
```
?? Session Logging: ? ACTIVADO
?? Log Directory: Logs/Sessions
```

### Fin Automático

Al salir del programa (comando `/salir` o `Ctrl+C`), el log se guarda automáticamente:

```
?? Log guardado en: Logs/Sessions/session_20250106_143052.log
```

### Deshabilitar Logging

En `appsettings.json`:
```json
{
  "Logging": {
    "EnableSessionLogging": false
  }
}
```

## ?? Casos de Uso

### 1. Debugging

Revisar interacciones problemáticas:
```bash
# Buscar errores en logs
grep "ERROR" Logs/Sessions/*.log

# Buscar consultas SQL
grep "SqlServer" Logs/Sessions/*.log

# Buscar sesiones de una fecha
ls Logs/Sessions/session_20250106_*.log
```

### 2. Auditoría

Rastrear qué consultas se hicieron y cuándo:
```bash
# Ver todas las consultas de un día
cat Logs/Sessions/session_20250106_*.log | grep "Tú>"
```

### 3. Análisis de Performance

Revisar tiempos de respuesta:
```bash
grep "Tiempo:" Logs/Sessions/*.log
```

### 4. Entrenamiento de Modelos

Usar logs como dataset:
- Pares pregunta-respuesta
- Contexto de conversaciones
- Ejemplos de uso de herramientas

## ??? Gestión de Logs

### Rotación Manual

Crear script para mover logs antiguos:

```powershell
# rotar-logs.ps1
$daysToKeep = 30
$logDir = "Logs/Sessions"
$archiveDir = "Logs/Archive"

Get-ChildItem $logDir -Filter "*.log" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$daysToKeep) } |
    Move-Item -Destination $archiveDir
```

### Limpieza Automática

Agregar en `appsettings.json` (funcionalidad futura):
```json
{
  "Logging": {
    "RetentionDays": 30,
    "AutoCleanup": true,
    "MaxLogSizeMB": 100
  }
}
```

### Compresión

Comprimir logs antiguos:
```bash
# Linux/Mac
gzip Logs/Sessions/session_*.log

# Windows PowerShell
Compress-Archive -Path "Logs/Sessions/*.log" -DestinationPath "Logs/archive.zip"
```

## ?? Estadísticas de Sesión

### Información Incluida

- **Inicio de sesión**: Fecha y hora exacta
- **Fin de sesión**: Fecha y hora exacta
- **Duración total**: Calculable (fin - inicio)
- **Número de interacciones**: Contando líneas con "Tú>"
- **Herramientas usadas**: Wikipedia, SQL, etc.
- **Errores ocurridos**: Contando líneas con "ERROR" o "?"

### Script de Análisis

```powershell
# analizar-sesion.ps1
param([string]$logFile)

$content = Get-Content $logFile
$userQueries = ($content | Select-String "Tú>").Count
$botResponses = ($content | Select-String "Bot:").Count
$errors = ($content | Select-String "ERROR|?").Count
$sqlQueries = ($content | Select-String "SqlServer").Count
$wikiSearches = ($content | Select-String "Wikipedia").Count

Write-Host "?? Análisis de $logFile"
Write-Host "   Consultas del usuario: $userQueries"
Write-Host "   Respuestas del bot: $botResponses"
Write-Host "   Errores: $errors"
Write-Host "   Consultas SQL: $sqlQueries"
Write-Host "   Búsquedas en Wikipedia: $wikiSearches"
```

## ?? Seguridad y Privacidad

### Datos Sensibles

?? **IMPORTANTE**: Los logs pueden contener:
- Consultas del usuario
- Datos de la base de datos
- Información del sistema
- Errores con stack traces

### Recomendaciones

1. **No commitear logs** al repositorio:
```gitignore
# .gitignore
Logs/
*.log
```

2. **Proteger directorio de logs**:
```bash
# Linux/Mac
chmod 700 Logs/Sessions

# Windows: Usar propiedades de carpeta para limitar acceso
```

3. **Sanitizar datos sensibles** antes de compartir logs:
```csharp
// Funcionalidad futura: modo "safe" que enmascara datos
"Logging": {
  "MaskSensitiveData": true,
  "MaskedPatterns": ["password", "apikey", "token"]
}
```

## ?? Testing

### Verificar que Logging Funciona

```bash
# 1. Ejecutar aplicación
dotnet run

# 2. Hacer algunas consultas
# 3. Salir con /salir
# 4. Verificar que se creó el log
ls Logs/Sessions/

# 5. Ver contenido
cat Logs/Sessions/session_*.log | tail -20
```

### Verificar Formato

```bash
# Verificar que tiene header
head -20 Logs/Sessions/session_*.log

# Verificar que tiene footer
tail -5 Logs/Sessions/session_*.log

# Verificar timestamps
grep "\[.*\]" Logs/Sessions/session_*.log | head
```

## ?? Funcionalidades Futuras

- [ ] Configuración de nivel de log (INFO, DEBUG, ERROR)
- [ ] Rotación automática de logs
- [ ] Compresión automática de logs antiguos
- [ ] Export a JSON/CSV
- [ ] Dashboard de análisis de logs
- [ ] Integración con sistemas de logging (Seq, Elasticsearch)
- [ ] Búsqueda avanzada en logs
- [ ] Estadísticas en tiempo real

## ?? Troubleshooting

### No se crea el archivo de log

**Problema**: El archivo no aparece en `Logs/Sessions/`

**Soluciones**:
1. Verificar que `EnableSessionLogging` esté en `true`
2. Verificar permisos de escritura en el directorio
3. Revisar logs de error en consola al inicio

### Log vacío o incompleto

**Problema**: El archivo existe pero está vacío o le falta contenido

**Soluciones**:
1. Asegurar que el programa termine normalmente (con `/salir`)
2. No usar `Ctrl+C` forzado (usar graceful shutdown)
3. Verificar que no haya excepciones al escribir

### Emojis no se ven bien en el log

**Problema**: Caracteres raros en lugar de emojis

**Soluciones**:
1. Abrir con editor que soporte UTF-8 (VS Code, Notepad++, etc.)
2. No usar Notepad de Windows (no soporta UTF-8 correctamente)
3. En Linux/Mac: usar `less -R` o `cat`

## ?? Referencias

- [TextWriter Class - Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/api/system.io.textwriter)
- [Console Redirection - .NET](https://docs.microsoft.com/en-us/dotnet/api/system.console.setout)
- [File Logging Best Practices](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)

---

**Versión**: 1.0.0  
**Última actualización**: 2025-01-06  
**Compatibilidad**: AgentWikiChat PRO v3.3.0+
