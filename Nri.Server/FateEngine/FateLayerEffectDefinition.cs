namespace Nri.Server.FateEngine;

public sealed class FateLayerEffectDefinition
{
    public int LayerNumber { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public string EffectCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string InfluenceType { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public bool CanUseChaos { get; set; }
    public bool CanUseAnomaly { get; set; }
    public string Description { get; set; } = string.Empty;
}
