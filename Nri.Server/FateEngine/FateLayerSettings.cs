namespace Nri.Server.FateEngine;

public sealed class FateLayerSettings
{
    public int LayerNumber { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Intensity { get; set; }
    public string Mode { get; set; } = "flat";
    public int FlatModifier { get; set; }
}
