using Rlyeh.Localizer.Core;

internal static class LocalizationSwitchChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var mode = new LocalizationSwitch(); long first = mode.Revision;
        check(mode.Enabled && mode.Accepts(first), "localization starts ON with a valid work generation");
        check(!mode.SetEnabled(true) && mode.Revision == first, "repeating the same localization mode preserves current work");
        mode.SetEnabled(false); long disabled = mode.Revision;
        check(!mode.Enabled && !mode.Accepts(first) && !mode.Accepts(disabled), "OFF rejects both prior and newly captured translation work");
        mode.SetEnabled(true);
        check(mode.Enabled && mode.Accepts(mode.Revision) && !mode.Accepts(first), "ON after OFF cannot accept an older ON result");
        check(!new LocalizationSwitch(false).Enabled, "persisted OFF can be selected before hooks and service startup");
        var master = new MasterDisplayCache();
        check(master.Register("攻撃力+500%", "공격력 +500%") && master.TryGet("攻撃力+500%", out var target) && target == "공격력 +500%",
            "reviewed formatted Master displays preserve a separate native lookup key");
        check(master.Register("攻撃力+500%", "공격력 +500%"), "identical Master display registrations remain reusable");
        check(!master.Register("攻撃力+500%", "다른 설명 +500%") && !master.TryGet("攻撃力+500%", out _) &&
            !master.Register("攻撃力+500%", "공격력 +500%"), "conflicting Master display pairs stay blocked");
    }
    internal static void Service(TranslationService service, Action<bool, string> check)
    {
        bool oldApplied = false, currentApplied = false;
        check(service.Request("古い翻訳結果です。", "toggle-test", (_, _) => oldApplied = true), "real service accepts work while ON");
        service.MasterDisplays.Register("確認用攻撃力+321%", "확인용 공격력 +321%");
        check(service.TryGetReady("確認用攻撃力+321%", "ui", out var master) && master == "확인용 공격력 +321%", "Master display pairs use the immediate UI path");
        service.Mode.SetEnabled(false);
        check(!service.TryGetReady("確認用攻撃力+321%", "ui", out _) && !service.TryGetReady("闘技場に987回挑戦する", "ui", out _) &&
            !service.Request("ネオン", "speaker", (_, _) => oldApplied = true), "OFF blocks reviewed UI, stored cache and new engine requests");
        service.Mode.SetEnabled(true);
        check(service.Request("現在の翻訳結果です。", "toggle-test", (value, error) =>
            { if (error != null) throw new Exception(error); currentApplied = value?.Any(c => c >= '가' && c <= '힣') == true; }),
            "real service resumes after ON without reinitializing hdTrans");
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!currentApplied && DateTime.UtcNow < deadline) { service.Drain(); Thread.Sleep(5); }
        check(currentApplied && !oldApplied, "real delayed completion cannot cross an OFF/ON generation change");
    }
}
