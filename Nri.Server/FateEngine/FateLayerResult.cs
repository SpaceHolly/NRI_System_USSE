namespace Nri.Server.FateEngine;

public sealed class FateLayerResult
{
    public int LayerNumber { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool AllowedForDie { get; set; }
    public bool Applied { get; set; }
    public int InputValue { get; set; }
    public int Modifier { get; set; }
    public int OutputValue { get; set; }
    public string Reason { get; set; } = string.Empty;
}
