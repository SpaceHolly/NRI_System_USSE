using System.Collections.Generic;

namespace Nri.Server.FateEngine;

public sealed class FateEngineResult
{
    public int BaseRoll { get; set; }
    public int DieSides { get; set; }
    public int FateValue { get; set; }
    public bool Applied { get; set; }
    public string SkippedReason { get; set; } = string.Empty;
    public List<FateLayerResult> Layers { get; set; } = new();
}
