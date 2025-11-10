# ?? Guía de System Prompt - AgentWikiChat PRO

## Descripción

El `SystemPrompt` es la instrucción inicial que define el comportamiento, personalidad y conocimiento del agente sobre sus capacidades. Es fundamental para que el modelo de IA entienda qué herramientas tiene disponibles y cómo usarlas.

## ?? System Prompt Actual

El prompt actual define a "Chuleta" como un asistente experto con acceso a:

### ??? Herramientas Disponibles

#### 1. **Wikipedia** (Búsqueda de Conocimiento)
- **Tools**: `search_wikipedia_titles`, `get_wikipedia_article`
- **Uso**: Información enciclopédica, biografías, conceptos técnicos, historia, etc.
- **Workflow**: 
  1. Buscar títulos con `search_wikipedia_titles`
  2. Analizar resultados y elegir el más relevante
  3. Obtener contenido completo con `get_wikipedia_article`
  4. Responder al usuario con la información

#### 2. **SQL Server** (Base de Datos)
- **Tool**: `query_database`
- **Uso**: Consultas a bases de datos, reportes, estadísticas, análisis de datos
- **Restricción**: Solo consultas SELECT (lectura)
- **Ejemplos**:
  - Contar registros
  - Listar datos con filtros
  - Hacer JOINs entre tablas
  - Agregaciones (SUM, AVG, COUNT, etc.)

## ?? Estructura del System Prompt

```
1. IDENTIDAD
   - Nombre del asistente
   - Rol/propósito

2. HERRAMIENTAS DISPONIBLES
   - Lista de tools con sus nombres
   - Descripción de cada herramienta
   - Cuándo usar cada una
   - Restricciones/limitaciones

3. INSTRUCCIONES DE USO
   - Cómo evaluar qué herramienta usar
   - Workflow sugerido
   - Manejo de errores
   - Formato de respuestas

4. EJEMPLOS PRÁCTICOS
   - Casos de uso comunes
   - Formato: "Usuario: X ? Acción: Y"
```

## ?? Personalizaciones Comunes

### Ejemplo 1: Asistente Enfocado en Datos

```json
{
  "SystemPrompt": "Sos DataBot, un asistente especializado en análisis de datos. Tu principal herramienta es query_database para SQL Server. Cuando el usuario pida información:\n\n1. Primero preguntá si se refiere a datos en la base de datos\n2. Si es así, construí una consulta SQL SELECT apropiada\n3. Ejecutá la query con query_database\n4. Presenta los resultados de forma clara, usando tablas o estadísticas\n5. Ofrece insights adicionales si es relevante\n\n?? IMPORTANTE: Solo podés hacer consultas SELECT, sin modificaciones.\n\n?? Si el usuario busca información que NO está en la BD, sugerí usar Wikipedia."
}
```

### Ejemplo 2: Asistente Educativo

```json
{
  "SystemPrompt": "Sos EduBot, un asistente educativo. Cuando el usuario pregunte sobre conceptos:\n\n1. Usá search_wikipedia_titles para encontrar artículos relevantes\n2. Obtené el contenido con get_wikipedia_article\n3. Explicá el concepto de forma didáctica\n4. Agregá ejemplos prácticos\n\nSi el usuario pregunta sobre datos específicos (ej: estadísticas), usá query_database para consultar la base de datos educativa."
}
```

### Ejemplo 3: Asistente Multidominio (Actual)

```json
{
  "SystemPrompt": "Sos Chuleta, un asistente experto con acceso a múltiples herramientas. Evaluá la consulta del usuario y elegí la herramienta apropiada:\n\n?? Wikipedia ? Información enciclopédica\n?? SQL Server ? Datos en la base de datos\n\nPodés usar múltiples herramientas si es necesario (patrón ReAct)."
}
```

## ?? Testing del System Prompt

### Preguntas de Prueba

#### Para Wikipedia:
```
? "¿Qué es C#?"
? "Buscá información sobre la Segunda Guerra Mundial"
? "Dame la biografía de Albert Einstein"
```

#### Para SQL Server:
```
? "¿Cuántos usuarios hay en la base de datos?"
? "Dame las últimas 10 ventas"
? "Muestra el total de pedidos por mes del último año"
? "Lista las tablas disponibles en la BD"
```

