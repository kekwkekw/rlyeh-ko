using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Rlyeh.Localizer.Core;

public sealed class ImageCatalog
{
    public sealed class Entry
    {
        public string Kind { get; set; } = "sprite";
        public string Name { get; set; } = "";
        public string Scope { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string File { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public int Bytes { get; set; }
        public string Key => MakeKey(Kind, Name, Width, Height, Scope);
    }
    public sealed class Document
    {
        public int Version { get; set; } = 1;
        public List<Entry> Entries { get; set; } = new();
    }
    public sealed record Asset(Entry Entry, byte[] Png);
    private readonly Dictionary<string, Asset> assets = new(StringComparer.Ordinal);
    public int Count => assets.Count;
    public static string MakeKey(string kind, string name, int width, int height, string scope)
        => string.Join("\u001f", scope, kind, name, width.ToString(), height.ToString());

    public static ImageCatalog Load(string directory)
    {
        var catalog = new ImageCatalog();
        string path = Path.Combine(directory, "catalog.json");
        if (!System.IO.File.Exists(path)) return catalog;
        var doc = JsonSerializer.Deserialize<Document>(System.IO.File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (doc == null || doc.Version != 1 || doc.Entries == null || doc.Entries.Count > 256)
            throw new InvalidDataException("Unsupported image catalog");
        long total = 0;
        foreach (var entry in doc.Entries)
        {
            if (entry == null || entry.Kind is not ("sprite" or "texture") ||
                string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 160 || entry.Name.Contains('\u001f') ||
                entry.Scope == null || entry.Scope.Length > 160 || entry.Scope.Contains('\u001f') ||
                entry.Width < 1 || entry.Width > 4096 || entry.Height < 1 || entry.Height > 4096 ||
                entry.Bytes < 33 || entry.Bytes > 8 * 1024 * 1024 || entry.Sha256 == null ||
                entry.Sha256.Length != 64 || entry.Sha256.Any(c => !"0123456789abcdef".Contains(c)) ||
                entry.File != "files/" + entry.Sha256 + ".png" || catalog.assets.ContainsKey(entry.Key))
                throw new InvalidDataException("Invalid or duplicate image target");
            total += entry.Bytes;
            if (total > 64 * 1024 * 1024) throw new InvalidDataException("Image catalog exceeds 64 MiB");
            string file = Path.Combine(directory, entry.File);
            if (new FileInfo(file).Length != entry.Bytes) throw new InvalidDataException("Image byte count mismatch");
            byte[] png = System.IO.File.ReadAllBytes(file);
            ValidatePng(entry, png);
            catalog.assets.Add(entry.Key, new(entry, png));
        }
        return catalog;
    }
    public static void ValidatePng(Entry entry, byte[] png)
    {
        ReadOnlySpan<byte> signature = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (png.Length < 33 || png.Length != entry.Bytes || !png.AsSpan(0, 8).SequenceEqual(signature) ||
            !png.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)) != entry.Width ||
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)) != entry.Height ||
            Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant() != entry.Sha256)
            throw new InvalidDataException("Image hash, PNG header, or dimensions mismatch");
    }
    public Asset? Find(string kind, string name, int width, int height, string scope)
    {
        if (assets.TryGetValue(MakeKey(kind, name, width, height, scope), out var asset)) return asset;
        return assets.GetValueOrDefault(MakeKey(kind, name, width, height, ""));
    }
}
