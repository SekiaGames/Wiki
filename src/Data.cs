using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Table
{
    public string[] Columns;
    public List<string[]> Rows = new();

    public Dictionary<string, string> Dict(string[] row)
    {
        Dictionary<string, string> item = new();
        for (int i = 0; i < Columns.Length; i++)
        {
            item[Columns[i]] = i < row.Length ? row[i] : "";
        }
        return item;
    }
}

public sealed class UnitRow
{
    public string Id, Name, En, Race, Klass, Icon, ModelBase, ModelWc3, Tip;
    public int Level, Hp, Armor, Speed, Gold, Lumber, Mana;
    public List<string> HeroAbilities = new();
    public List<string> Abilities = new();
    public Dictionary<string, string> UnitUI, UnitData, UnitBalance, UnitAbilities;
}

public sealed class AbilityRow
{
    public string Id, Name, En, Race, Icon, Pos, Anim, Tip;
    public int Levels, Cool, Cost;
    public Dictionary<string, string> Raw;
}

public sealed class DoodadRow
{
    public string Id, Name, En, Category, Dir, Base, File;
    public int Variants;
    public float Scale;
}

public sealed class MediaRow
{
    public string Path, Name, Dir, Family;
    public string FamilyLabel => Families.Label(Family);
}

public static class Families
{
    static readonly Dictionary<string, string> Map = new()
    {
        ["Units"] = "单位", ["Buildings"] = "建筑", ["Doodads"] = "装饰物", ["Abilities"] = "技能",
        ["Objects"] = "物件", ["Environment"] = "环境", ["SharedModels"] = "共享模型",
        ["ReplaceableTextures"] = "可替换贴图", ["TerrainArt"] = "地形", ["Textures"] = "贴图",
        ["UI"] = "界面", ["Sound"] = "音效", ["Fonts"] = "字体",
    };

    public static string Label(string family)
    {
        if (string.IsNullOrEmpty(family)) return "其他";
        return Map.TryGetValue(family, out string label) ? label : family;
    }
}

public static class Data
{
    public static List<UnitRow> Units { get; private set; }
    public static Dictionary<string, UnitRow> UnitById { get; private set; }
    public static List<AbilityRow> Abilities { get; private set; }
    public static Dictionary<string, AbilityRow> AbilityById { get; private set; }
    public static List<DoodadRow> Doodads { get; private set; }
    public static Dictionary<string, DoodadRow> DoodadById { get; private set; }

    public static List<MediaRow> Models { get; private set; }
    public static List<MediaRow> Sounds { get; private set; }
    public static List<MediaRow> Textures { get; private set; }

    public static List<string> TableFiles { get; private set; }
    public static List<string> TextFiles { get; private set; }

    public static int ModelCount => Models?.Count ?? 0;
    public static int SoundCount => Sounds?.Count ?? 0;
    public static int TextureCount => Textures?.Count ?? 0;

    static readonly Dictionary<string, Table> Tables = new();
    static readonly Dictionary<string, Dictionary<string, string>> TextFamilies = new();
    static readonly Dictionary<string, Texture2D> IconCache = new();

    public static void Warm()
    {
        TableFiles = FileNames(Assets.ListDir("Tables", false, ".json"));
        TextFiles = FileNames(Assets.ListDir("Texts", false, ".json"));
        LoadUnits();
        LoadAbilities();
        LoadDoodads();
    }

    public static void WarmMedia()
    {
        Models ??= BuildMedia(".glb");
        Sounds ??= BuildMedia(".wav");
        Textures ??= BuildMedia(".png");
    }

    static List<string> FileNames(Godot.Collections.Array paths)
    {
        List<string> result = new();
        foreach (Variant path in paths) result.Add(path.AsString());
        return result;
    }

    static string Base(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash < 0 ? path : path[(slash + 1)..];
    }

    static string Dir(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }

