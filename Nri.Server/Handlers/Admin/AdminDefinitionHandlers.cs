using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.Application;
using Nri.Server.Application.Services;
using Nri.Server.Infrastructure;
using Nri.Server.Transport;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Handlers.Admin;

public sealed class AdminDefinitionHandlers
{
    private readonly ClassDefinitionService _classService;
    private readonly SkillDefinitionService _skillService;
    private readonly INriRepositoryFactory _repositories;

    public AdminDefinitionHandlers(INriRepositoryFactory repositories, ClassDefinitionService classService, SkillDefinitionService skillService)
    {
        _repositories = repositories;
        _classService = classService;
        _skillService = skillService;
    }

    public IEnumerable<IRequestHandler> CreateHandlers()
    {
        return new IRequestHandler[]
        {
            new DelegateRequestHandler(CommandNames.AdminDefinitionsClassList, HandleGetClassList),
            new DelegateRequestHandler(CommandNames.AdminDefinitionsClassGet, HandleGetClassByCode),
            new DelegateRequestHandler(CommandNames.AdminDefinitionsClassSave, HandleSaveClass),
            new DelegateRequestHandler(CommandNames.AdminDefinitionsSkillList, HandleGetSkillList),
            new DelegateRequestHandler(CommandNames.AdminDefinitionsSkillGet, HandleGetSkillByCode),
            new DelegateRequestHandler(CommandNames.AdminDefinitionsSkillSave, HandleSaveSkill)
        };
    }

