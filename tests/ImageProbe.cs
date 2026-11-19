using System.Text.Json;
using UnityEngine;
using UnityEngine.UI;

namespace Rlyeh.Localizer;

// Opt-in asset provenance diagnostics. No component values are changed.
internal static class ImageProbe
{
    private static readonly HashSet<string> seen = new();
    private static float next;
    internal static void Tick()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            Plugin.Instance.Images.SetEnabled(!Plugin.Instance.Images.Enabled);
            Plugin.Instance.Log.LogInfo("IMAGE_PROBE enabled=" + Plugin.Instance.Images.Enabled);
        }
        if (Time.realtimeSinceStartup < next) return;
        next = Time.realtimeSinceStartup + 0.5f;
        foreach (var view in UnityEngine.Object.FindObjectsOfType<Image>())
        {
            var sprite = view.overrideSprite;
            if (!sprite) continue;
            var rect = sprite.rect;
            Write(view, "sprite", sprite.name, rect.width, rect.height,
                new { texture = sprite.texture.name, x = rect.x, y = rect.y,
                    pivotX = sprite.pivot.x, pivotY = sprite.pivot.y,
                    border = sprite.border.ToString(), pixelsPerUnit = sprite.pixelsPerUnit,
                    imageType = view.type.ToString(), hasOverride = (bool)view.m_OverrideSprite });
        }
        foreach (var view in UnityEngine.Object.FindObjectsOfType<RawImage>())
        {
            if (view.texture) Write(view, "texture", view.texture.name, view.texture.width, view.texture.height,
                new { uv = view.uvRect.ToString(), shader = view.material?.shader?.name });
        }
        foreach (var view in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
        {
            if (view.sprite) Write(view, "sprite-renderer", view.sprite.name, view.sprite.rect.width,
                view.sprite.rect.height, new { texture = view.sprite.texture.name });
        }
    }
    private static void Write(Component view, string kind, string name, float width, float height, object detail)
    {
        string scope = view.TryCast<Image>() is { } image ? Plugin.Instance.Images.Scope(image) : "";
        string json = JsonSerializer.Serialize(new { context = UiLocalizer.Context(view), kind, name, width, height, scope, detail });
        if (seen.Count >= 12000 || !seen.Add(json)) return;
        string path = Path.Combine(BepInEx.Paths.PluginPath, "Rlyeh.Localizer", "cache", "image-view-probe.jsonl");
        File.AppendAllText(path, json + "\n");
    }
}
