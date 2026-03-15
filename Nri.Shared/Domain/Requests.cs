using System;

namespace Nri.Shared.Domain;

public abstract class RequestBase : EntityBase
{
    public string SessionId { get; set; } = string.Empty;
    public string CreatorUserId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
}

public class ActionRequest : RequestBase
{
    public string CharacterId { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}

public class DiceRollRequest : RequestBase
{
    public string CharacterId { get; set; } = string.Empty;
    public string Expression { get; set; } = "1d20";
    public bool IsShadowRoll { get; set; }
}

public class CharacterApplicationRequest : RequestBase
{
    public string ApplicantUserId { get; set; } = string.Empty;
    public string CharacterConcept { get; set; } = string.Empty;
}
