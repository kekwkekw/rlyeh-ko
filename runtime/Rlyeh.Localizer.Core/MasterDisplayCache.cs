using System.Collections.Concurrent;

namespace Rlyeh.Localizer.Core;

// Native domain models retain GetText() results for the entire login session.
// Keep their bindings Japanese and publish reviewed, fully formatted display
// pairs separately for the common UI renderer. Conflicting pairs fail closed.
public sealed class MasterDisplayCache
{
    private sealed record Entry(string? Target);
    private readonly ConcurrentDictionary<string, Entry> entries = new();
    public bool Register(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || source == target) return true;
        var value = entries.AddOrUpdate(source, _ => new(target),
            (_, old) => old.Target == target ? old : new(null));
        return value.Target != null;
    }
    public bool TryGet(string source, out string target)
    {
        if (entries.TryGetValue(source, out var entry) && entry.Target != null)
        { target = entry.Target; return true; }
        target = "";
        return false;
    }
}
