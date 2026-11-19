using Rog.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rlyeh.Localizer;

// Existing game views only: wrap narrow labels and let native layouts make room for Korean.
// No replacement renderer, text edits, game IDs, or fixed screen coordinates.
internal static class UiReadability
{
    private sealed record Size(RectTransform Rect, float Height);
    private sealed record TextStyle(TMP_Text Text, float FontSize, bool AutoSize, TextWrappingModes Wrap, float LineSpacing);
    private static readonly Dictionary<int, Size> sizes = new();
    private static readonly Dictionary<int, TextStyle> styles = new();
    private static readonly Dictionary<int, (LayoutElement Element, float Height)> minimums = new();
    private static readonly Dictionary<int, (TMP_Text Text, float Height)> requests = new();
    private static readonly Dictionary<int, (RectTransform Rect, Vector3 Position)> rewardIcons = new();
    internal static void PruneDestroyedViews()
    {
        foreach (int id in sizes.Where(pair => !pair.Value.Rect).Select(pair => pair.Key).ToArray()) sizes.Remove(id);
        foreach (int id in requests.Where(pair => !pair.Value.Text).Select(pair => pair.Key).ToArray()) requests.Remove(id);
        foreach (int id in styles.Where(pair => !pair.Value.Text).Select(pair => pair.Key).ToArray()) styles.Remove(id);
        foreach (int id in minimums.Where(pair => !pair.Value.Element).Select(pair => pair.Key).ToArray()) minimums.Remove(id);
        foreach (int id in rewardIcons.Where(pair => !pair.Value.Rect).Select(pair => pair.Key).ToArray()) rewardIcons.Remove(id);
    }
    private static void Remember(TMP_Text text)
    {
        int id = text.GetInstanceID();
        if (!styles.TryGetValue(id, out var saved) || saved.Text != text)
            styles[id] = new(text, text.fontSize, text.enableAutoSizing, text.textWrappingMode, text.lineSpacing);
    }
    internal static void Restore()
    {
        foreach (var saved in styles.Values)
        {
            if (!saved.Text) continue;
            saved.Text.enableAutoSizing = saved.AutoSize; saved.Text.fontSize = saved.FontSize;
            saved.Text.textWrappingMode = saved.Wrap; saved.Text.lineSpacing = saved.LineSpacing;
            saved.Text.havePropertiesChanged = true;
            saved.Text.SetLayoutDirty(); saved.Text.SetVerticesDirty();
        }
        foreach (var saved in minimums.Values) if (saved.Element) saved.Element.minHeight = saved.Height;
        foreach (var saved in sizes.Values)
            if (saved.Rect) { Height(saved.Rect, saved.Height); LayoutRebuilder.MarkLayoutForRebuild(saved.Rect); }
        foreach (var saved in rewardIcons.Values) if (saved.Rect) saved.Rect.localPosition = saved.Position;
        rewardIcons.Clear();
        styles.Clear(); minimums.Clear(); sizes.Clear(); requests.Clear();
    }
    private static float Original(RectTransform rect)
    {
        int id = rect.GetInstanceID();
        if (!sizes.TryGetValue(id, out var saved) || !saved.Rect)
            sizes[id] = saved = new(rect, rect.rect.height);
        return saved.Height;
    }
    private static bool Same(TMP_Text? a, TMP_Text b) => a is not null && a && a.GetInstanceID() == b.GetInstanceID();
    private static void Height(RectTransform rect, float height)
    {
        if (Math.Abs(rect.rect.height - height) > 0.5f)
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
    internal static void Apply(TMP_Text text)
    {
        if (!text || !text.isActiveAndEnabled || text.rectTransform.rect.width < 50) return;
        RewardClassIcon(text);
        var button = text.GetComponentInParent<UnitAwakePotentialContent>();
        if (button && Same(button.m_awakeText, text))
        {
            Remember(text);
            // The shipped button is NoWrap: a long name shrank the entire two-line label to 10pt.
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = 0;
            FitRenderedLabel(text);
            return;
        }
        var active = text.GetComponentInParent<UnitDetailActiveSkillParamsView>();
        if (active && Same(active.m_descriptionText, text)) { ScrollBody(text, active); return; }
        var passive = text.GetComponentInParent<UnitDetailPassiveSkillParamsView>();
        if (passive && Same(passive.m_descriptionText, text)) { ScrollBody(text, passive); return; }
        var special = text.GetComponentInParent<UnitDetailSpecialSkillParamsView>();
        if (special && Same(special.m_descriptionText, text)) { ScrollBody(text, special); return; }
        var potential = text.GetComponentInParent<UnitDetailPotentialContentView>();
        if (potential && Same(potential.m_descriptionText, text)) { ScrollBody(text, potential); return; }
        var activeDetail = text.GetComponentInParent<ActiveSkillDetailView>();
        if (activeDetail && Same(activeDetail.m_skillText, text)) { ComparisonBody(text, activeDetail); return; }
        var passiveDetail = text.GetComponentInParent<PassiveSkillDetailView>();
        if (passiveDetail && Same(passiveDetail.m_skillText, text)) ComparisonBody(text, passiveDetail);
    }
    private static void RewardClassIcon(TMP_Text text)
    {
        // These native prefabs reserve three spaces on the reward line for a sibling Image.
        // Its Japanese position is baked into each prefab, so follow the rendered Korean gap.
        // Stay within the existing TMP layout pass; never edit the binding or its source text.
        if (!text.GetComponentInParent<RogueThemeTopFrontDesignView>()) return;
        var message = text.transform.parent;
        if (!message || message.name != "Message") return;
        var image = message.Find("IconImage")?.GetComponent<Image>();
        if (image is null || !image || !image.sprite) return;
        if (TmpBufferLocalizer.ReadCurrentSource(text)?.Contains("   ") != true) return;
        text.ForceMeshUpdate();
        var info = text.textInfo;
        var chars = info.characterInfo;
        int gap = -1;
        for (int i = 1; i + 3 < info.characterCount; i++)
        {
            if (chars[i].lineNumber != 1 || chars[i - 1].lineNumber != 1 || chars[i + 3].lineNumber != 1 ||
                chars[i - 1].character == ' ' || chars[i + 3].character == ' ' ||
                chars[i].character != ' ' || chars[i + 1].character != ' ' || chars[i + 2].character != ' ') continue;
            if (gap >= 0) return; // A changed/ambiguous native layout keeps its original placement.
            gap = i;
        }
        if (gap < 0 || chars[gap + 2].xAdvance <= chars[gap].origin) return;
        var icon = image.rectTransform;
        int id = icon.GetInstanceID();
        if (!rewardIcons.TryGetValue(id, out var saved) || saved.Rect != icon)
            rewardIcons[id] = saved = (icon, icon.localPosition);
        float center = (chars[gap].origin + chars[gap + 2].xAdvance) * 0.5f;
        var point = icon.parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(center, 0, 0)));
        icon.localPosition = new Vector3(point.x, saved.Position.y, saved.Position.z);
    }
    private static float Measure(TMP_Text text)
    {
        Remember(text);
        text.enableAutoSizing = false;
        text.fontSize = text.fontSizeMax > 0 ? Math.Min(text.fontSizeMax, 17) : 17;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.ForceMeshUpdate();
        // TMP preferred height can undercount Korean fallback lines. Measure the actual glyph mesh too.
        return Mathf.Ceil(Math.Max(text.GetPreferredValues(text.rectTransform.rect.width, float.PositiveInfinity).y,
            text.renderedHeight + text.margin.y + text.margin.w)) + 4;
    }
    private static void FitRenderedLabel(TMP_Text text)
    {
        text.enableAutoSizing = false;
        text.fontSize = text.fontSizeMax;
        float width = text.rectTransform.rect.width - text.margin.x - text.margin.z;
        float height = text.rectTransform.rect.height - text.margin.y - text.margin.w - 8;
        if (width <= 0 || height <= 0) return;
        for (int i = 0; i < 5; i++)
        {
            text.ForceMeshUpdate();
            float ratio = Math.Min(width / Math.Max(1, text.renderedWidth), height / Math.Max(1, text.renderedHeight));
            if (ratio >= 1) return;
            text.fontSize = Math.Max(text.fontSizeMin, Mathf.Floor(text.fontSize * ratio * 19.8f) / 20);
        }
        text.ForceMeshUpdate();
    }
    private static void ScrollBody(TMP_Text text, Component view)
    {
        var scroll = view.GetComponentInParent<ScrollRect>();
        var root = view.GetComponent<RectTransform>();
        if (!scroll || !scroll.content || !root || root.rect.height < 1 || !root.IsChildOf(scroll.content)) return;
        // Follow vertically stretched wrappers to the fixed-height row owned by this view.
        var row = text.rectTransform;
        while (row != root && Math.Abs(row.anchorMax.y - row.anchorMin.y) > 0.01f)
        {
            var parent = row.parent?.TryCast<RectTransform>();
            if (parent is null || !parent) return;
            row = parent;
        }
        if (row != root && !row.IsChildOf(root)) return;
        float baseRow = Original(row), baseRoot = Original(root);
        float required = Math.Max(baseRow, Measure(text));
        Height(row, required);
        if (row != root) Height(root, baseRoot + required - baseRow);
        LayoutRebuilder.MarkLayoutForRebuild(scroll.content);
    }
    private static void ComparisonBody(TMP_Text text, Component detail)
    {
        var popup = detail.GetComponentInParent<UnitAwakeSkillConfirmPopup>();
        var skill = text.GetComponentInParent<LayoutElement>();
        var layout = skill ? skill.transform.parent.GetComponent<VerticalLayoutGroup>() : null;
        if (!popup || !skill || layout is null || !layout) return;
        Component? panel = detail.GetComponentInParent<ReinforcePassiveSkillView>();
        if (!panel) panel = detail.GetComponentInParent<ReinforceOverSkillView>();
        if (!panel) panel = detail.GetComponentInParent<AcquirePassiveSkillView>();
        if (panel is null || !panel) return;
        var panelRect = panel.GetComponent<RectTransform>();
        var frame = panel.transform.parent?.parent?.TryCast<RectTransform>();
        var popupRect = popup.GetComponent<RectTransform>();
        var detailRect = detail.GetComponent<RectTransform>();
        var layoutRect = layout.GetComponent<RectTransform>();
        var skillRect = skill.GetComponent<RectTransform>();
        if (frame is null || !frame || !frame.GetComponent<ContentSizeFitter>() || frame.rect.height < 100) return;
        float baseText = Original(text.rectTransform), baseSkill = Original(skillRect);
        float required = Math.Max(baseText, Measure(text));
        // Preserve the popup's native buttons and keep its grown frame within the parent canvas.
        float room = Math.Max(0, popupRect.rect.height - Original(frame) - 32);
        float extra = Math.Min(required - baseText, room);
        requests[text.GetInstanceID()] = (text, extra);
        float panelExtra = extra;
        foreach (var request in requests.Values)
            if (request.Text && request.Text.transform.IsChildOf(panel.transform)) panelExtra = Math.Max(panelExtra, request.Height);
        float baseLayout = Original(layoutRect), baseDetail = Original(detailRect), basePanel = Original(panelRect);
        int skillId = skill.GetInstanceID();
        if (!minimums.TryGetValue(skillId, out var savedMinimum) || savedMinimum.Element != skill)
            minimums[skillId] = (skill, skill.minHeight);
        skill.minHeight = baseSkill + extra;
        Height(layoutRect, baseLayout + extra);
        Height(detailRect, baseDetail + extra);
        Height(panelRect, basePanel + panelExtra);
        // If an exceptional future description cannot fit the available canvas, native auto-size remains.
        text.enableAutoSizing = true;
        LayoutRebuilder.MarkLayoutForRebuild(frame);
    }
}
