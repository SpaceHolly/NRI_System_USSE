using System.Collections.Generic;
using System;

namespace Nri.Server.Content;

public sealed class GameContentRegistry
{
    public Dictionary<string, GameContentRecord> Skills { get; set; } = NewMap();
    public Dictionary<string, GameContentRecord> ClassSkills { get; set; } = NewMap();
    public Dictionary<string, GameContentRecord> Classes { get; set; } = NewMap();
    public Dictionary<string, GameContentRecord> Races { get; set; } = NewMap();
    public Dictionary<string, GameContentRecord> Items { get; set; } = NewMap();
    public Dictionary<string, GameContentRecord> ItemTemplates { get; set; } = NewMap();
    public Dictionary<string, GameContentRecord> Holdings { get; set; } = NewMap();
    public Dictionary<string, GameContentRecord> Magic { get; set; } = NewMap();

    private static Dictionary<string, GameContentRecord> NewMap() => new Dictionary<string, GameContentRecord>(StringComparer.OrdinalIgnoreCase);
}
