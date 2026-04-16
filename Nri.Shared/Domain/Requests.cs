using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public enum RequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Expired,
    Archived
}

public enum RequestVisibility
{
    Public,
    PlayerShadow,
    AdminOnlyShadow
}

public class RequestDecision
{
    public string? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string AdminComment { get; set; } = string.Empty;
}

public class RequestHistoryEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string ActorUserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public abstract class RequestBase : EntityBase
{
    public string RequestType { get; set; } = string.Empty;
    public string CreatorUserId { get; set; } = string.Empty;
    public string RelatedUserId { get; set; } = string.Empty;
    public string? CharacterId { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public string Description { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Fingerprint { get; set; } = string.Empty;
    public int RejectionCountForFingerprint { get; set; }
    public RequestDecision Decision { get; set; } = new RequestDecision();
    public List<RequestHistoryEntry> History { get; set; } = new List<RequestHistoryEntry>();
}

public class ActionRequest : RequestBase
{
    public string ActionCode { get; set; } = string.Empty;
}

public class DiceFormulaSpec
{
    public int DiceCount { get; set; }
    public int DiceSides { get; set; }
    public int Modifier { get; set; }
    public string Normalized { get; set; } = "1d20";
}

public class DiceRollResult
{
    public string NormalizedFormula { get; set; } = "1d20";
    public List<int> Rolls { get; set; } = new List<int>();
    public int Modifier { get; set; }
    public int Total { get; set; }
    public RequestVisibility Visibility { get; set; } = RequestVisibility.Public;
    public string ApprovedByUserId { get; set; } = string.Empty;
    public DateTime ApprovedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DiceRollRequest : RequestBase
{
    public string RawFormula { get; set; } = "1d20";
    public DiceFormulaSpec Formula { get; set; } = new DiceFormulaSpec();
    public RequestVisibility Visibility { get; set; } = RequestVisibility.Public;
    public bool IsTestRoll { get; set; }
    public string TestRollOwnerUserId { get; set; } = string.Empty;
    public DiceRollResult? Result { get; set; }
}

public class CharacterApplicationRequest : RequestBase
{
    public string ApplicantUserId { get; set; } = string.Empty;
    public string CharacterConcept { get; set; } = string.Empty;
}
