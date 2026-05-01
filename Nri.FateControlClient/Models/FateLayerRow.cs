namespace Nri.FateControlClient.Models;

public sealed class FateLayerRow
{
    public int LayerNumber { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int FlatModifier { get; set; }
    public double Intensity { get; set; }
    public string Mode { get; set; } = "flat";
    public string EffectCode { get; set; } = "None";
}
