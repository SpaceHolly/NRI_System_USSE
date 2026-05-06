using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.Content;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope CharacterClassAssign(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var classCode = RequireLength(PayloadReader.GetString(context.Request.Payload, "classCode"), 1, 128, "classCode");
        var level = PayloadReader.GetInt(context.Request.Payload, "level") ?? 1;
        if (level < 1) throw new ArgumentException("level must be >= 1.");

        var character = GetCharacter(characterId);
        EnsureCharacterEditAllowed(actor, character.Id);
        EnsureCharacterDefaults(character);

        var classDef = _contentService.GetSnapshot().Classes.Values.FirstOrDefault(x => string.Equals(x.Code, classCode, StringComparison.OrdinalIgnoreCase));
        if (classDef == null) throw new KeyNotFoundException("Класс не найден в справочнике.");

        var levelCap = ParseIntField(classDef, "levelCap");
        if (levelCap > 0 && level > levelCap) throw new ArgumentException($"level exceeds levelCap ({levelCap}).");

        character.CharacterClasses ??= new List<CharacterClassState>();
        var existing = character.CharacterClasses.FirstOrDefault(x => string.Equals(x.ClassCode, classCode, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            character.CharacterClasses.Add(new CharacterClassState { ClassCode = classCode, Level = level, LearnedUtc = DateTime.UtcNow });
        }
        else
        {
            existing.Level = level;
        }

        _repositories.Characters.Replace(character);
        WriteAudit("character", actor.Id, "class.assign", $"{character.Id}:{classCode}:{level}");
        _logger.Admin($"character.class.assign actor={actor.Login} characterId={character.Id} classCode={classCode} level={level}");

        return Ok("Класс назначен.", new Dictionary<string, object> { { "characterId", character.Id }, { "classCode", classCode }, { "level", level } });
    }

    public ResponseEnvelope CharacterClassesGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var character = GetCharacter(characterId);
        var owner = GetAccount(character.OwnerUserId);
        if (!CanViewCharacter(actor, owner, character))
        {
            throw new UnauthorizedAccessException("Character classes unavailable.");
        }
        var classes = character.CharacterClasses ?? new List<CharacterClassState>();
        var defs = _contentService.GetSnapshot().Classes.Values.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        var items = classes.Select(c =>
        {
            defs.TryGetValue(c.ClassCode, out var def);
            return new Dictionary<string, object>
            {
                { "classCode", c.ClassCode },
                { "displayName", def?.DisplayName ?? c.ClassCode },
                { "level", c.Level },
                { "branchCode", GetFieldString(def, "branchCode") },
                { "description", GetFieldString(def, "description") },
                { "learnedUtc", c.LearnedUtc }
            };
        }).Cast<object>().ToArray();

        return Ok("Character classes loaded.", new Dictionary<string, object> { { "items", items }, { "total", items.Length } });
    }

    private static int ParseIntField(GameContentRecord record, string field)
    {
        var value = GetFieldString(record, field);
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static string GetFieldString(GameContentRecord? record, string field)
    {
        if (record == null) return string.Empty;
        if (!record.ExtraFields.TryGetValue(field, out var value)) return string.Empty;
        return value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }
}
