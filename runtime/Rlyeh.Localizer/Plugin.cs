using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Rlyeh.Localizer.Core;
using UnityEngine;
using Utage;

namespace Rlyeh.Localizer;

[BepInPlugin("local.rlyeh.korean", "Rlyeh Korean", "0.2.0")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Instance = null!;
    internal TranslationService Service = null!;
    internal LocalizationSwitch Mode = null!;
    private ConfigEntry<bool> enabledSetting = null!;
    private bool imagesRequested, scenariosRequested;
    private sealed record SavedFont(UnityEngine.UI.Text View, Font? Original);
    private readonly Dictionary<int, SavedFont> savedFonts = new();
    private float nextFontPrune, noticeUntil;
    private string notice = "";
    private GUIStyle? noticeStyle;
    private Font? koreanFont;
    private string koreanFontBundlePath = null!;
    internal bool IsBundledKoreanFont { get; private set; }
    internal bool Trace;
    internal bool TraceCatalog;
    internal UiLocalizer? Ui;
    internal ImageLocalizer Images = null!;
    internal ScenarioLocalizer Scenarios = null!;
    internal readonly HashSet<int> StoryNameIds = new();
    internal readonly Dictionary<int, WindowState> Windows = new();
    public override void Load()
    {
        Instance = this;
        enabledSetting = Config.Bind("General", "Enabled", true, "Enable Korean localization. F2 switches all localization paths together; reload a screen to refresh existing content.");
        Mode = new LocalizationSwitch(enabledSetting.Value);
        string root = Path.GetDirectoryName(GetType().Assembly.Location)!;
        koreanFontBundlePath = Path.Combine(root, "fonts", "notoserifcjkkr-bold.bundle");
        string dictionary = Config.Bind("Local", "Dictionary", "",
            "Leave empty to use the bundled hdTrans dictionary. Relative overrides are resolved next to the plugin.").Value.Trim();
        string snapshot = Path.Combine(root, dictionary.Length == 0
            ? Path.Combine("native", "hdtrans-data", "dicts.hdsnap")
            : dictionary);
        if (dictionary.Length == 0) Log.LogInfo("Using bundled hdTrans dictionary");
        int delay = Config.Bind("Diagnostics", "TranslationDelayMs", 0, "Regression tests only: simulated slow local response. Leave zero normally.").Value;
        Trace = Config.Bind("Diagnostics", "TraceDisplay", false, "Log source/target display counts for regression tests.").Value;
        TraceCatalog = Config.Bind("Diagnostics", "TraceCatalog", false, "Log reviewed master template selection and native formatted skill text.").Value;
        Service = new TranslationService(root, snapshot, s => Log.LogInfo(s), delay, Mode);
        imagesRequested = Config.Bind("Images", "Enabled", true, "Use bundled Korean image replacements.").Value;
        scenariosRequested = Config.Bind("Scenarios", "Enabled", true, "Apply local reviewed scenario Text cells before UTAGE parsing. Missing rows use hdTrans.").Value;
        Images = new ImageLocalizer(this, Path.Combine(root, "images"),
            Mode.Enabled && imagesRequested);
        Scenarios = new ScenarioLocalizer(this, Path.Combine(root, "scenario", "reviewed.json"),
            Mode.Enabled && scenariosRequested);
        Log.LogInfo("Reviewed text catalog: " + Service.Catalog.Count);
        if (Config.Bind("Local", "TranslateUi", true, "Translate ordinary UI locally. Story windows always use the separate UTAGE adapter.").Value) Ui = new UiLocalizer(this);
        ClassInjector.RegisterTypeInIl2Cpp<MainThreadPump>();
        AddComponent<MainThreadPump>();
        new Harmony("local.rlyeh.korean").PatchAll();
        Log.LogInfo("UTAGE renderer + TMP processing-buffer localization; hdTrans only; no TMP polling/setter hooks");
        Log.LogInfo($"LOCALIZATION {(Mode.Enabled ? "ON" : "OFF")} revision={Mode.Revision}; F2 switches all paths");
    }
    internal void SetEnabled(bool enabled)
    {
        if (!Mode.SetEnabled(enabled)) return;
        enabledSetting.Value = enabled;
        notice = "한국어화 " + (enabled ? "ON" : "OFF") + "\n화면 이동·재로딩 후 반영됩니다.";
        noticeUntil = Time.realtimeSinceStartup + 3.5f;
        Scenarios.SetEnabled(enabled && scenariosRequested);
        foreach (var state in Windows.Values)
        { state.Slot.Invalidate(); state.Translation = null; state.Alignment = null; }
        Ui?.ResetForMode();
        Images.SetEnabled(enabled && imagesRequested);
        if (!enabled)
            foreach (var saved in savedFonts.Values.ToArray())
                if (saved.View && !UiLocalizer.HasKorean(saved.View.text ?? "")) RestoreUguiFont(saved.View);
        Log.LogInfo($"LOCALIZATION {(enabled ? "ON" : "OFF")} revision={Mode.Revision}; existing content refreshes on screen reload");
    }
    internal void UpdateMode()
    {
        if (Input.GetKeyDown(KeyCode.F2)) SetEnabled(!Mode.Enabled);
        if (Time.realtimeSinceStartup < nextFontPrune) return;
        nextFontPrune = Time.realtimeSinceStartup + 2;
        foreach (int id in savedFonts.Where(x => !x.Value.View).Select(x => x.Key).ToArray()) savedFonts.Remove(id);
    }
    internal void DrawModeNotice()
    {
        if (notice.Length == 0 || Time.realtimeSinceStartup >= noticeUntil) return;
        if (noticeStyle == null)
        {
            noticeStyle = new GUIStyle(GUI.skin.box) { font = GetKoreanFont(), fontSize = 16,
                alignment = TextAnchor.MiddleCenter, wordWrap = true };
            noticeStyle.normal.textColor = Color.white;
        }
        GUI.Box(new Rect(Math.Max(8, Screen.width - 310), 12, 298, 64), notice, noticeStyle);
    }
    internal void EnsureUguiFont(UnityEngine.UI.Text? text)
    {
        if (!Mode.Enabled || !text) return;
        var font = GetKoreanFont();
        if (text!.font != font)
        {
            savedFonts[text.GetInstanceID()] = new(text, text.font);
            text.font = font;
        }
    }
    private void RestoreUguiFont(UnityEngine.UI.Text? text)
    {
        if (!text || !savedFonts.Remove(text!.GetInstanceID(), out var saved) || saved.View != text) return;
        if (text.font == koreanFont && (saved.Original == null || saved.Original)) text.font = saved.Original;
    }
    internal void EnsureFont(AdvUguiMessageWindow window)
    {
        EnsureUguiFont(window.Text);
        if (window.nameText)
        {
            StoryNameIds.Add(window.nameText.GetInstanceID());
            EnsureUguiFont(window.nameText);
        }
    }
    internal Font GetKoreanFont()
    {
        if (koreanFont) return koreanFont!;
        if (File.Exists(koreanFontBundlePath))
        {
            AssetBundle? bundle = null;
            try
            {
                bundle = AssetBundle.LoadFromFile(koreanFontBundlePath);
                if (bundle)
                {
                    var font = bundle.LoadAsset<Font>("assets/notoserifcjkkr-bold.otf");
                    if (font && font.dynamic && font.HasCharacter('한') && font.HasCharacter('힣'))
                        koreanFont = font;
                    else if (font)
                        UnityEngine.Object.Destroy(font);
                }
            }
            catch (Exception ex) { Log.LogWarning("Bundled UGUI font: " + ex.Message); }
            finally { if (bundle) bundle!.Unload(false); }
        }
        IsBundledKoreanFont = koreanFont;
        if (!koreanFont)
        {
            Log.LogWarning("Noto Serif CJK KR Bold unavailable for UGUI; using installed Korean font");
            koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans KR", "Malgun Gothic", "Arial" }, 32);
            if (!koreanFont || !koreanFont.HasCharacter('한')) throw new InvalidOperationException("No Korean dynamic font available");
        }
        koreanFont!.hideFlags = HideFlags.DontUnloadUnusedAsset;
        UnityEngine.Object.DontDestroyOnLoad(koreanFont);
        Log.LogInfo($"Dynamic font ready: {koreanFont.name} (bundled={IsBundledKoreanFont})");
        return koreanFont;
    }
    internal void Changed(AdvUguiMessageWindow window, AdvMessageWindow model)
    {
        if (window.nameText) StoryNameIds.Add(window.nameText.GetInstanceID());
        int id = window.GetInstanceID();
        if (!Windows.TryGetValue(id, out var state)) Windows[id] = state = new WindowState(window);
        string source = model.Text?.OriginalText ?? "";
        long revision = state.Slot.Begin(source);
        state.Model = model; state.Translation = null; state.SourceLength = model.Text?.Length ?? 0;
        state.Alignment = null;
        state.Name = model.NameText ?? "";
        string scope = window.Engine.Page.ScenarioLabel ?? "";
        if (Trace) Log.LogInfo($"STORY begin rev={revision} scope={scope} chars={state.SourceLength} frame={Time.frameCount} source={source}");
        if (!Mode.Enabled)
        {
            if (!UiLocalizer.HasKorean(source)) RestoreUguiFont(window.Text);
            RestoreUguiFont(window.nameText);
            return;
        }
        EnsureFont(window);
        if (source.Length > 0)
        {
            var rows = Scenarios.PageRows(window.Engine.Page);
            if (!ScenarioTextPlan.TryCreate(source, rows, out var parts))
            {
                Log.LogWarning("Reviewed scenario spans differ from native page; current text retained: " + scope);
            }
            else
            {
                if (Trace) Log.LogInfo($"STORY route rev={revision} reviewed={rows.Count(x => x.Reviewed)} engineParts={parts.Count(x => x.Translate)}");
                ScenarioTextPlan.Resolve(parts,
                    text => Service.TryGetReady(text, scope, out string value) ? value : null,
                    (text, done) =>
                    {
                        if (!Service.Request(text, scope, done)) done(null, "Translation queue unavailable; original retained");
                    },
                    (value, ready) =>
                    {
                        if (!state.Slot.Accepts(revision, source) || !state.Window ||
                            (!ready && !state.Window.isActiveAndEnabled) || state.Model?.Text?.OriginalText != source)
                        {
                            if (Trace) Log.LogInfo("STORY stale result ignored rev=" + revision);
                            return;
                        }
                        if (value != source) SetStoryTranslation(state, source, value, ready);
                    },
                    error =>
                    {
                        if (state.Slot.Accepts(revision, source)) Log.LogWarning(error);
                    });
            }
        }
        string name = state.Name;
        if (name.Length == 0) return;
        if (Service.TryGetReady(name, "speaker", out string translatedName))
        {
            if (window.nameText) window.nameText.text = translatedName;
            return;
        }
        Service.Request(name, "speaker", (value, error) =>
        {
            if (error == null && value != null && state.Window && state.Window.nameText &&
                state.Slot.Accepts(revision, source) && state.Name == name)
                state.Window.nameText.text = value;
        });
    }
    private void SetStoryTranslation(WindowState state, string source, string value, bool ready)
    {
        if (!Mode.Enabled) return;
        state.Translation = value;
        state.TargetLength = new TextData(value).Length;
        state.Alignment = new DisplayAlignment(new DisplayText(source).Plain, new DisplayText(value).Plain);
        if (state.Window.isActiveAndEnabled && state.Window.Text) Apply(state, state.Window.Text.LengthOfView);
        if (Trace) Log.LogInfo($"STORY translated rev={state.Slot.Revision} chars={state.TargetLength} ready={ready} frame={Time.frameCount} text={value}");
    }
    internal void Apply(WindowState state, int originalVisible)
    {
        if (!state.Window.Text) return;
        var renderer = state.Window.Text;
        if (!Mode.Enabled)
        {
            string? original = state.Model?.Text?.OriginalText;
            if (original != null && renderer.text != original)
            { renderer.text = original; renderer.LengthOfView = originalVisible; }
            if (state.Window.nameText && state.Window.nameText.text != state.Name) state.Window.nameText.text = state.Name;
            if (original != null && !UiLocalizer.HasKorean(original)) RestoreUguiFont(renderer);
            RestoreUguiFont(state.Window.nameText);
            return;
        }
        if (state.Translation == null) return;
        if (renderer.text != state.Translation) renderer.text = state.Translation;
        renderer.LengthOfView = state.Alignment?.MapVisible(originalVisible, state.SourceLength, state.TargetLength)
            ?? DisplayText.MapVisible(originalVisible, state.SourceLength, state.TargetLength);
        // UTAGE placed the wait marker using the source count earlier in LateUpdate.
        // Re-evaluate its native anchoring after the translated display count is set.
        state.Window.LinkIcon();
        if (Trace && originalVisible != state.LastTraced)
        {
            state.LastTraced = originalVisible;
            Log.LogInfo($"DISPLAY rev={state.Slot.Revision} sourceVisible={originalVisible}/{state.SourceLength} targetVisible={renderer.LengthOfView}/{state.TargetLength} send={state.Window.Engine.Page.IsSendChar} wait={state.Window.Engine.Page.IsWaitInputInPage}");
        }
    }
}

