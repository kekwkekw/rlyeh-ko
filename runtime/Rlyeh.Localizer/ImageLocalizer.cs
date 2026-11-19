using HarmonyLib;
using Rlyeh.Localizer.Core;
using Rog.View;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rlyeh.Localizer;

// UI component references are replaced. Original Sprite/Texture objects remain available for restoration.
internal sealed class ImageLocalizer
{
    private sealed class SpriteState
    {
        internal Image View = null!;
        internal Sprite? Original, OriginalOverride;
        internal string Scope = "";
    }
    private sealed class TextureState
    {
        internal RawImage View = null!;
        internal Texture? Original;
    }
    private sealed record SpritePair(Sprite Original, Sprite Replacement);
    private readonly Plugin plugin;
    private readonly ImageCatalog catalog;
    private readonly Dictionary<int, SpriteState> views = new();
    private readonly Dictionary<int, TextureState> rawViews = new();
    private readonly Dictionary<int, string> scopes = new();
    private readonly Dictionary<string, Texture2D> textures = new();
    private readonly Dictionary<(int, string), SpritePair> sprites = new();
    private readonly Dictionary<int, Sprite> originalSprites = new();
    private readonly Dictionary<int, Texture> originalTextures = new();
    private readonly HashSet<string> logged = new();
    private bool applying;
    private float nextPrune;
    internal bool Enabled { get; private set; }

    internal ImageLocalizer(Plugin plugin, string directory, bool enabled)
    {
        this.plugin = plugin;
        Enabled = enabled;
        try { catalog = ImageCatalog.Load(directory); }
        catch (Exception ex)
        {
            plugin.Log.LogWarning("Image catalog rejected: " + ex.Message);
            catalog = new ImageCatalog();
        }
        plugin.Log.LogInfo("Local image catalog: " + catalog.Count);
    }
    internal void Bind(Image? view, int themeId)
    {
        if (view) scopes[view!.GetInstanceID()] = "rogue:" + themeId;
    }
    internal string Scope(Image view) => scopes.GetValueOrDefault(view.GetInstanceID(), "");
    private Sprite? Original(Sprite? value)
        => value && originalSprites.TryGetValue(value!.GetInstanceID(), out var original) ? original : value;
    private Texture? Original(Texture? value)
        => value && originalTextures.TryGetValue(value!.GetInstanceID(), out var original) ? original : value;
    private SpriteState Capture(Image view)
    {
        int id = view.GetInstanceID();
        if (!views.TryGetValue(id, out var state) || state.View != view)
            views[id] = state = new() { View = view };
        state.Original = Original(view.sprite);
        state.OriginalOverride = Original(view.m_OverrideSprite);
        state.Scope = Scope(view);
        return state;
    }
    internal void Assign(Image view, ref Sprite? value, bool isOverride)
    {
        if (applying || catalog.Count == 0 || !view) return;
        var state = Capture(view);
        value = Original(value);
        if (isOverride) state.OriginalOverride = value;
        else state.Original = value;
        if (Enabled) value = Translate(value, state.Scope);
    }
    internal void Refresh(Image view)
    {
        if (applying || catalog.Count == 0 || !view) return;
        var state = Capture(view);
        applying = true;
        try
        {
            view.sprite = Enabled ? Translate(state.Original, state.Scope) : state.Original;
            view.overrideSprite = Enabled ? Translate(state.OriginalOverride, state.Scope) : state.OriginalOverride;
        }
        finally { applying = false; }
    }
    internal void Assign(RawImage view, ref Texture? value)
    {
        if (applying || catalog.Count == 0 || !view) return;
        value = Original(value);
        rawViews[view.GetInstanceID()] = new() { View = view, Original = value };
        if (!Enabled || !value || view.uvRect != new Rect(0, 0, 1, 1)) return;
        var asset = catalog.Find("texture", value!.name, value.width, value.height, "");
        if (asset == null) return;
        Texture? translated = Load(asset);
        if (!translated) return;
        originalTextures[translated!.GetInstanceID()] = value;
        value = translated;
    }
    internal void Refresh(RawImage view)
    {
        if (applying || catalog.Count == 0 || !view) return;
        Texture? value = view.texture;
        Assign(view, ref value);
        applying = true;
        try { view.texture = value; }
        finally { applying = false; }
    }
    private Texture2D? Load(ImageCatalog.Asset asset)
    {
        string key = asset.Entry.Sha256;
        if (textures.TryGetValue(key, out var cached) && cached) return cached;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, asset.Png, true) ||
            texture.width != asset.Entry.Width || texture.height != asset.Entry.Height)
        {
            Object.Destroy(texture);
            throw new InvalidDataException("Could not decode local image " + asset.Entry.Name);
        }
        texture.name = "Rlyeh.Localizer:" + key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Object.DontDestroyOnLoad(texture);
        textures[key] = texture;
        return texture;
    }
    private Sprite? Translate(Sprite? original, string scope)
    {
        if (!original) return original;
        var rect = original!.rect;
        var asset = catalog.Find("sprite", original.name, (int)rect.width, (int)rect.height, scope);
        if (asset == null) return original;
        var key = (original.GetInstanceID(), asset.Entry.Key);
        if (sprites.TryGetValue(key, out var pair) && pair.Original == original && pair.Replacement && pair.Replacement.texture)
            return pair.Replacement;
        var texture = Load(asset);
        if (!texture) return original;
        var replacement = Sprite.Create(texture, new Rect(0, 0, rect.width, rect.height),
            new Vector2(original.pivot.x / rect.width, original.pivot.y / rect.height),
            original.pixelsPerUnit, 0, SpriteMeshType.FullRect, original.border);
        replacement.name = "Rlyeh.Localizer:" + original.name;
        Object.DontDestroyOnLoad(replacement);
        sprites[key] = new(original, replacement);
        originalSprites[replacement.GetInstanceID()] = original;
        if (logged.Add(asset.Entry.Key)) plugin.Log.LogInfo($"IMAGE applied scope={scope} name={original.name} size={rect.width}x{rect.height}");
        return replacement;
    }
    // Future F2 can call this at the chosen screen/scene reload boundary.
    internal void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        applying = true;
        try
        {
            foreach (var state in views.Values)
            {
                if (!state.View) continue;
                state.View.sprite = enabled ? Translate(state.Original, state.Scope) : state.Original;
                state.View.overrideSprite = enabled ? Translate(state.OriginalOverride, state.Scope) : state.OriginalOverride;
            }
            foreach (var state in rawViews.Values)
            {
                if (!state.View) continue;
                state.View.texture = state.Original;
            }
        }
        finally { applying = false; }
        if (enabled) foreach (var state in rawViews.Values.ToArray()) if (state.View) Refresh(state.View);
    }
    internal void Tick()
    {
        if (Time.realtimeSinceStartup < nextPrune) return;
        nextPrune = Time.realtimeSinceStartup + 2;
        foreach (int id in views.Where(x => !x.Value.View).Select(x => x.Key).ToArray())
        { views.Remove(id); scopes.Remove(id); }
        foreach (int id in rawViews.Where(x => !x.Value.View).Select(x => x.Key).ToArray()) rawViews.Remove(id);
        foreach (var key in sprites.Where(x => !x.Value.Original || !x.Value.Replacement || !x.Value.Replacement.texture).Select(x => x.Key).ToArray())
        {
            var pair = sprites[key];
            if (pair.Replacement)
            {
                originalSprites.Remove(pair.Replacement.GetInstanceID());
                Object.Destroy(pair.Replacement);
            }
            sprites.Remove(key);
        }
    }
}

