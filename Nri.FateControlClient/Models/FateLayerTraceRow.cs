namespace Nri.FateControlClient.Models;

public sealed class FateLayerTraceRow
{
    public int LayerNumber { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public string EffectCode { get; set; } = string.Empty;
    public string EffectDisplayName { get; set; } = string.Empty;
    public string InfluenceType { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public bool Applied { get; set; }
    public bool AllowedForDie { get; set; }
    public int InputValue { get; set; }
    public int Modifier { get; set; }
    public int OutputValue { get; set; }
    public string Reason { get; set; } = string.Empty;
}
