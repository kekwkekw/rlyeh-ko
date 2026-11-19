using System.Security.Cryptography;
using System.Text.Json;
using Rlyeh.Localizer.Core;

internal static class ImageCatalogChecks
{
    internal static void Run(Action<bool, string> check)
    {
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
        string hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        ImageCatalog.Entry Entry(string scope) => new()
        {
            Name = "theme_title", Scope = scope, Width = 1, Height = 1,
            File = "files/" + hash + ".png", Sha256 = hash, Bytes = png.Length
        };
        string directory = Path.Combine(Path.GetTempPath(), "rlyeh-image-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "files"));
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "files", hash + ".png"), png);
            void Write(params ImageCatalog.Entry[] entries) => File.WriteAllText(Path.Combine(directory, "catalog.json"),
                JsonSerializer.Serialize(new ImageCatalog.Document { Entries = entries.ToList() }));
            bool Rejected()
            {
                try { ImageCatalog.Load(directory); return false; }
                catch (InvalidDataException) { return true; }
            }
            Write(Entry("rogue:1"), Entry("rogue:2"));
            var catalog = ImageCatalog.Load(directory);
            check(catalog.Count == 2 && catalog.Find("sprite", "theme_title", 1, 1, "rogue:2")?.Entry.Scope == "rogue:2",
                "same-named theme images resolve by native theme identity");
            check(catalog.Find("sprite", "theme_title", 1, 1, "rogue:3") == null &&
                catalog.Find("sprite", "theme_title", 2, 1, "rogue:1") == null,
                "unknown themes and changed sprite dimensions retain the source");
            Write(Entry("rogue:1"), Entry("rogue:1"));
            check(Rejected(), "duplicate image targets reject the whole catalog");
            var unsafeEntry = Entry(""); unsafeEntry.File = "../outside.png"; Write(unsafeEntry);
            check(Rejected(), "image payload paths are fixed to content hashes");
            var dimensions = Entry(""); dimensions.Width = 2; Write(dimensions);
            check(Rejected(), "cropped sprite exports cannot silently change the original canvas");
            Write(Entry(""));
            byte[] corrupt = png.ToArray(); corrupt[^1] ^= 1;
            File.WriteAllBytes(Path.Combine(directory, "files", hash + ".png"), corrupt);
            check(Rejected(), "modified PNG bytes are rejected before Unity decoding");
        }
        finally { Directory.Delete(directory, true); }
    }
}
