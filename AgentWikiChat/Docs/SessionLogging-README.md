# ?? Resumen: Sistema de Logging de Sesiones

## ? Implementación Completada

Se ha implementado un **sistema completo de logging de sesiones** que captura toda la interacción de la consola y la guarda en archivos únicos por cada ejecución.

---

## ?? Características Implementadas

### 1. **Captura Automática**
? **Toda la salida de consola** (usuario, bot, debug, errores)  
? **Timestamps precisos** (con milisegundos)  
? **Metadata del sistema** (OS, .NET, usuario, máquina)  
? **Header y footer** estructurados  

### 2. **Archivos Únicos por Sesión**
```
Logs/Sessions/
??? session_20250106_143052.log
??? session_20250106_154823.log
??? session_20250106_161445.log
```

Formato: `{prefix}_{YYYYMMDD}_{HHMMSS}.log`

### 3. **Configuración Flexible**
```json
{
  "Logging": {
    "EnableSessionLogging": true,      // Activar/desactivar
    "LogDirectory": "Logs/Sessions",   // Directorio personalizable
    "LogFilePrefix": "session",        // Prefijo del archivo
    "IncludeTimestamp": true,          // Timestamp por línea
    "SaveOnExit": true                 // Guardar al salir
  }
}
```

---

## ?? Archivos Creados/Modificados

### ? Nuevos Archivos

1. **`Services/ConsoleLogger.cs`** (180 líneas)
   - Clase principal de logging
   - Intercepta `Console.Out`
   - Escribe en archivo en tiempo real
   - TextWriter personalizado

2. **`Docs/SessionLogging-Guide.md`** (500+ líneas)
   - Guía completa de uso
   - Ejemplos de configuración
   - Scripts de análisis
   - Troubleshooting

3. **`analizar-sesion.ps1`** (250+ líneas)
   - Script PowerShell para análisis
   - Estadísticas detalladas
   - Modo resumen y detallado
   - Análisis individual o múltiple

4. **`.gitignore`**
   - Excluye `Logs/` del repositorio
   - Protege archivos sensibles

### ?? Archivos Modificados

1. **`appsettings.json`**
   - Agregada sección `"Logging"`
   - Configuración de logging

2. **`Program.cs`**
   - Inicialización de `ConsoleLogger`
   - Manejo del ciclo de vida
   - Mensaje de confirmación al guardar
   - Try-finally para asegurar guardado

---

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
[14:30:55.234]  ?? Tú> ¿Cuántas tablas hay en la base de datos?
[14:30:56.456]  ?? Bot: En la base de datos hay 42 tablas.
[14:31:10.234]  ?? Tú> /salir
[14:31:10.235] ?? ¡Hasta luego!

?????????????????????????????????????????????????????????????????????????????
Session Ended: 2025-01-06 14:31:10
??????????????????????????????????????????????????????????????????????????????
```

### Elementos del Log

| Elemento | Descripción | Ejemplo |
|----------|-------------|---------|
| **Header** | Metadata de inicio | Fecha, usuario, sistema |
| **Timestamp** | Por cada línea | `[14:30:52.123]` |
| **Contenido** | Salida de consola | Mensajes, respuestas, errores |
| **Footer** | Metadata de cierre | Fecha de fin |

---

## ?? Uso

### Inicio Automático

Al ejecutar el programa:
```bash
dotnet run
```

Verás:
```
?? Session Logging: ? ACTIVADO
?? Log Directory: Logs/Sessions
```

### Guardado Automático

Al salir con `/salir`:
```
?? Log guardado en: Logs/Sessions/session_20250106_143052.log
```

### Análisis de Logs

**Analizar sesión más reciente:**
```powershell
.\analizar-sesion.ps1
```

**Analizar archivo específico:**
```powershell
.\analizar-sesion.ps1 -LogFile "Logs\Sessions\session_20250106_143052.log"
```

**Analizar todas las sesiones:**
```powershell
.\analizar-sesion.ps1 -All
```

**Resumen rápido:**
```powershell
.\analizar-sesion.ps1 -All -Summary
```

---

## ?? Información Capturada

### ? Datos de Sesión
- Inicio y fin de sesión
- Duración total
- Usuario y máquina
- Sistema operativo y .NET version

### ? Interacciones
- Todas las consultas del usuario
- Todas las respuestas del bot
- Mensajes de herramientas (SQL, Wikipedia, etc.)
- Comandos ejecutados (`/memoria`, `/tools`, etc.)

### ? Métricas
- Tiempo de respuesta por consulta
- Número de mensajes en memoria
- Iteraciones del patrón ReAct
- Herramientas invocadas

### ? Debug y Errores
- Logs de debug (si está activado)
- Errores y excepciones
- Stack traces
- Warnings

---

## ?? Casos de Uso

### 1. **Debugging**
Revisar qué pasó en una sesión problemática:
```powershell
# Buscar errores
Select-String "ERROR|?" Logs\Sessions\*.log

