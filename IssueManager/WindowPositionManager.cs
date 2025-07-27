using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

public static class WindowPositionManager
{
    private static readonly string ConfigPath = @"C:\ProgramData\RK Tools\IssueManager\WindowPositions.json";

    private class WindowPositions
    {
        public Dictionary<string, WindowPlacement> Positions { get; set; } = new Dictionary<string, WindowPlacement>();
    }

    private static WindowPositions LoadAll()
    {
        if (!File.Exists(ConfigPath)) return new WindowPositions();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonConvert.DeserializeObject<WindowPositions>(json) ?? new WindowPositions();
        }
        catch
        {
            return new WindowPositions();
        }
    }

    public static void Save(string windowKey, double left, double top)
    {
        var all = LoadAll();
        all.Positions[windowKey] = new WindowPlacement { Left = left, Top = top };

        try
        {
            var json = JsonConvert.SerializeObject(all, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
        catch { /* fail silently */ }
    }

    public static WindowPlacement Load(string windowKey)
    {
        var all = LoadAll();
        if (all.Positions.TryGetValue(windowKey, out var pos))
            return pos;

        return new WindowPlacement(); // fallback with default values
    }

}

public class WindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
}