    private ResponseEnvelope HandleGetClassList(CommandContext context)
    {
        var request = new GetClassListRequest { IncludeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived") };
        var response = new GetClassListResponse { Items = _classService.GetAll(request.IncludeArchived) };
        return Ok("Class definitions loaded.", new Dictionary<string, object> { { "items", response.Items.Select(ToPayload).Cast<object>().ToArray() } });
    }

    private ResponseEnvelope HandleGetClassByCode(CommandContext context)
    {
        var request = new GetClassByCodeRequest { Code = RequireString(context.Request.Payload, "code") };
        var response = new GetClassByCodeResponse { Item = _classService.GetByCode(request.Code) };
        return Ok("Class definition loaded.", new Dictionary<string, object> { { "item", response.Item == null ? new Dictionary<string, object>() : ToPayload(response.Item) } });
    }

    private ResponseEnvelope HandleSaveClass(CommandContext context)
    {
        var request = new SaveClassRequest { Definition = ReadClassDefinition(context.Request.Payload) };
        var actor = RequireActor(context);
        var response = _classService.Save(request.Definition, actor.Id);
        return Ok(response.Created ? "Class definition created." : "Class definition updated.", new Dictionary<string, object>
        {
            { "created", response.Created },
            { "item", ToPayload(response.Item) }
        });
    }

    private ResponseEnvelope HandleGetSkillList(CommandContext context)
    {
        var request = new GetSkillListRequest { IncludeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived") };
        var response = new GetSkillListResponse { Items = _skillService.GetAll(request.IncludeArchived) };
        return Ok("Skill definitions loaded.", new Dictionary<string, object> { { "items", response.Items.Select(ToPayload).Cast<object>().ToArray() } });
    }

    private ResponseEnvelope HandleGetSkillByCode(CommandContext context)
    {
        var request = new GetSkillByCodeRequest { Code = RequireString(context.Request.Payload, "code") };
        var response = new GetSkillByCodeResponse { Item = _skillService.GetByCode(request.Code) };
        return Ok("Skill definition loaded.", new Dictionary<string, object> { { "item", response.Item == null ? new Dictionary<string, object>() : ToPayload(response.Item) } });
    }

    private ResponseEnvelope HandleSaveSkill(CommandContext context)
    {
        var request = new SaveSkillRequest { Definition = ReadSkillDefinition(context.Request.Payload) };
        var actor = RequireActor(context);
        var response = _skillService.Save(request.Definition, actor.Id);
        return Ok(response.Created ? "Skill definition created." : "Skill definition updated.", new Dictionary<string, object>
        {
            { "created", response.Created },
            { "item", ToPayload(response.Item) }
        });
    }

    private UserAccount RequireActor(CommandContext context)
    {
        if (context.Session == null) throw new UnauthorizedAccessException("Session is required.");
        var actor = _repositories.Accounts.GetById(context.Session.UserId) ?? throw new KeyNotFoundException("Account not found.");
        RoleGuard.EnsureRole(actor, UserRole.Admin, UserRole.SuperAdmin);
        return actor;
    }

    private static ClassDefinitionDto ReadClassDefinition(IDictionary<string, object> payload)
    {
        var map = RequireMap(payload, "definition");
        return new ClassDefinitionDto
        {
            Code = RequireString(map, "code"),
            Name = GetString(map, "name"),
            Description = GetString(map, "description"),
            DirectionCode = RequireString(map, "directionCode"),
            BranchCode = RequireString(map, "branchCode"),
            RootClassCode = RequireString(map, "rootClassCode"),
            ParentClassCode = GetString(map, "parentClassCode"),
            Level = GetInt(map, "level"),
            GrantedSkillCodes = GetStringList(map, "grantedSkillCodes"),
            RequiredClassCodes = GetStringList(map, "requiredClassCodes"),
            IsActive = GetBool(map, "isActive", true),
            Status = ParseEnum<DefinitionStatus>(GetString(map, "status"), DefinitionStatus.Draft)
        };
    }

    private static SkillDefinitionDto ReadSkillDefinition(IDictionary<string, object> payload)
    {
        var map = RequireMap(payload, "definition");
        return new SkillDefinitionDto
        {
            Code = RequireString(map, "code"),
            Name = GetString(map, "name"),
            Description = GetString(map, "description"),
            Tier = GetInt(map, "tier"),
            MaxLevel = GetInt(map, "maxLevel"),
            SkillCategory = ParseEnum<SkillCategory>(GetString(map, "skillCategory"), SkillCategory.Undefined),
            IsClassSkill = GetBool(map, "isClassSkill", false),
            RequiredClassCodes = GetStringList(map, "requiredClassCodes"),
            RequiredSkillCodes = GetStringList(map, "requiredSkillCodes"),
            Levels = ReadSkillLevels(map),
            IsActive = GetBool(map, "isActive", true),
            Status = ParseEnum<DefinitionStatus>(GetString(map, "status"), DefinitionStatus.Draft)
        };
    }

    private static List<SkillLevelDefinition> ReadSkillLevels(IDictionary<string, object> map)
    {
        var result = new List<SkillLevelDefinition>();
        if (!map.TryGetValue("levels", out var raw) || !(raw is IEnumerable items)) return result;
        foreach (var item in items)
        {
            if (!(item is IDictionary<string, object> levelMap)) continue;
            result.Add(new SkillLevelDefinition
            {
                Level = GetInt(levelMap, "level"),
                Description = GetString(levelMap, "description"),
                Requirements = ReadRequirements(levelMap),
                Effects = ReadEffects(levelMap)
            });
        }

        return result;
    }

    private static List<RequirementDefinition> ReadRequirements(IDictionary<string, object> map)
    {
        var result = new List<RequirementDefinition>();
        if (!map.TryGetValue("requirements", out var raw) || !(raw is IEnumerable items)) return result;
        foreach (var item in items)
        {
            if (!(item is IDictionary<string, object> requirementMap)) continue;
            result.Add(new RequirementDefinition
            {
                RequirementType = GetString(requirementMap, "requirementType"),
                TargetCode = GetString(requirementMap, "targetCode"),
                MinimumValue = TryGetInt(requirementMap, "minimumValue"),
                Description = GetString(requirementMap, "description")
            });
        }

        return result;
    }

    private static List<EffectDefinition> ReadEffects(IDictionary<string, object> map)
    {
        var result = new List<EffectDefinition>();
        if (!map.TryGetValue("effects", out var raw) || !(raw is IEnumerable items)) return result;
        foreach (var item in items)
        {
            if (!(item is IDictionary<string, object> effectMap)) continue;
            result.Add(new EffectDefinition
            {
                EffectType = GetString(effectMap, "effectType"),
                TargetCode = GetString(effectMap, "targetCode"),
                Value = GetString(effectMap, "value"),
                Description = GetString(effectMap, "description")
            });
        }

        return result;
    }

    private static Dictionary<string, object> ToPayload(ClassDefinitionDto dto)
    {
        return new Dictionary<string, object>
        {
            { "code", dto.Code }, { "name", dto.Name }, { "description", dto.Description },
            { "directionCode", dto.DirectionCode }, { "branchCode", dto.BranchCode }, { "rootClassCode", dto.RootClassCode },
            { "parentClassCode", dto.ParentClassCode }, { "level", dto.Level },
            { "grantedSkillCodes", dto.GrantedSkillCodes.Cast<object>().ToArray() },
            { "requiredClassCodes", dto.RequiredClassCodes.Cast<object>().ToArray() },
            { "isActive", dto.IsActive }, { "status", dto.Status.ToString() },
            { "createdUtc", dto.CreatedUtc }, { "updatedUtc", dto.UpdatedUtc }
        };
    }

    private static Dictionary<string, object> ToPayload(SkillDefinitionDto dto)
    {
        return new Dictionary<string, object>
        {
            { "code", dto.Code }, { "name", dto.Name }, { "description", dto.Description },
            { "tier", dto.Tier }, { "maxLevel", dto.MaxLevel }, { "skillCategory", dto.SkillCategory.ToString() },
            { "isClassSkill", dto.IsClassSkill },
            { "requiredClassCodes", dto.RequiredClassCodes.Cast<object>().ToArray() },
            { "requiredSkillCodes", dto.RequiredSkillCodes.Cast<object>().ToArray() },
            { "levels", dto.Levels.Select(level => new Dictionary<string, object>
                {
                    { "level", level.Level },
                    { "description", level.Description },
                    { "requirements", level.Requirements.Select(requirement => new Dictionary<string, object>
                        {
                            { "requirementType", requirement.RequirementType },
                            { "targetCode", requirement.TargetCode },
                            { "minimumValue", requirement.MinimumValue.HasValue ? (object)requirement.MinimumValue.Value : string.Empty },
                            { "description", requirement.Description }
                        }).Cast<object>().ToArray() },
                    { "effects", level.Effects.Select(effect => new Dictionary<string, object>
                        {
                            { "effectType", effect.EffectType },
                            { "targetCode", effect.TargetCode },
                            { "value", effect.Value },
                            { "description", effect.Description }
                        }).Cast<object>().ToArray() }
                }).Cast<object>().ToArray() },
            { "isActive", dto.IsActive }, { "status", dto.Status.ToString() },
            { "createdUtc", dto.CreatedUtc }, { "updatedUtc", dto.UpdatedUtc }
        };
    }

    private static ResponseEnvelope Ok(string message, Dictionary<string, object> payload)
    {
        return new ResponseEnvelope { Status = ResponseStatus.Ok, Message = message, Payload = payload };
    }

    private static IDictionary<string, object> RequireMap(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || !(value is IDictionary<string, object> map)) throw new ArgumentException($"{key} is required.");
        return map;
    }

    private static string RequireString(IDictionary<string, object> map, string key)
    {
        var value = GetString(map, key);
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{key} is required.");
        return value;
    }

    private static string GetString(IDictionary<string, object> map, string key)
    {
        return map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    }

    private static int GetInt(IDictionary<string, object> map, string key)
    {
        return TryGetInt(map, key) ?? 0;
    }

    private static int? TryGetInt(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return null;
        if (value is int intValue) return intValue;
        if (int.TryParse(Convert.ToString(value), out var parsed)) return parsed;
        return null;
    }

    private static bool GetBool(IDictionary<string, object> map, string key, bool defaultValue)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return defaultValue;
        if (value is bool boolValue) return boolValue;
        if (bool.TryParse(Convert.ToString(value), out var parsed)) return parsed;
        return defaultValue;
    }

    private static List<string> GetStringList(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || !(value is IEnumerable items)) return new List<string>();
        var result = new List<string>();
        foreach (var item in items)
        {
            var text = Convert.ToString(item);
            if (!string.IsNullOrWhiteSpace(text)) result.Add(text);
        }

        return result;
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
    }

    private sealed class DelegateRequestHandler : IRequestHandler
    {
        private readonly Func<CommandContext, ResponseEnvelope> _handler;

        public DelegateRequestHandler(string commandName, Func<CommandContext, ResponseEnvelope> handler)
        {
            CommandName = commandName;
            _handler = handler;
        }

        public string CommandName { get; }

        public ResponseEnvelope Handle(CommandContext context)
        {
            return _handler(context);
        }
    }
}
