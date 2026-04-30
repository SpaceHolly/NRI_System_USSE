namespace Nri.Server.FateEngine;

public sealed class FateEngineRequest
{
    public int BaseRoll { get; set; }
    public int DieSides { get; set; }
    public string RollType { get; set; } = "generic";
    public string ActorId { get; set; } = string.Empty;
    public string SceneId { get; set; } = "default";
    public int? Seed { get; set; }
}
