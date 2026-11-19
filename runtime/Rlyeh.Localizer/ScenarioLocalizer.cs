using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Rlyeh.Localizer.Core;
using Utage;

namespace Rlyeh.Localizer;

// Only in-memory Text cells change. The catalog retains the exact original row.
internal sealed class ScenarioLocalizer
{
    private readonly Plugin plugin;
    private readonly ScenarioCatalog catalog;
    private readonly HashSet<string> reported = new(StringComparer.Ordinal);
    internal bool Enabled { get; private set; }
    internal ScenarioLocalizer(Plugin plugin, string path, bool enabled)
    {
        this.plugin = plugin; Enabled = enabled;
        try { catalog = ScenarioCatalog.Load(path); }
        catch (Exception ex)
        {
            plugin.Log.LogWarning("Scenario catalog rejected; hdTrans remains available: " + ex.Message);
            catalog = new(new ScenarioCatalog.Document());
        }
        plugin.Log.LogInfo("Reviewed scenario rows: " + catalog.Count);
    }
    // F2 may switch this before a screen/scenario reload. Command execution restores
    // the source even with a reused command list; no native object is kept alive.
    internal void SetEnabled(bool enabled) => Enabled = enabled;
    internal void BeforeCommands(AdvImportScenarioSheet grid)
    {
        foreach (var entry in catalog.ForGrid(grid.Name))
            if (entry.Row < grid.Rows.Count) Prepare(grid.Rows[entry.Row]);
    }
    internal void BeforeText(AdvCommandText command, AdvEngine engine)
    {
        Prepare(command.RowData);
        var previous = engine.Page.TextDataList;
        if (previous == null) return;
        for (int i = 0; i < previous.Count; i++)
            if (previous[i] is { } text) Prepare(text.RowData);
    }
    private ScenarioCatalog.Entry? Match(StringGridRow? row, out string[] cells)
    {
        cells = Array.Empty<string>();
        if (row == null || row.Grid == null) return null;
        var grid = row.Grid;
        var entry = catalog.Find(grid.Name, row.RowIndex);
        if (entry == null || entry.Row >= grid.Rows.Count || entry.HeaderRow >= grid.Rows.Count ||
            grid.Rows[entry.Row]?.Pointer != row.Pointer || row.Strings == null ||
            grid.Rows[entry.HeaderRow]?.Strings is not { } header) return null;
        cells = Copy(row.Strings);
        if (ScenarioCatalog.Matches(entry, Copy(header), cells)) return entry;
        if (reported.Add("mismatch:" + entry.Id)) plugin.Log.LogWarning("SCENARIO source/header mismatch; row retained: " + entry.Id);
        return null;
    }
    internal void Prepare(StringGridRow? row)
    {
        var entry = Match(row, out var cells);
        if (entry == null) return;
        string text = Enabled ? entry.Target : entry.Source;
        if (cells[entry.TextColumn] == text) return;
        // Il2CppStringArray's indexer writes a raw reference in this interop build.
        // Native Array.SetValue supplies the IL2CPP GC write barrier.
        var value = new Il2CppSystem.String(IL2CPP.ManagedStringToIl2Cpp(text));
        new Il2CppSystem.Array(row!.Strings.Pointer).SetValue(value, entry.TextColumn);
        string report = (Enabled ? "applied:" : "restored:") + entry.Id;
        if (reported.Add(report)) plugin.Log.LogInfo($"SCENARIO {(Enabled ? "applied" : "restored")} id={entry.Id} row={entry.Row}");
    }
    internal List<ScenarioTextPlan.NativeRow> PageRows(AdvPage page)
    {
        var result = new List<ScenarioTextPlan.NativeRow>();
        if (!Enabled || catalog.Count == 0 || page.TextDataList == null) return result;
        var commands = new List<(AdvCommandText Command, bool Reviewed)>();
        for (int i = 0; i < page.TextDataList.Count; i++)
        {
            var command = page.TextDataList[i];
            if (command == null) continue;
            var entry = Match(command.RowData, out var cells);
            commands.Add((command, entry != null && cells[entry.TextColumn] == entry.Target));
        }
        if (!commands.Any(x => x.Reviewed)) return result;
        foreach (var (command, reviewed) in commands)
            result.Add(new(command.ParseCellLocalizedText(), reviewed));
        return result;
    }
    private static string[] Copy(Il2CppStringArray values)
    {
        var result = new string[values.Length];
        for (int i = 0; i < result.Length; i++) result[i] = values[i];
        return result;
    }
}

[HarmonyPatch(typeof(AdvImportScenarioSheet), nameof(AdvImportScenarioSheet.CreateCommandList), typeof(AdvSettingDataManager))]
internal static class ScenarioCommandsPatch
{
    private static void Prefix(AdvImportScenarioSheet __instance)
    { try { Plugin.Instance.Scenarios.BeforeCommands(__instance); } catch (Exception ex) { Plugin.Instance.Log.LogWarning("Scenario rows: " + ex.Message); } }
}
[HarmonyPatch(typeof(AdvCommandText), nameof(AdvCommandText.DoCommand), typeof(AdvEngine))]
internal static class ScenarioTextCommandPatch
{
    private static void Prefix(AdvCommandText __instance, AdvEngine __0)
    { try { Plugin.Instance.Scenarios.BeforeText(__instance, __0); } catch (Exception ex) { Plugin.Instance.Log.LogWarning("Scenario text command: " + ex.Message); } }
}