# Ver consultas SQL
Select-String "SqlServer" Logs\Sessions\*.log
```

### 2. **Auditoría**
Rastrear consultas realizadas:
```powershell
# Ver todas las consultas del día
Get-Content Logs\Sessions\session_20250106_*.log | Select-String "Tú>"
```

### 3. **Análisis de Performance**
Medir tiempos de respuesta:
```powershell
.\analizar-sesion.ps1 -All
# Muestra tiempo promedio por consulta
```

### 4. **Entrenamiento**
Usar logs como dataset para entrenar modelos:
- Pares pregunta-respuesta
- Ejemplos de uso de herramientas
- Patrones de conversación

---

## ??? Script de Análisis

El script `analizar-sesion.ps1` proporciona:

### ?? Estadísticas por Sesión
- Número de consultas del usuario
- Número de respuestas del bot
- Iteraciones ReAct ejecutadas
- Herramientas usadas (SQL, Wikipedia)
- Tiempo promedio de respuesta
- Errores encontrados

### ?? Resumen Global
Con `-All`, muestra:
- Total de sesiones analizadas
- Total de consultas realizadas
- Total de herramientas usadas
- Tiempo promedio global
- Total de errores

### Ejemplo de Salida:

```
??????????????????????????????????????????????????????????????????
?              ANÁLISIS DE SESIÓN - AgentWikiChat PRO            ?
??????????????????????????????????????????????????????????????????

?? Archivo: session_20250106_143052.log
?? Inicio: 2025-01-06 14:30:52
?? Fin: 2025-01-06 14:35:22
??  Duración: 4m 30s

?? ESTADÍSTICAS:
   ?? Consultas del usuario: 12
   ?? Respuestas del bot: 12
   ?? Iteraciones ReAct: 18

???  HERRAMIENTAS USADAS:
   ?? Consultas SQL: 8
   ?? Búsquedas Wikipedia: 2
   ?? Artículos Wikipedia: 2

? RENDIMIENTO:
   Tiempo promedio por consulta: 1.23s
```

---

## ?? Seguridad

### ?? Datos Sensibles

Los logs pueden contener:
- Consultas del usuario
- Datos de la base de datos
- Información del sistema

### ? Protección Implementada

1. **`.gitignore`**: Excluye `Logs/` del repositorio
2. **Directorio local**: Los logs solo existen localmente
3. **Permisos**: Solo el usuario que ejecuta puede acceder

### ?? Recomendaciones Adicionales

```powershell
# Windows: Proteger carpeta de logs
icacls "Logs\Sessions" /inheritance:r /grant:r "%USERNAME%:(OI)(CI)F"

# Linux/Mac: Restringir permisos
chmod 700 Logs/Sessions
```

---

## ?? Documentación

### Archivos de Documentación

1. **`Docs/SessionLogging-Guide.md`**
   - Guía completa de logging
   - Configuración detallada
   - Troubleshooting
   - Ejemplos avanzados

2. **Este README**
   - Resumen ejecutivo
   - Guía de uso rápido
   - Casos de uso comunes

---

## ? Ventajas del Sistema

| Ventaja | Descripción |
|---------|-------------|
| **?? Trazabilidad** | Todo queda registrado con timestamps |
| **?? Debugging** | Fácil reproducción de problemas |
| **?? Métricas** | Análisis de performance y uso |
| **?? Auditoría** | Registro completo de operaciones |
| **?? Aprendizaje** | Dataset para entrenar/mejorar el agente |
| **?? Configurable** | Activar/desactivar según necesidad |
| **?? Automático** | No requiere intervención manual |

---

## ?? Próximas Mejoras (Opcionales)

- [ ] Niveles de log (INFO, DEBUG, ERROR)
- [ ] Rotación automática de logs antiguos
- [ ] Compresión automática (gzip)
- [ ] Export a JSON/CSV
- [ ] Dashboard web de análisis
- [ ] Búsqueda avanzada en logs
- [ ] Integración con Seq/Elasticsearch
- [ ] Máscara de datos sensibles

---

## ?? Conclusión

El sistema de logging está **completamente funcional** y listo para usar. Proporciona:

? Captura completa de sesiones  
? Archivos únicos por ejecución  
? Análisis detallado con script PowerShell  
? Configuración flexible  
? Documentación completa  
? Protección de datos sensibles  

**Para empezar a usarlo**, simplemente ejecuta:
```bash
dotnet run
```

Los logs se guardarán automáticamente en `Logs/Sessions/` al finalizar cada sesión.

---

**Versión**: 1.0.0  
**Fecha**: 2025-01-06  
**Compatibilidad**: AgentWikiChat PRO v3.3.0+
