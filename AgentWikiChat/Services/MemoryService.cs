using System.Collections.Concurrent;
using AgentWikiChat.Models;

namespace AgentWikiChat.Services;

public class MemoryService
{
    // Memoria global (historial de diálogo general)
    public List<Message> Global { get; } = new();

    // Memoria por módulo (por intent o contexto específico)
    private readonly ConcurrentDictionary<string, List<Message>> _modules = new();

    public void AddToGlobal(string role, string content)
    {
        Global.Add(new Message(role, content));
    }

    public void AddToModule(string module, string role, string content)
    {
        var list = _modules.GetOrAdd(module, _ => new List<Message>());
        list.Add(new Message(role, content));
    }

    public IEnumerable<Message> GetModuleContext(string module)
    {
        return _modules.TryGetValue(module, out var msgs) ? msgs : new List<Message>();
    }

    public IEnumerable<string> GetModules()
    {
        return _modules.Keys;
    }

    public void ClearGlobal()
    {
        Global.Clear();
    }

    public void ClearModule(string module)
    {
        _modules.TryRemove(module, out _);
    }

    public void ClearAll()
    {
        Global.Clear();
        _modules.Clear();
    }

    public void PrintDebug()
    {
        Console.WriteLine("\n🧠 Memoria actual:");
        Console.WriteLine($"   Global: {Global.Count} mensajes");

        if (_modules.Any())
        {
            foreach (var kv in _modules)
                Console.WriteLine($"   └─ {kv.Key}: {kv.Value.Count} mensajes");
        }

        Console.WriteLine();
    }

    public int GetTotalMessageCount()
    {
        return Global.Count + _modules.Values.Sum(m => m.Count);
    }
}
