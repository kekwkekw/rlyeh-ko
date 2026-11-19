namespace Rlyeh.Localizer.Core;

// Native UTAGE has already joined the current page. Reviewed row spans must never
// enter a whole-page cache lookup or an engine request, even on a mixed page.
public static class ScenarioTextPlan
{
    public sealed record Part(string Text, bool Translate);
    public sealed record NativeRow(string Text, bool Reviewed);
    public static bool HasJapanese(string text) => new DisplayText(text).Plain.Any(c =>
        c is >= '\u3040' and <= '\u30ff' or >= '\u3400' and <= '\u9fff' or >= '\uff66' and <= '\uff9d');
    public static bool TryCreate(string source, IReadOnlyList<NativeRow> rows, out List<Part> parts)
    {
        parts = new();
        int cursor = 0;
        foreach (var row in rows)
        {
            string text = row.Text;
            if (text.Length == 0) continue;
            int index = source.IndexOf(text, cursor, StringComparison.Ordinal);
            if (index < 0) { parts.Clear(); return false; }
            if (index > cursor) { string gap = source[cursor..index]; parts.Add(new(gap, HasJapanese(gap))); }
            parts.Add(new(text, !row.Reviewed && HasJapanese(text)));
            cursor = index + text.Length;
        }
        if (cursor < source.Length || parts.Count == 0)
        { string tail = source[cursor..]; parts.Add(new(tail, HasJapanese(tail))); }
        return true;
    }
    public static void Resolve(IReadOnlyList<Part> parts, Func<string, string?> ready,
        Action<string, Action<string?, string?>> request, Action<string, bool> done, Action<string> error)
    {
        var values = new string[parts.Count];
        var pending = new List<int>();
        for (int i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            string? value = part.Translate ? ready(part.Text) : part.Text;
            values[i] = value ?? part.Text;
            if (value == null) pending.Add(i);
        }
        if (pending.Count == 0) { done(string.Concat(values), true); return; }
        int remaining = pending.Count;
        foreach (int index in pending)
            request(parts[index].Text, (value, failure) =>
            {
                if (failure != null) error(failure);
                else if (value != null) values[index] = value;
                if (--remaining == 0) done(string.Concat(values), false);
            });
    }
}
