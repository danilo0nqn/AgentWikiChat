# Script de análisis de logs de sesión - AgentWikiChat PRO
# Uso: .\analizar-sesion.ps1 -LogFile "Logs\Sessions\session_20250106_143052.log"
# O para analizar todas las sesiones: .\analizar-sesion.ps1 -All

param(
    [string]$LogFile = "",
    [switch]$All = $false,
    [switch]$Summary = $false
)

function Analyze-LogFile {
    param([string]$FilePath)

    if (!(Test-Path $FilePath)) {
        Write-Host "? Archivo no encontrado: $FilePath" -ForegroundColor Red
        return
    }

    $content = Get-Content $FilePath -Encoding UTF8

    # Extraer metadata
    $sessionStart = ($content | Select-String "Session Started:").ToString() -replace "^.*Session Started: ", ""
    $sessionEnd = ($content | Select-String "Session Ended:").ToString() -replace "^.*Session Ended: ", ""
    
    # Calcular estadísticas
    $userQueries = ($content | Select-String "?? Tú>").Count
    $botResponses = ($content | Select-String "?? Bot:").Count
    $errors = ($content | Select-String "ERROR|?").Count
    $sqlQueries = ($content | Select-String "\[SqlServer\] Query recibida:").Count
    $wikiSearches = ($content | Select-String "search_wikipedia_titles").Count
    $wikiArticles = ($content | Select-String "get_wikipedia_article").Count
    $reactIterations = ($content | Select-String "Iteración \d+/\d+").Count
    
    # Calcular tiempos
    $avgTime = 0
    $times = $content | Select-String "? Tiempo: ([\d\.]+)s" | ForEach-Object {
        if ($_.Matches[0].Groups[1].Value) {
            [double]$_.Matches[0].Groups[1].Value
        }
    }
    
    if ($times.Count -gt 0) {
        $avgTime = ($times | Measure-Object -Average).Average
    }

    # Mostrar resultados
    Write-Host "`n??????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
    Write-Host "?              ANÁLISIS DE SESIÓN - AgentWikiChat PRO            ?" -ForegroundColor Cyan
    Write-Host "??????????????????????????????????????????????????????????????????" -ForegroundColor Cyan
    
    Write-Host "`n?? Archivo: " -NoNewline
    Write-Host (Split-Path $FilePath -Leaf) -ForegroundColor Yellow
    
    if ($sessionStart) {
        Write-Host "?? Inicio: " -NoNewline
        Write-Host $sessionStart -ForegroundColor Green
    }
    
    if ($sessionEnd) {
        Write-Host "?? Fin: " -NoNewline
        Write-Host $sessionEnd -ForegroundColor Green
        
        # Calcular duración
        try {
            $start = [DateTime]::Parse($sessionStart)
            $end = [DateTime]::Parse($sessionEnd)
            $duration = $end - $start
            Write-Host "??  Duración: " -NoNewline
            Write-Host "$($duration.Minutes)m $($duration.Seconds)s" -ForegroundColor Magenta
        } catch {}
    }

    Write-Host "`n?? ESTADÍSTICAS:" -ForegroundColor Cyan
    Write-Host "   ?? Consultas del usuario: " -NoNewline
    Write-Host $userQueries -ForegroundColor Yellow
    
    Write-Host "   ?? Respuestas del bot: " -NoNewline
    Write-Host $botResponses -ForegroundColor Yellow
    
    if ($reactIterations -gt 0) {
        Write-Host "   ?? Iteraciones ReAct: " -NoNewline
        Write-Host $reactIterations -ForegroundColor Yellow
    }
    
    Write-Host "`n???  HERRAMIENTAS USADAS:" -ForegroundColor Cyan
    
    if ($sqlQueries -gt 0) {
        Write-Host "   ?? Consultas SQL: " -NoNewline
        Write-Host $sqlQueries -ForegroundColor Green
    }
    
    if ($wikiSearches -gt 0) {
        Write-Host "   ?? Búsquedas Wikipedia: " -NoNewline
        Write-Host $wikiSearches -ForegroundColor Green
    }
    
    if ($wikiArticles -gt 0) {
        Write-Host "   ?? Artículos Wikipedia: " -NoNewline
        Write-Host $wikiArticles -ForegroundColor Green
    }
    
    if ($sqlQueries -eq 0 -and $wikiSearches -eq 0) {
        Write-Host "   ??  No se usaron herramientas" -ForegroundColor DarkGray
    }

    if ($avgTime -gt 0) {
        Write-Host "`n? RENDIMIENTO:" -ForegroundColor Cyan
        Write-Host "   Tiempo promedio por consulta: " -NoNewline
        Write-Host ("{0:N2}s" -f $avgTime) -ForegroundColor Yellow
    }

    if ($errors -gt 0) {
        Write-Host "`n? ERRORES:" -ForegroundColor Red
        Write-Host "   Total de errores: " -NoNewline
        Write-Host $errors -ForegroundColor Red
    }

    Write-Host ""

    # Devolver objeto con estadísticas para resumen
    return [PSCustomObject]@{
        File = (Split-Path $FilePath -Leaf)
        StartTime = $sessionStart
        UserQueries = $userQueries
        BotResponses = $botResponses
        SQLQueries = $sqlQueries
        WikiSearches = $wikiSearches
        Errors = $errors
        AvgTime = $avgTime
    }
}

