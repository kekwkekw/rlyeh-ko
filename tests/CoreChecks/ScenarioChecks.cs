using Rlyeh.Localizer.Core;

internal static class ScenarioChecks
{
    internal static void Run(Action<bool, string> check)
    {
        ScenarioCatalog.Entry Entry(int row, string target = "검수한 대사") => new()
        {
            Id = "scene:" + row, Grid = "scene.xlsx:scene", Row = row, SerializedRow = row,
            HeaderRow = 0, TextColumn = 2, Columns = new[] { "Command", "Arg1", "Text", "Voice", "PageCtrl" },
            SourceCells = new[] { "", "original-speaker-id", "同じ台詞", "voice/file", "" },
            Target = target, Reviewer = "test"
        };
        ScenarioCatalog Catalog(params ScenarioCatalog.Entry[] entries) => new(new() { Entries = entries.ToList() });
        bool Rejected(ScenarioCatalog.Entry entry)
        { try { Catalog(entry); return false; } catch (InvalidDataException) { return true; } }
        var first = Entry(10); var second = Entry(11, "문맥이 다른 검수 대사"); var catalog = Catalog(first, second);
        check(catalog.Find(first.Grid, 10)?.Target == first.Target && catalog.Find(first.Grid, 11)?.Target == second.Target &&
            catalog.Find("another-scene", 10) == null, "reviewed dialogue uses exact grid and row, not source-only matching");
        check(ScenarioCatalog.Matches(first, first.Columns, first.SourceCells), "original scenario cells match without normalization");
        var applied = first.SourceCells.ToArray(); applied[2] = first.Target;
        check(ScenarioCatalog.Matches(first, first.Columns, applied) && first.Source == "同じ台詞",
            "already reviewed rows remain reversible with their original text");
        applied[3] = "updated-voice";
        check(!ScenarioCatalog.Matches(first, first.Columns, applied), "changed voice or other native fields decline reviewed replacement");
        check(!ScenarioCatalog.Matches(first, new[] { "Arg1", "Command", "Text", "Voice", "PageCtrl" }, first.SourceCells),
            "changed script headers decline reviewed replacement");
        var unreviewed = Entry(12); unreviewed.Reviewer = "";
        check(Rejected(unreviewed), "unreviewed scenario entries cannot become runtime replacements");
        var controls = Entry(12); controls.SourceCells[2] = "<speed=2>日本語<interval=0.8>\n{player}、${value}\\n";
        controls.Target = "<speed=2>한국어<interval=0.8>\n{player}, ${value}\\n";
        check(Catalog(controls).Count == 1, "reviewed scenario tags, variables and line controls stay verbatim");
        controls.Target = controls.Target.Replace("<interval=0.8>", "<interval=0.4>");
        check(Rejected(controls), "changed timing tags reject a reviewed scenario file");
        bool duplicateRejected;
        try { Catalog(Entry(10), Entry(10)); duplicateRejected = false; }
        catch (InvalidDataException) { duplicateRejected = true; }
        check(duplicateRejected, "duplicate scenario row identities reject the catalog");

        int reads = 0, requests = 0; string result = ""; bool synchronous = false;
        var reviewedRows = new[] { new ScenarioTextPlan.NativeRow("검수본 日本語", true) };
        check(ScenarioTextPlan.TryCreate("검수본 日本語", reviewedRows, out var plan), "native reviewed page spans resolve before cache lookup");
        ScenarioTextPlan.Resolve(plan, _ => { reads++; return "오래된 캐시"; },
            (_, _) => requests++, (text, ready) => { result = text; synchronous = ready; }, _ => { });
        check(result == "검수본 日本語" && synchronous && reads == 0 && requests == 0,
            "reviewed rows bypass cached and engine translations even if they retain Japanese");
        var source = "同じ台詞\n검수한 대사\n次の台詞";
        var native = new[] { new ScenarioTextPlan.NativeRow("同じ台詞", false),
            new ScenarioTextPlan.NativeRow("검수한 대사", true), new ScenarioTextPlan.NativeRow("次の台詞", false) };
        check(ScenarioTextPlan.TryCreate(source, native, out plan), "mixed native pages preserve ordered row boundaries");
        var requested = new List<string>(); var callbacks = new List<Action<string?, string?>>();
        int completed = 0;
        ScenarioTextPlan.Resolve(plan, text => { requested.Add("cache:" + text); return null; },
            (text, callback) => { requested.Add(text); callbacks.Add(callback); },
            (text, ready) => { result = text; synchronous = ready; completed++; }, _ => { });
        check(!requested.Any(x => x.Contains("검수한") || x == source) && callbacks.Count == 2,
            "mixed pages never send reviewed rows or the combined page to hdTrans/cache");
        callbacks[1]("다음 대사", null); callbacks[0]("첫 대사", null);
        check(result == "첫 대사\n검수한 대사\n다음 대사" && !synchronous && completed == 1,
            "out-of-order asynchronous fallback composes once in native row order");
        callbacks.Clear();
        var slot = new RevisionSlot(); long revision = slot.Begin(source); bool staleApplied = false;
        ScenarioTextPlan.Resolve(plan, _ => null, (_, callback) => callbacks.Add(callback),
            (_, _) => { if (slot.Accepts(revision, source)) staleApplied = true; }, _ => { });
        slot.Begin("다음 페이지"); callbacks[0]("늦은 첫 결과", null); callbacks[1]("늦은 둘째 결과", null);
        check(!staleApplied, "completed mixed-page work cannot pass a newer page revision");
        int errors = 0;
        ScenarioTextPlan.Resolve(plan, _ => null, (_, callback) => callback(null, "engine unavailable"),
            (text, _) => result = text, _ => errors++);
        check(result == source && errors == 2, "failed fallback retains reviewed and original native spans");
        check(!ScenarioTextPlan.TryCreate("변경된 본문", reviewedRows, out _), "unmatched reviewed spans cannot fall through to whole-page cache");
        check(ScenarioTextPlan.TryCreate("<interval=0.8>日本語\n全文", Array.Empty<ScenarioTextPlan.NativeRow>(), out plan) &&
            plan.Count == 1 && plan[0].Translate, "pages without reviewed rows retain the existing whole-request fallback");
        check(!ScenarioTextPlan.HasJapanese("한국어 <link=日本語>123</link>") && ScenarioTextPlan.HasJapanese("ﾃｽﾄ"),
            "story language detection excludes control attributes and covers half-width kana");
    }
}