    public static Table Table(string suffix)
    {
        if (Tables.TryGetValue(suffix, out Table cached)) return cached;

        string match = TableFiles.FirstOrDefault(path =>
        {
            string name = Base(path);
            return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                   && !name.StartsWith("Custom", StringComparison.OrdinalIgnoreCase)
                   && !name.StartsWith("Melee", StringComparison.OrdinalIgnoreCase);
        });

        Table table = match == null ? null : ParseTable(match);
        Tables[suffix] = table;
        return table;
    }

    static Table ParseTable(string relative)
    {
        Variant payload = Assets.ReadJson(relative);
        if (payload.VariantType != Variant.Type.Dictionary) return null;

        Godot.Collections.Dictionary root = payload.AsGodotDictionary();
        Table table = new();

        Godot.Collections.Array columns = root["columns"].AsGodotArray();
        table.Columns = new string[columns.Count];
        for (int i = 0; i < columns.Count; i++) table.Columns[i] = columns[i].AsString();

        if (!root.ContainsKey("rows")) return table;
        foreach (Variant rowVariant in root["rows"].AsGodotArray())
        {
            Godot.Collections.Array cells = rowVariant.AsGodotArray();
            string[] row = new string[cells.Count];
            for (int i = 0; i < cells.Count; i++) row[i] = cells[i].AsString();
            table.Rows.Add(row);
        }

        return table;
    }

