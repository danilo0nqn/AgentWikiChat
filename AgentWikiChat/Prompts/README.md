# System Prompts - Gu�a de Uso

Este directorio contiene diferentes System Prompts que definen el comportamiento y personalidad del agente.

## ?? Archivos Disponibles

### 1. `SystemPrompt.txt` (Por Defecto)
**Personalidad**: Chuleta - Asistente amigable en espa�ol  
**Tono**: Informal, cercano, uso de voseo argentino  
**Ideal para**: Interacciones casuales, usuarios que prefieren espa�ol rioplatense  

**Caracter�sticas**:
- ? Explicaciones detalladas de herramientas
- ? Ejemplos concretos de uso
- ? Tono amigable y conversacional
- ? Instrucciones completas del patr�n ReAct

---

### 2. `SystemPrompt_Technical.txt` (T�cnico - English)
**Personalidad**: Technical Assistant  
**Tono**: Profesional, t�cnico, formal  
**Ideal para**: Entornos corporativos, documentaci�n t�cnica, usuarios que prefieren ingl�s  

**Caracter�sticas**:
- ? Lenguaje t�cnico preciso
- ? Enfoque en mejores pr�cticas
- ? Ejemplos de arquitectura
- ? Consideraciones de dise�o

---

### 3. `SystemPrompt_Minimal.txt` (Minimalista)
**Personalidad**: Asistente conciso  
**Tono**: Directo, sin rodeos  
**Ideal para**: Usuarios avanzados, escenarios con l�mite de tokens, testing  

**Caracter�sticas**:
- ? Instrucciones m�nimas
- ? Sin ejemplos extensos
- ? Menor consumo de tokens
- ? Respuestas m�s directas

---

## ?? C�mo Cambiar el System Prompt

### Opci�n 1: Editar `appsettings.json`

```json
{
  "AppSettings": {
    "SystemPromptPath": "Prompts/SystemPrompt_Technical.txt"
  }
}
```

### Opci�n 2: Variable de Entorno

```bash
# Windows (PowerShell)
$env:AppSettings__SystemPromptPath="Prompts/SystemPrompt_Minimal.txt"

# Linux/Mac
export AppSettings__SystemPromptPath="Prompts/SystemPrompt_Technical.txt"
```

### Opci�n 3: Crear Tu Propio Prompt

1. Crea un nuevo archivo `.txt` en este directorio
2. Escribe tu prompt personalizado
3. Actualiza `appsettings.json` con la ruta

**Ejemplo**: `SystemPrompt_Custom.txt`

---

## ?? Plantilla para Crear Prompts Personalizados

```text
[Descripci�n de personalidad y rol del asistente]

??? HERRAMIENTAS DISPONIBLES:

1. **Wikipedia** (search_wikipedia_titles, get_wikipedia_article):
   [Descripci�n...]

2. **Base de Datos** (query_database):
   [Descripci�n...]

3. **Repositorios** (svn_operation, git_operation, github_operation):
   [Descripci�n...]

4. **RAG** (search_documents):
   [Descripci�n...]

?? INSTRUCCIONES:
- [Instrucci�n 1]
- [Instrucci�n 2]
- [Instrucci�n N]

?? EJEMPLOS:
- [Ejemplo 1]
- [Ejemplo 2]
```

---

## ?? Recomendaciones por Caso de Uso

| Caso de Uso | Prompt Recomendado | Raz�n |
|-------------|-------------------|-------|
| Desarrollo personal | `SystemPrompt.txt` | Tono amigable, espa�ol |
| Entorno corporativo | `SystemPrompt_Technical.txt` | Profesional, ingl�s |
| Testing/Debugging | `SystemPrompt_Minimal.txt` | Menor overhead |
| Demos p�blicas | `SystemPrompt_Technical.txt` | Universal, profesional |
| Usuarios argentinos | `SystemPrompt.txt` | Voseo, localizado |

---

## ?? Notas Importantes

1. **Codificaci�n**: Todos los archivos deben estar en **UTF-8** para soportar emojis y caracteres especiales
2. **Tama�o**: Manten� los prompts razonables (<4KB) para no consumir demasiados tokens
3. **Validaci�n**: Si el archivo no existe, el sistema usa un prompt por defecto
4. **Hot Reload**: Los cambios en `appsettings.json` se recargan autom�ticamente, pero necesit�s reiniciar la app para cambiar el prompt

---

## ?? Mejores Pr�cticas

### ? DO:
- Usa instrucciones claras y espec�ficas
- Inclu� ejemplos concretos
- Define el tono y personalidad
- Especifica limitaciones de seguridad
- Manten� consistencia en el formato

### ? DON'T:
- No uses prompts demasiado largos (>8KB)
- No incluyas informaci�n sensible
- No uses formatos binarios (solo texto plano)
- No omitas las descripciones de herramientas

---

## ?? Recursos Adicionales

- [OpenAI Prompt Engineering Guide](https://platform.openai.com/docs/guides/prompt-engineering)
- [Anthropic Claude Prompt Guide](https://docs.anthropic.com/claude/docs/prompt-engineering)
- [ReAct Pattern Paper](https://arxiv.org/abs/2210.03629)

---

**�ltima actualizaci�n**: v3.7.0  
**Autor**: AgentWikiChat Team
