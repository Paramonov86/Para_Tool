using System.Text.Json;
using ParaTool.Core.Artifacts;

namespace ParaTool.App.Services;

/// <summary>
/// Item-level undo/redo store for the Constructor editor. A dumb capped dual-stack of
/// full-state JSON snapshots (the same serialize/deserialize trick used for duplicate).
/// The owning view-model orchestrates when to push (burst coalescing) and applies restores
/// via <see cref="ArtifactDefinition.CopyFrom"/>.
/// </summary>
public sealed class ArtifactUndoStack
{
    public const int MaxDepth = 10;

    private static readonly JsonSerializerOptions JsonOpts = new();

    private readonly List<string> _undo = new();
    private readonly List<string> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public static string Snapshot(ArtifactDefinition art) => JsonSerializer.Serialize(art, JsonOpts);

    public static ArtifactDefinition? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<ArtifactDefinition>(json, JsonOpts); }
        catch { return null; }
    }

    public void PushUndo(string json)
    {
        _undo.Add(json);
        if (_undo.Count > MaxDepth) _undo.RemoveAt(0);
    }

    public void PushRedo(string json)
    {
        _redo.Add(json);
        if (_redo.Count > MaxDepth) _redo.RemoveAt(0);
    }

    public string? PopUndo()
    {
        if (_undo.Count == 0) return null;
        var v = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        return v;
    }

    public string? PopRedo()
    {
        if (_redo.Count == 0) return null;
        var v = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        return v;
    }

    public void ClearRedo() => _redo.Clear();

    public void Clear() { _undo.Clear(); _redo.Clear(); }
}