[HarmonyPatch(typeof(Image), "set_sprite")]
internal static class ImageSpritePatch
{
    private static void Prefix(Image __instance, ref Sprite? value)
    { try { Plugin.Instance.Images.Assign(__instance, ref value, false); } catch (Exception ex) { Plugin.Instance.Log.LogWarning("Image sprite: " + ex.Message); } }
}
[HarmonyPatch(typeof(Image), "set_overrideSprite")]
internal static class ImageOverridePatch
{
    private static void Prefix(Image __instance, ref Sprite? value)
    { try { Plugin.Instance.Images.Assign(__instance, ref value, true); } catch (Exception ex) { Plugin.Instance.Log.LogWarning("Image override: " + ex.Message); } }
}
[HarmonyPatch(typeof(Image), "OnEnable")]
internal static class ImageEnablePatch
{
    private static void Postfix(Image __instance)
    { try { Plugin.Instance.Images.Refresh(__instance); } catch (Exception ex) { Plugin.Instance.Log.LogWarning("Image enable: " + ex.Message); } }
}
[HarmonyPatch(typeof(RawImage), "set_texture")]
internal static class RawImageTexturePatch
{
    private static void Prefix(RawImage __instance, ref Texture? value)
    { try { Plugin.Instance.Images.Assign(__instance, ref value); } catch (Exception ex) { Plugin.Instance.Log.LogWarning("Image texture: " + ex.Message); } }
}
[HarmonyPatch(typeof(MaskableGraphic), "OnEnable")]
internal static class RawImageEnablePatch
{
    private static void Postfix(MaskableGraphic __instance)
    {
        try { var raw = __instance.TryCast<RawImage>(); if (raw) Plugin.Instance.Images.Refresh(raw!); }
        catch (Exception ex) { Plugin.Instance.Log.LogWarning("Raw image enable: " + ex.Message); }
    }
}
[HarmonyPatch(typeof(RogueThemeSelectContent), nameof(RogueThemeSelectContent.Setup))]
internal static class ThemeTitleImagePatch
{
    private static void Prefix(RogueThemeSelectContent __instance, Rog.Domain.RogueThemeModel __0)
        => Plugin.Instance.Images.Bind(__instance.m_titleImage, __0.Id);
}
[HarmonyPatch(typeof(RogueThemeBannerView), nameof(RogueThemeBannerView.Setup))]
internal static class ThemeBannerImagePatch
{
    private static void Prefix(RogueThemeBannerView __instance, Rog.Domain.RogueThemeModel __0)
        => Plugin.Instance.Images.Bind(__instance.m_image, __0.Id);
}
[HarmonyPatch(typeof(RogueThemeTopPage), nameof(RogueThemeTopPage.Setup))]
internal static class ThemeLogoImagePatch
{
    private static void Prefix(RogueThemeTopPage __instance, Rog.Domain.RogueThemeModel __0)
        => Plugin.Instance.Images.Bind(__instance.m_logoImage, __0.Id);
}
