using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rlyeh.Localizer.Core;

// Reviewed script rows are independent of hdTrans and its cache revision.
public sealed class ScenarioCatalog
{
    public sealed class Entry
    {
        public string Id { get; set; } = "";
        public string Grid { get; set; } = "";
        public int Row { get; set; }
        public int SerializedRow { get; set; }
        public int HeaderRow { get; set; }
        public int TextColumn { get; set; }
        public string[] Columns { get; set; } = Array.Empty<string>();
        public string[] SourceCells { get; set; } = Array.Empty<string>();
        public string Target { get; set; } = "";
        public string Reviewer { get; set; } = "";
        public string Source => SourceCells[TextColumn];
    }
    public sealed class Document
    {
        public int Version { get; set; } = 1;
        public List<Entry> Entries { get; set; } = new();
    }
    private readonly Dictionary<(string, int), Entry> rows = new();
    private readonly Dictionary<string, List<Entry>> grids = new(StringComparer.Ordinal);
    public int Count => rows.Count;
    public ScenarioCatalog(Document doc)
    {
        if (doc.Version != 1 || doc.Entries == null || doc.Entries.Count > 200000)
            throw new InvalidDataException("Unsupported scenario catalog");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var positions = new HashSet<(string, int)>();
        foreach (var e in doc.Entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id) ||
                string.IsNullOrWhiteSpace(e.Grid) || e.Grid.Contains('\0') ||
                e.HeaderRow < 0 || e.Row <= e.HeaderRow || e.SerializedRow < 0 ||
                !positions.Add((e.Grid, e.Row)) || e.Columns == null || e.SourceCells == null ||
                e.Columns.Any(x => x == null) || e.SourceCells.Any(x => x == null || x.Contains('\0')) ||
                e.TextColumn < 0 || e.TextColumn >= e.Columns.Length || e.TextColumn >= e.SourceCells.Length ||
                e.Columns[e.TextColumn] != "Text" || e.Columns.Count(x => x == "Text") != 1 ||
                string.IsNullOrWhiteSpace(e.Source) || string.IsNullOrWhiteSpace(e.Target) || e.Target.Contains('\0') ||
                string.IsNullOrWhiteSpace(e.Reviewer) ||
                !Controls(e.Source).SequenceEqual(Controls(e.Target)) ||
                !rows.TryAdd((e.Grid, e.SerializedRow), e))
                throw new InvalidDataException("Invalid, unreviewed or duplicate scenario row: " + e?.Id);
            if (!grids.TryGetValue(e.Grid, out var list)) grids[e.Grid] = list = new();
            list.Add(e);
        }
    }
    public static ScenarioCatalog Load(string path)
    {
        if (!File.Exists(path)) return new(new Document());
        if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new InvalidDataException("Scenario catalog exceeds 64 MiB");
        var doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return new(doc ?? throw new InvalidDataException("Empty scenario catalog"));
    }
    public IReadOnlyList<Entry> ForGrid(string grid) => grids.TryGetValue(grid, out var entries) ? entries : Array.Empty<Entry>();
    public Entry? Find(string grid, int serializedRow) => rows.GetValueOrDefault((grid, serializedRow));
    public static bool Matches(Entry entry, IReadOnlyList<string> columns, IReadOnlyList<string> cells)
    {
        if (!columns.SequenceEqual(entry.Columns) || cells.Count != entry.SourceCells.Length) return false;
        for (int i = 0; i < cells.Count; i++)
            if (i == entry.TextColumn ? cells[i] != entry.Source && cells[i] != entry.Target : cells[i] != entry.SourceCells[i])
                return false;
        return true;
    }
    private static readonly Regex Token = new(@"\r\n|\r|\n|\\[nr]|\$\{[^{}\r\n]+\}|\{[A-Za-z_0-9][^{}\r\n]*\}|%(?:[0-9]+\$)?[sdif]|\$[A-Za-z_][A-Za-z_0-9]*", RegexOptions.Compiled);
    private static readonly Regex TagStart = new(@"</?[A-Za-z][A-Za-z0-9_:-]*(?=[\s=/>]|$)", RegexOptions.Compiled);
    // Keep tag arguments, variables, escapes and line boundaries byte-for-byte and in order.
    public static List<string> Controls(string text)
    {
        var result = new List<string>();
        for (int i = 0; i < text.Length;)
        {
            var start = TagStart.Match(text, i);
            if (start.Success && start.Index == i)
            {
                int end = i + 1; char quote = '\0';
                for (; end < text.Length; end++)
                {
                    char c = text[end];
                    if (quote != '\0') { if (c == quote) quote = '\0'; }
                    else if (c is '\'' or '"') quote = c;
                    else if (c is '<' or '>') break;
                }
                if (end == text.Length || text[end] != '>' || quote != '\0') throw new InvalidDataException("Malformed scenario tag");
                result.Add(text[i..(end + 1)]); i = end + 1;
            }
            else
            {
                var match = Token.Match(text, i);
                if (match.Success && match.Index == i) { result.Add(match.Value); i += match.Length; }
                else i++;
            }
        }
        return result;
    }
}
