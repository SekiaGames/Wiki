using Godot;
using System.Collections.Generic;

public static class Assets
{
    public static string ProjectDir { get; private set; }
    public static string DiskRoot { get; private set; }
    public static string PckPath { get; private set; }
    public static bool PackMounted { get; private set; }

    static readonly Dictionary<string, Variant> JsonCache = new();

    public static void Init()
    {
        ProjectDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/');
        DiskRoot = ProjectDir + "/../Extract/extract";
        PckPath = ProjectDir + "/../extract.pck";
        PackMounted = false;

        if (FileAccess.FileExists(PckPath))
        {
            PackMounted = ProjectSettings.LoadResourcePack(PckPath);
        }
    }

    public static string ResPath(string relative)
    {
        return "res://extract/" + relative.Replace("\\", "/").TrimStart('/');
    }

    public static string DiskPath(string relative)
    {
        return DiskRoot + "/" + relative.Replace("\\", "/").TrimStart('/');
    }

    public static bool ResourceExists(string relative)
    {
        return ResourceLoader.Exists(ResPath(relative));
    }

    public static string ReadText(string relative)
    {
        if (PackMounted)
        {
            string packed = ResPath(relative);
            if (FileAccess.FileExists(packed))
            {
                using FileAccess file = FileAccess.Open(packed, FileAccess.ModeFlags.Read);
                if (file != null) return file.GetAsText();
            }
        }

        string disk = DiskPath(relative);
        if (!FileAccess.FileExists(disk)) return null;
        using FileAccess fallback = FileAccess.Open(disk, FileAccess.ModeFlags.Read);
        return fallback?.GetAsText();
    }

    public static Variant ReadJson(string relative)
    {
        if (JsonCache.TryGetValue(relative, out Variant cached)) return cached;

        string text = ReadText(relative);
        if (string.IsNullOrEmpty(text)) return default;

        Variant parsed = Json.ParseString(text);
        JsonCache[relative] = parsed;
        return parsed;
    }

    public static Godot.Collections.Array ListDir(string relative, bool recursive, string extension, int limit = 0)
    {
        Godot.Collections.Array result = new();
        string root = DiskPath(relative);
        if (!DirAccess.DirExistsAbsolute(root)) return result;

        Walk(root, relative, recursive, extension, limit, result);
        return result;
    }

    static void Walk(string absolute, string relative, bool recursive, string extension, int limit, Godot.Collections.Array result)
    {
        if (limit > 0 && result.Count >= limit) return;

        DirAccess dir = DirAccess.Open(absolute);
        if (dir == null) return;

        dir.ListDirBegin();
        while (true)
        {
            string name = dir.GetNext();
            if (string.IsNullOrEmpty(name)) break;
            if (name.StartsWith(".")) continue;

            string childAbsolute = absolute + "/" + name;
            string childRelative = relative.Length == 0 ? name : relative + "/" + name;

            if (dir.CurrentIsDir())
            {
                if (recursive) Walk(childAbsolute, childRelative, recursive, extension, limit, result);
                continue;
            }

            if (extension.Length > 0 && !name.EndsWith(extension)) continue;

            result.Add(childRelative);
            if (limit > 0 && result.Count >= limit)
            {
                dir.ListDirEnd();
                return;
            }
        }

        dir.ListDirEnd();
    }

    public static ImageTexture LoadTexture(string relative)
    {
        if (string.IsNullOrEmpty(relative)) return null;

        if (PackMounted)
        {
            string packed = ResPath(relative);
            if (ResourceLoader.Exists(packed))
            {
                Texture2D loaded = ResourceLoader.Load<Texture2D>(packed);
                if (loaded is ImageTexture texture) return texture;
                if (loaded != null) return ImageTexture.CreateFromImage(loaded.GetImage());
            }
        }

        string disk = DiskPath(relative);
        if (!FileAccess.FileExists(disk)) return null;

        Image image = Image.LoadFromFile(disk);
        if (image == null || image.IsEmpty()) return null;
        return ImageTexture.CreateFromImage(image);
    }

    public static AudioStream LoadAudio(string relative)
    {
        if (!PackMounted) return null;
        string packed = ResPath(relative);
        if (!ResourceLoader.Exists(packed)) return null;
        return ResourceLoader.Load<AudioStream>(packed);
    }

    public static PackedScene LoadScene(string relative)
    {
        if (!PackMounted) return null;
        string packed = ResPath(relative);
        if (!ResourceLoader.Exists(packed)) return null;
        return ResourceLoader.Load<PackedScene>(packed);
    }
}
