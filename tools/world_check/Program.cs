using System;
using System.Collections.Generic;
using System.IO;
using GridGame.Core.Content;
using Newtonsoft.Json;

// dotnet run --project tools/world_check -- <draft.json> [<draft.json> ...]
// Loads Assets/GridGame/Resources/Content, merges every draft's regions into one world and prints WorldContent.Validate's
// problems. Exit 0 only when every draft deserializes and nothing is reported.
static class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("usage: world_check <draft.json> [<draft.json> ...]"); return 2; }
        string content = Path.Combine(RepoRoot(), "Assets", "GridGame", "Resources", "Content");
        var db = ContentLoader.Load(name =>
        {
            var f = Path.Combine(content, name + ".json");
            return File.Exists(f) ? File.ReadAllText(f) : null;
        });
        var world = new WorldDef();
        int bad = 0;
        foreach (var path in args)
        {
            try
            {
                var w = JsonConvert.DeserializeObject<WorldDef>(File.ReadAllText(path), ContentLoader.Settings);
                world.Regions.AddRange(w.Regions);
                Console.WriteLine($"{path}: {w.Regions.Count} region(s)");
            }
            catch (Exception e) { Console.WriteLine($"{path}: does not load: {e.Message}"); bad++; }
        }
        var p = new List<string>();
        WorldContent.Validate(db, world, p);
        foreach (var s in p) Console.WriteLine(s);
        Console.WriteLine($"{p.Count} problem(s)");
        return bad + p.Count == 0 ? 0 : 1;
    }

    static string RepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "Assets", "GridGame"))) return d.FullName;
        throw new DirectoryNotFoundException("no Assets/GridGame above " + AppContext.BaseDirectory);
    }
}