    public static Dictionary<string, string> Texts(string suffix)
    {
        if (TextFamilies.TryGetValue(suffix, out Dictionary<string, string> cached)) return cached;

        IEnumerable<string> files = TextFiles
            .Where(path => Base(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => VariantRank(Base(path)))
            .ThenBy(path => path);

        Dictionary<string, string> merged = new();
        foreach (string file in files)
        {
            Variant payload = Assets.ReadJson(file);
            if (payload.VariantType != Variant.Type.Dictionary) continue;
            Godot.Collections.Dictionary root = payload.AsGodotDictionary();
            if (!root.ContainsKey("keys")) continue;

            foreach (KeyValuePair<Variant, Variant> pair in root["keys"].AsGodotDictionary())
            {
                string key = pair.Key.AsString();
                if (!merged.ContainsKey(key)) merged[key] = pair.Value.AsString();
            }
        }

        TextFamilies[suffix] = merged;
        return merged;
    }

    static int VariantRank(string name)
    {
        if (name.StartsWith("Melee", StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.StartsWith("CustomV1", StringComparison.OrdinalIgnoreCase)) return 2;
        if (name.StartsWith("CustomV0", StringComparison.OrdinalIgnoreCase)) return 3;
        return 0;
    }

    static string Clean(string text)
    {
        return string.IsNullOrEmpty(text) ? "" : text.Replace("|n", " · ").Replace("|c", "").Trim();
    }

    static int Int(string text)
    {
        return int.TryParse(text, out int value) ? value : 0;
    }

    static float Float(string text)
    {
        return float.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : 0f;
    }

    static List<string> Split(string text)
    {
        List<string> parts = new();
        if (string.IsNullOrEmpty(text) || text == "-" || text == "_") return parts;

        foreach (string part in text.Split(','))
        {
            string item = part.Trim();
            if (item.Length > 0 && item != "-" && item != "_") parts.Add(item);
        }

        return parts;
    }

    static string NormalizeRace(string race, string wc3Path)
    {
        string value = (race ?? "").Trim().ToLowerInvariant();
        if (value is "human" or "orc" or "undead" or "nightelf") return value;

        string[] segments = (wc3Path ?? "").Replace('/', '\\').Split('\\');
        string fromPath = segments.Length > 1 ? segments[1].ToLowerInvariant() : "";
        return fromPath is "human" or "orc" or "undead" or "nightelf" ? fromPath : "neutral";
    }

    static void LoadUnits()
    {
        Table ui = Table("UnitUI.json");
        if (ui == null) return;

        Dictionary<string, Dictionary<string, string>> dataById = Index(Table("UnitData.json"), "unitID");
        Dictionary<string, Dictionary<string, string>> balanceById = Index(Table("UnitBalance.json"), "unitBalanceID");
        Dictionary<string, Dictionary<string, string>> abilityById = Index(Table("UnitAbilities.json"), "unitAbilID");
        Dictionary<string, string> strings = Texts("UnitStrings.json");
        Dictionary<string, string> funcs = Texts("UnitFunc.json");

        List<UnitRow> rows = new();
        foreach (string[] raw in ui.Rows)
        {
            Dictionary<string, string> row = ui.Dict(raw);
            string id = row["unitUIID"];
            if (string.IsNullOrEmpty(id)) continue;

            string file = row["file"].Replace('\\', '/');
            string modelBase = Base(file);

            UnitRow unit = new()
            {
                Id = id,
                UnitUI = row,
                UnitData = dataById.GetValueOrDefault(id) ?? new Dictionary<string, string>(),
                UnitBalance = balanceById.GetValueOrDefault(id) ?? new Dictionary<string, string>(),
                UnitAbilities = abilityById.GetValueOrDefault(id) ?? new Dictionary<string, string>(),
                ModelWc3 = file,
                ModelBase = modelBase,
                Icon = funcs.TryGetValue(id + ".Art", out string art) ? art : "",
                Name = Clean(strings.TryGetValue(id + ".Name", out string name) ? name : modelBase),
                En = Clean(row.GetValueOrDefault("comment(s)", modelBase)),
                Tip = Clean(strings.TryGetValue(id + ".Ubertip", out string tip) ? tip : ""),
            };

            unit.HeroAbilities = Split(unit.UnitAbilities.GetValueOrDefault("heroAbilList", ""));
            unit.Abilities = Split(unit.UnitAbilities.GetValueOrDefault("abilList", ""));
            unit.Race = NormalizeRace(unit.UnitData.GetValueOrDefault("race", ""), file);
            unit.Klass = unit.UnitBalance.GetValueOrDefault("isbldg", "") == "1" ? "building"
                : unit.HeroAbilities.Count > 0 ? "hero" : "unit";
            unit.Level = Int(unit.UnitBalance.GetValueOrDefault("level", ""));
            unit.Hp = Int(unit.UnitBalance.GetValueOrDefault("realHP", "0"));
            if (unit.Hp <= 0) unit.Hp = Int(unit.UnitBalance.GetValueOrDefault("HP", ""));
            unit.Mana = Int(unit.UnitBalance.GetValueOrDefault("manaN", ""));
            unit.Armor = Int(unit.UnitBalance.GetValueOrDefault("realdef", "0"));
            unit.Speed = Int(unit.UnitBalance.GetValueOrDefault("spd", ""));
            unit.Gold = Int(unit.UnitBalance.GetValueOrDefault("goldcost", ""));
            unit.Lumber = Int(unit.UnitBalance.GetValueOrDefault("lumbercost", ""));

            rows.Add(unit);
        }

        Units = rows;
        UnitById = rows.ToDictionary(row => row.Id, row => row);
    }

    static Dictionary<string, Dictionary<string, string>> Index(Table table, string keyColumn)
    {
        Dictionary<string, Dictionary<string, string>> result = new();
        if (table == null) return result;

        int keyIndex = Array.IndexOf(table.Columns, keyColumn);
        if (keyIndex < 0) return result;

        foreach (string[] row in table.Rows)
        {
            if (keyIndex >= row.Length) continue;
            result[row[keyIndex]] = table.Dict(row);
        }
        return result;
    }

    public static void LoadAbilities()
    {
        if (Abilities != null) return;

        Table table = Table("AbilityData.json");
        if (table == null) return;

        Dictionary<string, string> strings = Texts("AbilityStrings.json");
        Dictionary<string, string> funcs = Texts("AbilityFunc.json");

        List<AbilityRow> rows = new();
        foreach (string[] raw in table.Rows)
        {
            Dictionary<string, string> row = table.Dict(raw);
            string id = row.TryGetValue("alias", out string alias) && !string.IsNullOrEmpty(alias) ? alias : row.GetValueOrDefault("code", "");
            if (string.IsNullOrEmpty(id)) continue;

            string icon = funcs.TryGetValue(id + ".Art", out string art) ? art : "";
            List<string> anims = Split(funcs.TryGetValue(id + ".Animnames", out string anim) ? anim : "");

            rows.Add(new AbilityRow
            {
                Id = id,
                Name = Clean(strings.TryGetValue(id + ".Name", out string name) ? name : row.GetValueOrDefault("comment(s)", id)),
                En = Clean(row.GetValueOrDefault("comment(s)", id)),
                Race = row.GetValueOrDefault("race", ""),
                Icon = icon,
                Pos = funcs.TryGetValue(id + ".Buttonpos", out string pos) ? pos : funcs.GetValueOrDefault(id + ".ButtonPos", ""),
                Anim = anims.Count > 0 ? anims[0] : "Spell",
                Levels = Int(row.GetValueOrDefault("levels", "")),
                Cool = Int(row.GetValueOrDefault("Cool1", "")),
                Cost = Int(row.GetValueOrDefault("Cost1", "")),
                Tip = Clean(strings.TryGetValue(id + ".Ubertip", out string tip) ? tip : ""),
                Raw = row,
            });
        }

        Abilities = rows;
        AbilityById = rows.GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
    }

    static void LoadDoodads()
    {
        Table table = Table("Doodads.json");
        if (table == null) return;

        Dictionary<string, string> world = Texts("WorldEditStrings.json");

        List<DoodadRow> rows = new();
        foreach (string[] raw in table.Rows)
        {
            Dictionary<string, string> row = table.Dict(raw);
            string file = row.GetValueOrDefault("file", "").Replace('\\', '/');
            if (string.IsNullOrEmpty(file)) continue;

            string key = "WorldEditStrings." + row.GetValueOrDefault("Name", "");
            string eastrring = world.TryGetValue(key, out string name) ? name : "";

            rows.Add(new DoodadRow
            {
                Id = row.GetValueOrDefault("doodID", ""),
                Name = Clean(string.IsNullOrEmpty(eastrring) ? row.GetValueOrDefault("comment", Base(file)) : eastrring),
                En = Clean(row.GetValueOrDefault("comment", Base(file))),
                Category = row.GetValueOrDefault("category", ""),
                Dir = Dir(file),
                Base = Base(file),
                File = file,
                Variants = Math.Max(1, Int(row.GetValueOrDefault("numVar", "1"))),
                Scale = Float(row.GetValueOrDefault("defScale", "1")),
            });
        }

        Doodads = rows;
        DoodadById = rows.ToDictionary(row => row.Id, row => row);
    }

    static List<MediaRow> BuildMedia(string extension)
    {
        List<MediaRow> rows = new();
        Godot.Collections.Array files = Assets.ListDir("", true, extension);

        foreach (Variant item in files)
        {
            string path = item.AsString();
            string dir = Dir(path);
            string family = dir.Split('/')[0];
            rows.Add(new MediaRow
            {
                Path = path,
                Name = Base(path)[..^extension.Length],
                Dir = dir,
                Family = family,
            });
        }

        rows.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return rows;
    }

    public static Texture2D IconFor(string art)
    {
        if (string.IsNullOrEmpty(art)) return null;

        string path = art.Replace('\\', '/');
        if (path.EndsWith(".blp", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4] + ".png";
        }

        if (IconCache.TryGetValue(path, out Texture2D cached)) return cached;

        Texture2D texture = Assets.LoadTexture(path);
        IconCache[path] = texture;
        return texture;
    }

    public static Texture2D IconFor(UnitRow unit) => IconFor(unit.Icon);
    public static Texture2D IconFor(AbilityRow ability) => IconFor(ability.Icon);

    static Dictionary<string, string> _modelIndex;

    public static Dictionary<string, string> ModelIndex
    {
        get
        {
            if (_modelIndex != null) return _modelIndex;

            Models ??= BuildMedia(".glb");
            _modelIndex = new Dictionary<string, string>();
            foreach (MediaRow row in Models)
            {
                string key = row.Name.ToLowerInvariant();
                if (!_modelIndex.TryGetValue(key, out string existing) || existing.Contains("PORTRAIT", StringComparison.OrdinalIgnoreCase))
                {
                    _modelIndex[key] = row.Path;
                }
            }
            return _modelIndex;
        }
    }

    public static string FindModel(string baseName)
    {
        if (string.IsNullOrEmpty(baseName)) return null;
        return ModelIndex.TryGetValue(baseName.ToLowerInvariant(), out string path) ? path : null;
    }

    public static string SceneFor(string glbPath)
    {
        return string.IsNullOrEmpty(glbPath) ? null : glbPath[..glbPath.LastIndexOf('.')] + ".tscn";
    }

    public static List<string> DirFiles(string dir, string extension)
    {
        List<string> names = new();
        foreach (Variant path in Assets.ListDir(dir, false, extension)) names.Add(Base(path.AsString()));
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    public static string SoundKind(string fileName)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(fileName);
        if (name.EndsWith("YesAttack") || name.Contains("YesAttack")) return "YesAttack";
        if (name.Contains("Attack")) return "Attack";
        if (name.Contains("Pissed")) return "Pissed";
        if (name.Contains("Warcry")) return "Warcry";
        if (name.Contains("Ready")) return "Ready";
        if (name.Contains("What")) return "What";
        if (name.Contains("Yes")) return "Yes";
        if (name.Contains("Death")) return "Death";
        if (name.Contains("Birth")) return "Birth";
        return "Other";
    }

    public static float[] MaterialDetailAlpha(string wc3RelativePath)
    {
        Variant payload = Assets.ReadJson(wc3RelativePath);
        if (payload.VariantType != Variant.Type.Dictionary) return null;

        Godot.Collections.Dictionary root = payload.AsGodotDictionary();
        if (!root.ContainsKey("materials")) return null;

        Godot.Collections.Array materials = root["materials"].AsGodotArray();
        float[] result = new float[materials.Count];
        for (int i = 0; i < materials.Count; i++)
        {
            result[i] = -1f;
            Godot.Collections.Dictionary material = materials[i].AsGodotDictionary();
            if (!material.ContainsKey("layers")) continue;

            Godot.Collections.Array layers = material["layers"].AsGodotArray();
            if (layers.Count < 2) continue;

            Godot.Collections.Dictionary layer = layers[1].AsGodotDictionary();
            float peak = layer.ContainsKey("alpha") ? layer["alpha"].AsSingle() : 1f;

            if (layer.ContainsKey("tracks"))
            {
                Godot.Collections.Dictionary tracks = layer["tracks"].AsGodotDictionary();
                if (tracks.ContainsKey("KMTA"))
                {
                    Godot.Collections.Array keys = tracks["KMTA"].AsGodotDictionary()["keys"].AsGodotArray();
                    float animated = 0f;
                    foreach (Variant key in keys)
                    {
                        Godot.Collections.Array pair = key.AsGodotArray();
                        if (pair.Count < 2) continue;
                        animated = Mathf.Max(animated, pair[1].AsSingle());
                    }
                    peak = animated;
                }
            }

            result[i] = peak;
        }

        return result;
    }

    public static string Wc3JsonFor(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath) || !scenePath.EndsWith(".tscn")) return null;
        return scenePath[..^5] + ".wc3.json";
    }

    public static string ClassLabel(string klass) => klass switch
    {
        "hero" => "英雄",
        "unit" => "单位",
        "building" => "建筑",
        "ability" => "技能",
        "doodad" => "装饰物",
        "model" => "模型",
        "sound" => "音效",
        "texture" => "贴图",
        _ => klass,
    };

    public static string RaceLabel(string race) => race switch
    {
        "human" => "人族",
        "orc" => "兽族",
        "undead" => "不死",
        "nightelf" => "暗夜",
        "neutral" => "中立",
        _ => race,
    };
}
