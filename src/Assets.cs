using Godot;
using System;
using System.Collections.Generic;

public static class Assets
{
    public static string ProjectDir { get; private set; }
    public static string PckPath { get; private set; }
    public static bool PackMounted { get; private set; }

    static readonly Dictionary<string, Variant> JsonCache = new();

    public static void Init()
    {
        ProjectDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/');
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

    public static bool ResourceExists(string relative)
    {
        return ResourceLoader.Exists(ResPath(relative));
    }

    public static string ReadText(string relative)
    {
        string packed = ResPath(relative);
        if (!FileAccess.FileExists(packed)) return null;
        using FileAccess file = FileAccess.Open(packed, FileAccess.ModeFlags.Read);
        return file?.GetAsText();
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

    public static Variant ReadJsonRes(string resPath)
    {
        if (JsonCache.TryGetValue(resPath, out Variant cached)) return cached;

        if (!FileAccess.FileExists(resPath)) return default;
        using FileAccess file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        if (file == null) return default;

        Variant parsed = Json.ParseString(file.GetAsText());
        JsonCache[resPath] = parsed;
        return parsed;
    }

    public static Godot.Collections.Array ListDir(string relative, bool recursive, string extension, int limit = 0)
    {
        Godot.Collections.Array result = new();
        if (!PackMounted) return result;

        string root = ResPath(relative);
        if (DirAccess.Open(root) == null) return result;

        Walk(root, relative.Replace("\\", "/").Trim('/'), recursive, extension, limit, result);
        return result;
    }

    static void Walk(string absolute, string relative, bool recursive, string extension, int limit, Godot.Collections.Array result)
    {
        if (limit > 0 && result.Count >= limit) return;

        DirAccess dir = DirAccess.Open(absolute);
        if (dir == null) return;

        foreach (string name in dir.GetDirectories())
        {
            if (name.StartsWith(".")) continue;
            if (!recursive) continue;

            string childRelative = relative.Length == 0 ? name : relative + "/" + name;
            Walk(absolute + "/" + name, childRelative, recursive, extension, limit, result);
            if (limit > 0 && result.Count >= limit) return;
        }

        foreach (string file in dir.GetFiles())
        {
            if (file.StartsWith(".")) continue;

            string name = LogicalName(file);
            if (extension.Length > 0 && !name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;

            result.Add(relative.Length == 0 ? name : relative + "/" + name);
            if (limit > 0 && result.Count >= limit) return;
        }
    }

    static string LogicalName(string packedName)
    {
        if (packedName.EndsWith(".remap")) return packedName[..^6];
        if (packedName.EndsWith(".import")) return packedName[..^7];
        return packedName;
    }

    public static ImageTexture LoadTexture(string relative)
    {
        if (string.IsNullOrEmpty(relative)) return null;

        string packed = ResPath(relative);
        if (!ResourceLoader.Exists(packed)) return null;

        Texture2D loaded = ResourceLoader.Load<Texture2D>(packed);
        if (loaded is ImageTexture texture) return texture;
        return loaded == null ? null : ImageTexture.CreateFromImage(loaded.GetImage());
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
