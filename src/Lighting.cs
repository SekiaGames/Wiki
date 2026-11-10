using Godot;
using System.Collections.Generic;

public static class Lighting
{
    public static bool Ready { get; private set; }
    public static string Family { get; private set; } = "DNCLordaeron";
    public static readonly List<string> Families = new();

    static readonly Dictionary<string, float[]> TerrainTables = new();
    static readonly Dictionary<string, float[]> UnitTables = new();

    public static void Load()
    {
        Variant json = Assets.ReadJsonRes("res://assets/common/lighting/dnc.json");
        if (json.VariantType != Variant.Type.Dictionary) return;

        foreach (var entry in json.AsGodotDictionary())
        {
            string name = entry.Key.AsString();
            Godot.Collections.Dictionary node = entry.Value.AsGodotDictionary();
            float[] terrain = ToArray(node, "terrain");
            float[] unit = ToArray(node, "unit");
            if (unit == null) continue;

            Families.Add(name);
            if (terrain != null) TerrainTables[name] = terrain;
            UnitTables[name] = unit;
        }

        Families.Sort();
        if (!UnitTables.ContainsKey(Family) && Families.Count > 0) Family = Families[0];
        Ready = UnitTables.Count > 0;
    }

    static float[] ToArray(Godot.Collections.Dictionary node, string key)
    {
        if (!node.ContainsKey(key)) return null;
        Godot.Collections.Array values = node[key].AsGodotArray();
        if (values.Count == 0) return null;

        float[] result = new float[values.Count];
        for (int i = 0; i < values.Count; i++) result[i] = (float)values[i];
        return result;
    }

    public static void SetFamily(string family)
    {
        if (!UnitTables.ContainsKey(family)) return;
        Family = family;
    }

    public static void Sample(float hour, out Vector3 direction, out Vector3 color, out Vector3 ambient)
    {
        direction = new Vector3(-0.42f, 0.78f, 0.46f);
        color = Vector3.One;
        ambient = new Vector3(0.35f, 0.35f, 0.35f);
        if (!UnitTables.TryGetValue(Family, out float[] table)) return;

        int count = table.Length / 12;
        if (count < 2) return;

        float position = Mathf.Clamp(hour, 0f, 24f) / 24f * (count - 1);
        int low = Mathf.Clamp(Mathf.FloorToInt(position), 0, count - 1);
        int high = Mathf.Min(low + 1, count - 1);
        float blend = position - low;

        int a = low * 12;
        int b = high * 12;
        Vector3 mixDirection = new(
            Mathf.Lerp(table[a + 1], table[b + 1], blend),
            Mathf.Lerp(table[a + 2], table[b + 2], blend),
            Mathf.Lerp(table[a + 3], table[b + 3], blend));
        Vector3 diffuse = new(
            Mathf.Lerp(table[a + 4], table[b + 4], blend),
            Mathf.Lerp(table[a + 5], table[b + 5], blend),
            Mathf.Lerp(table[a + 6], table[b + 6], blend));
        float diffuseIntensity = Mathf.Lerp(table[a + 7], table[b + 7], blend);
        Vector3 ambientColor = new(
            Mathf.Lerp(table[a + 8], table[b + 8], blend),
            Mathf.Lerp(table[a + 9], table[b + 9], blend),
            Mathf.Lerp(table[a + 10], table[b + 10], blend));
        float ambientIntensity = Mathf.Lerp(table[a + 11], table[b + 11], blend);

        direction = new Vector3(mixDirection.X, mixDirection.Z, -mixDirection.Y).Normalized();
        color = diffuse * diffuseIntensity;
        ambient = ambientColor * ambientIntensity;
    }
}