# Ejecución principal
if ($All) {
    $logDir = "Logs\Sessions"
    
    if (!(Test-Path $logDir)) {
        Write-Host "? Directorio de logs no encontrado: $logDir" -ForegroundColor Red
        exit 1
    }

    $logFiles = Get-ChildItem $logDir -Filter "*.log" | Sort-Object LastWriteTime -Descending

    if ($logFiles.Count -eq 0) {
        Write-Host "??  No se encontraron archivos de log en $logDir" -ForegroundColor Yellow
        exit 0
    }

    Write-Host "?? Analizando $($logFiles.Count) sesiones..." -ForegroundColor Cyan
    Write-Host ""

    $results = @()
    
    foreach ($file in $logFiles) {
        if (!$Summary) {
            $result = Analyze-LogFile -FilePath $file.FullName
            $results += $result
        } else {
            # Modo resumen: solo información básica
            $content = Get-Content $file.FullName -Encoding UTF8
            $queries = ($content | Select-String "?? Tú>").Count
            $errors = ($content | Select-String "ERROR|?").Count
            
            Write-Host "$($file.Name): " -NoNewline
            Write-Host "$queries consultas" -NoNewline -ForegroundColor Yellow
            
            if ($errors -gt 0) {
                Write-Host ", $errors errores" -NoNewline -ForegroundColor Red
            }
            
            Write-Host " ($($file.LastWriteTime.ToString('yyyy-MM-dd HH:mm')))"
        }
    }

    if (!$Summary -and $results.Count -gt 0) {
        # Mostrar resumen global
        Write-Host "`n??????????????????????????????????????????????????????????????????" -ForegroundColor Green
        Write-Host "?                      RESUMEN GLOBAL                            ?" -ForegroundColor Green
        Write-Host "??????????????????????????????????????????????????????????????????" -ForegroundColor Green
        
        $totalQueries = ($results | Measure-Object -Property UserQueries -Sum).Sum
        $totalSQL = ($results | Measure-Object -Property SQLQueries -Sum).Sum
        $totalWiki = ($results | Measure-Object -Property WikiSearches -Sum).Sum
        $totalErrors = ($results | Measure-Object -Property Errors -Sum).Sum
        $avgResponseTime = ($results | Where-Object { $_.AvgTime -gt 0 } | Measure-Object -Property AvgTime -Average).Average

        Write-Host "`n?? Total de sesiones: " -NoNewline
        Write-Host $results.Count -ForegroundColor Yellow
        
        Write-Host "?? Total de consultas: " -NoNewline
        Write-Host $totalQueries -ForegroundColor Yellow
        
        Write-Host "?? Total de consultas SQL: " -NoNewline
        Write-Host $totalSQL -ForegroundColor Green
        
        Write-Host "?? Total de búsquedas Wikipedia: " -NoNewline
        Write-Host $totalWiki -ForegroundColor Green
        
        if ($avgResponseTime -gt 0) {
            Write-Host "? Tiempo promedio global: " -NoNewline
            Write-Host ("{0:N2}s" -f $avgResponseTime) -ForegroundColor Yellow
        }
        
        if ($totalErrors -gt 0) {
            Write-Host "? Total de errores: " -NoNewline
            Write-Host $totalErrors -ForegroundColor Red
        }
        
        Write-Host ""
    }

} elseif ($LogFile) {
    Analyze-LogFile -FilePath $LogFile
} else {
    # Analizar el log más reciente
    $logDir = "Logs\Sessions"
    
    if (!(Test-Path $logDir)) {
        Write-Host "? Directorio de logs no encontrado: $logDir" -ForegroundColor Red
        Write-Host "?? Uso: .\analizar-sesion.ps1 -LogFile `"ruta\al\archivo.log`"" -ForegroundColor Yellow
        exit 1
    }

    $latestLog = Get-ChildItem $logDir -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if ($latestLog) {
        Write-Host "?? Analizando sesión más reciente..." -ForegroundColor Cyan
        Analyze-LogFile -FilePath $latestLog.FullName
    } else {
        Write-Host "??  No se encontraron archivos de log" -ForegroundColor Yellow
    }
}

Write-Host "? Análisis completado`n" -ForegroundColor Green