#### Preguntas Mixtas (Evalúa razonamiento):
```
? "¿Qué es SQL Server?" ? Debería usar Wikipedia
? "¿Cuántas tablas tenemos?" ? Debería usar query_database
? "Explica qué es un JOIN y muestra un ejemplo de nuestra BD" ? Debería usar ambas
```

## ?? Métricas de Efectividad

Un buen System Prompt debe lograr:

| Métrica | Objetivo | Cómo Medir |
|---------|----------|------------|
| **Selección correcta de tool** | >90% | ¿El agente elige la herramienta apropiada? |
| **Uso eficiente de herramientas** | >85% | ¿Usa la cantidad mínima de tool calls? |
| **Manejo de errores** | >95% | ¿Responde apropiadamente a errores? |
| **Claridad de respuestas** | >90% | ¿Las respuestas son comprensibles? |

## ?? Consejos de Optimización

### 1. **Sé Específico**
? Malo: "Usa las herramientas disponibles"
? Bueno: "Cuando el usuario pregunte sobre datos, usa query_database con una consulta SELECT"

### 2. **Incluye Ejemplos**
Los modelos aprenden mejor con ejemplos concretos:
```
Usuario: "¿cuántos clientes hay?" 
? query_database("SELECT COUNT(*) FROM Customers")
```

### 3. **Define Restricciones Claramente**
```
?? IMPORTANTE: Solo consultas SELECT, sin UPDATE/DELETE/INSERT
```

### 4. **Usa Formato Estructurado**
```
??? HERRAMIENTAS:
1. Tool A ? Uso X
2. Tool B ? Uso Y

?? INSTRUCCIONES:
- Paso 1
- Paso 2
```

### 5. **Incluye Manejo de Errores**
```
Si una herramienta falla:
1. Explicá el error al usuario
2. Sugerí alternativas
3. Intentá con otra herramienta si es relevante
```

## ?? Próximas Herramientas (Planificadas)

Cuando se implementen, agregar al SystemPrompt:

### SVN Repository
```
3. **Control de Versiones SVN** (svn_operation):
   - Consultar logs, diffs, historial de commits
   - Información de revisiones
   - Estado de archivos
```

### RAG Documents
```
4. **Búsqueda en Documentos** (search_documents):
   - Búsqueda en documentos locales (PDF, DOCX, TXT)
   - Bases vectoriales
   - Búsqueda web avanzada
```

## ?? Template Recomendado

```json
{
  "SystemPrompt": "Sos [NOMBRE], un asistente [ROL/ESPECIALIZACIÓN].\n\n??? HERRAMIENTAS DISPONIBLES:\n\n[LISTA DE TOOLS CON DESCRIPCIÓN]\n\n?? INSTRUCCIONES:\n[WORKFLOW Y REGLAS]\n\n?? RESTRICCIONES:\n[LIMITACIONES IMPORTANTES]\n\n?? EJEMPLOS:\n[CASOS DE USO COMUNES]"
}
```

## ?? Versionado del Prompt

Es recomendable versionar cambios importantes:

```json
{
  "SystemPromptVersion": "2.0.0",
  "SystemPromptChangelog": [
    "2.0.0 - Agregado soporte para query_database (SQL Server)",
    "1.5.0 - Mejorado workflow de Wikipedia con ejemplos",
    "1.0.0 - Prompt inicial con Wikipedia"
  ]
}
```

## ?? Mejores Prácticas

1. **Actualiza el prompt al agregar nuevas herramientas**
2. **Prueba cambios con diferentes tipos de consultas**
3. **Mantén el prompt conciso pero completo**
4. **Usa lenguaje claro y directo**
5. **Incluye restricciones de seguridad visibles**
6. **Proporciona ejemplos concretos**
7. **Define personalidad del asistente**
8. **Especifica formato de respuestas esperado**

## ?? Referencias

- [OpenAI Best Practices for Prompts](https://platform.openai.com/docs/guides/prompt-engineering)
- [Anthropic Prompt Engineering Guide](https://docs.anthropic.com/claude/docs/prompt-engineering)
- [ReAct Pattern Paper](https://arxiv.org/abs/2210.03629)

---

**Última actualización**: 2025  
**Versión del documento**: 1.0.0  
**Compatibilidad**: AgentWikiChat PRO v3.3.0+