internal sealed class WindowState
{
    public readonly AdvUguiMessageWindow Window;
    public readonly RevisionSlot Slot = new();
    public AdvMessageWindow? Model;
    public string Name = "";
    public string? Translation;
    public DisplayAlignment? Alignment;
    public int SourceLength, TargetLength;
    public int LastTraced = int.MinValue;
    public WindowState(AdvUguiMessageWindow window) => Window = window;
}

public sealed class MainThreadPump : MonoBehaviour
{
    private float nextProbe;
    public MainThreadPump(IntPtr pointer) : base(pointer) { }
    public void LateUpdate() => Plugin.Instance.Ui?.Tmp.ApplyLayouts();
    public void OnGUI() => Plugin.Instance.DrawModeNotice();
    public void Update()
    {
        Plugin.Instance.UpdateMode();
        Plugin.Instance.Service.Drain();
        Plugin.Instance.Images.Tick();
        Plugin.Instance.Ui?.Tick();
#if IMAGE_PROBE
        ImageProbe.Tick();
#endif
#if SCENARIO_PROBE
        ScenarioProbe.Tick();
#endif
#if TMP_REGRESSION_PROBE
        TmpRegressionProbe.Tick();
#endif
#if UI_LAYOUT_PROBE
        UiLayoutProbe.Tick();
#endif
        if (Plugin.Instance.Trace && Time.realtimeSinceStartup < 90 && Time.realtimeSinceStartup > nextProbe)
        {
            nextProbe = Time.realtimeSinceStartup + 10;
            Plugin.Instance.Log.LogInfo($"PROBE frame={Time.frameCount} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} scale={Time.timeScale} windows={UnityEngine.Object.FindObjectsOfType<AdvUguiMessageWindow>().Length}");
        }
    }
}

