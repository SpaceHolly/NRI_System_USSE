using System.Collections.Generic;

namespace Nri.Server.FateEngine;

public sealed class FateLayerResult
{
    public int LayerNumber { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public string EffectCode { get; set; } = string.Empty;
    public string EffectDisplayName { get; set; } = string.Empty;
    public string InfluenceType { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool AllowedForDie { get; set; }
    public bool Applied { get; set; }
    public int InputValue { get; set; }
    public int Modifier { get; set; }
    public int OutputValue { get; set; }
    public List<int> CandidateRolls { get; set; } = new List<int>();
    public int SelectedValue { get; set; }
    public int DistributionShift { get; set; }
    public int AnomalyShift { get; set; }
    public int ChaosShift { get; set; }
    public string CalculationDetails { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
