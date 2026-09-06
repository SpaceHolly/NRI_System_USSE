using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatNaturalAttackAreaResolver022Gate2
{
    Task<IReadOnlyList<CombatParticipantState>> ResolveTargetsAsync(CombatEncounterState encounter, CombatParticipantState attacker, CombatParticipantState aim, NaturalAttackDefinition attack);
}

public sealed class CombatNaturalAttackAreaResolver022Gate2 : ICombatNaturalAttackAreaResolver022Gate2
{
    private readonly ICombatParticipantRepository _participants;
    private readonly IMongoCollection<BsonDocument> _tokens;

    public CombatNaturalAttackAreaResolver022Gate2(ICombatParticipantRepository participants, MongoContext mongo)
    {
        _participants = participants;
        _tokens = mongo.Database.GetCollection<BsonDocument>("map_token_instances");
    }

    public async Task<IReadOnlyList<CombatParticipantState>> ResolveTargetsAsync(CombatEncounterState encounter, CombatParticipantState attacker, CombatParticipantState aim, NaturalAttackDefinition attack)
    {
        if (encounter == null || attacker == null || aim == null || attack == null) return Array.Empty<CombatParticipantState>();
        if (string.Equals(attack.AreaShape, "single", StringComparison.OrdinalIgnoreCase)) return new[] { aim };
        if (string.IsNullOrWhiteSpace(attacker.MapTokenId) || string.IsNullOrWhiteSpace(aim.MapTokenId))
            throw new InvalidOperationException("natural_attack_area_requires_map_tokens");

        var participants = (await _participants.ListByEncounterAsync(encounter.Id, 500)).ToList();
        var tokenIds = participants.Select(v => v.MapTokenId).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var tokenDocs = _tokens.Find(Builders<BsonDocument>.Filter.In("Id", tokenIds) & Builders<BsonDocument>.Filter.Eq("IsArchived", false)).ToList();
        var points = tokenDocs.ToDictionary(
            token => Text(token, "Id"),
            token => new CombatAreaPoint022Gate2 { X = Number(token, "X"), Y = Number(token, "Y") },
            StringComparer.OrdinalIgnoreCase);
        if (!points.TryGetValue(attacker.MapTokenId, out var origin) || !points.TryGetValue(aim.MapTokenId, out var targetPoint))
            throw new InvalidOperationException("natural_attack_area_map_token_missing");
        origin.ParticipantId = attacker.Id;
        targetPoint.ParticipantId = aim.Id;
        var candidates = participants.Where(v => points.ContainsKey(v.MapTokenId)).Select(v => new CombatAreaPoint022Gate2
        {
            ParticipantId = v.Id,
            X = points[v.MapTokenId].X,
            Y = points[v.MapTokenId].Y
        }).ToArray();
        var resolvedIds = NaturalAttackAreaRules022Gate2.ResolveTargets(attack, origin, targetPoint, candidates).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return participants.Where(v => resolvedIds.Contains(v.Id)).ToArray();
    }

    private static string Text(BsonDocument value, string field) => value.TryGetValue(field, out var result) && !result.IsBsonNull ? result.ToString() : string.Empty;
    private static double Number(BsonDocument value, string field) => value.TryGetValue(field, out var result) && result.IsNumeric ? result.ToDouble() : 0d;
}