[HarmonyPatch(typeof(AdvUguiMessageWindow), nameof(AdvUguiMessageWindow.OnTextChanged))]
internal static class TextChangedPatch
{
    private static void Postfix(AdvUguiMessageWindow __instance, AdvMessageWindow __0)
    { try { Plugin.Instance.Changed(__instance, __0); } catch (Exception ex) { Plugin.Instance.Log.LogError(ex); } }
}
[HarmonyPatch(typeof(AdvUguiMessageWindow), nameof(AdvUguiMessageWindow.LateUpdate))]
internal static class DisplayProgressPatch
{
    private static void Postfix(AdvUguiMessageWindow __instance)
    {
        try
        {
            if (Plugin.Instance.Windows.TryGetValue(__instance.GetInstanceID(), out var state))
                Plugin.Instance.Apply(state, __instance.Text.LengthOfView);
        }
        catch (Exception ex) { Plugin.Instance.Log.LogError(ex); }
    }
}
[HarmonyPatch(typeof(AdvUguiMessageWindow), nameof(AdvUguiMessageWindow.Clear))]
internal static class ClearPatch
{
    private static void Prefix(AdvUguiMessageWindow __instance)
    {
        if (Plugin.Instance.Windows.Remove(__instance.GetInstanceID(), out var state)) state.Slot.Invalidate();
    }
}
