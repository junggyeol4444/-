using System.Text.Json;
using MyVocalStudio.Models;

namespace MyVocalStudio.Services;

/// <summary>Bounded snapshot history for destructive editor operations.</summary>
public sealed class ProjectHistory(int capacity = 50)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event EventHandler? Changed;

    public void Record(StudioProject project)
    {
        _undo.Push(JsonSerializer.Serialize(project, Options));
        while (_undo.Count > capacity)
            RemoveOldest(_undo);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public StudioProject? Undo(StudioProject current) => Move(current, _undo, _redo);
    public StudioProject? Redo(StudioProject current) => Move(current, _redo, _undo);

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private StudioProject? Move(StudioProject current, Stack<string> source, Stack<string> destination)
    {
        if (source.Count == 0) return null;
        destination.Push(JsonSerializer.Serialize(current, Options));
        var project = JsonSerializer.Deserialize<StudioProject>(source.Pop(), Options)
                      ?? throw new InvalidDataException("프로젝트 변경 이력을 복원할 수 없습니다.");
        Changed?.Invoke(this, EventArgs.Empty);
        return project;
    }

    private static void RemoveOldest(Stack<string> stack)
    {
        var items = stack.Reverse().Skip(1).ToArray();
        stack.Clear();
        foreach (var item in items) stack.Push(item);
    }
}
