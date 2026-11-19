using System.Text.Json;
using Rlyeh.Localizer.Core;

internal static class MasterDisplayChecks
{
    internal static void Run(Action<bool, string> check)
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../data/exports/text-catalog.json"));
        var catalog = TextCatalog.Load(path);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var entries = doc.RootElement.GetProperty("entries").EnumerateArray()
            .Where(x => x.GetProperty("scope").GetString() is "master:AutoSkillMasterData:12:text" or "master:AutoSkillMasterData:24:text").ToArray();
        if (entries.Length != 2) throw new Exception("Missing shared Master display fixtures");
        foreach (string value in new[] { "250", "500", "1000" })
        {
            var display = new MasterDisplayCache();
            string? previous = null;
            foreach (var entry in entries)
            {
                string source = entry.GetProperty("source").GetString()!;
                string native = source.Replace("[0,0,0]", value);
                if (!catalog.TryRenderMaster(entry.GetProperty("scope").GetString()!, source, native, _ => value, out string target) ||
                    !display.Register(native, target) || !display.TryGet(native, out var ready) || ready != target ||
                    (previous != null && previous != target)) throw new Exception("Shared Master display conflict at " + value);
                previous = target;
            }
            check(true, "shipped AutoSkill 12/24 share a ready reviewed display at native value " + value);
        }
    }
}
