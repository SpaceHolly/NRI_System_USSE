using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope AdminDashboardGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!ClientFlag(nameof(ClientFunctionalizationFeatureFlags.UseFunctionalAdminDashboard)))
            return ClientFunctionalizationDisabled(context.Request.Command);

        var activeProcesses = BuildAdminActiveProcesses();
        var nextActions = BuildAdminNextActions();
        var metrics = new object[]
        {
            Metric("Игроки", Count(_repositories.Accounts, Builders<UserAccount>.Filter.Empty), "Аккаунты и участники кампании"),
            Metric("Персонажи", Count(_repositories.Characters, Builders<Character>.Filter.Empty), "Character v2 / profile-based путь"),
            Metric("Заявки", Count(_repositories.PlayerRequests, Builders<PlayerRequestState>.Filter.Ne(x => x.Status, PlayerRequestStatusIds.Archived)), "Формальные запросы игроков"),
            Metric("Предложения", CountProposalDrafts(Builders<PlayerProposalDraftState>.Filter.Ne(x => x.ProposalStatus, ProposalStatusIds.Archived)), "Черновики и предложения игроков"),
            Metric("Активные процессы", activeProcesses.Length, "Проекты, заявки, предложения, производство и активы")
        };

        return Ok("Admin dashboard loaded.", FunctionalDashboardPayload(true, metrics, activeProcesses, nextActions, Array.Empty<object>()));
    }

    public ResponseEnvelope PlayerDashboardGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ClientFlag(nameof(ClientFunctionalizationFeatureFlags.UseFunctionalPlayerDashboard)))
            return ClientFunctionalizationDisabled(context.Request.Command);

        var activeProcesses = BuildPlayerActiveProcesses(actor);
        var nextActions = BuildPlayerNextActions(actor);
        var characterPairs = ResolvePlayerCharacterHubRows(actor, string.Empty);
        var characters = characterPairs.Select(x => x.Character).Where(x => !string.IsNullOrWhiteSpace(x.Id)).ToArray();
        var metrics = new object[]
        {
            Metric("Мои персонажи", characters.Length, "Доступные персонажи игрока"),
            Metric("Мои заявки", Count(_repositories.PlayerRequests, PlayerRequestOwnerFilter(actor)), "Отправленные GM запросы"),
            Metric("Мои предложения", CountProposalDrafts(PlayerProposalOwnerFilter(actor) & Builders<PlayerProposalDraftState>.Filter.Ne(x => x.ProposalStatus, ProposalStatusIds.Archived)), "Черновики и предложения для GM"),
            Metric("Мои процессы", activeProcesses.Length, "Проекты и работы, видимые игроку"),
            Metric("Следующие действия", nextActions.Length, "Что можно сделать сейчас")
        };

        return Ok("Player dashboard loaded.", FunctionalDashboardPayload(true, metrics, activeProcesses, nextActions, BuildPlayerCharacterCards(characterPairs, actor)));
    }

    public ResponseEnvelope AdminActiveProcessesList(CommandContext context)
    {
        RequireAdmin(context);
        if (!ClientFlag(nameof(ClientFunctionalizationFeatureFlags.UseActiveProcessDashboard)))
            return ClientFunctionalizationDisabled(context.Request.Command);
        return Ok("Admin active processes loaded.", new Dictionary<string, object> { ["items"] = BuildAdminActiveProcesses(), ["builtAtUtc"] = DateTime.UtcNow });
    }

    public ResponseEnvelope PlayerActiveProcessesList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ClientFlag(nameof(ClientFunctionalizationFeatureFlags.UseActiveProcessDashboard)))
            return ClientFunctionalizationDisabled(context.Request.Command);
        return Ok("Player active processes loaded.", new Dictionary<string, object> { ["items"] = BuildPlayerActiveProcesses(actor), ["builtAtUtc"] = DateTime.UtcNow });
    }

    public ResponseEnvelope AdminNextActionsList(CommandContext context)
    {
        RequireAdmin(context);
        if (!ClientFlag(nameof(ClientFunctionalizationFeatureFlags.UseNextActionCards)))
            return ClientFunctionalizationDisabled(context.Request.Command);
        return Ok("Admin next actions loaded.", new Dictionary<string, object> { ["items"] = BuildAdminNextActions(), ["builtAtUtc"] = DateTime.UtcNow });
    }

    public ResponseEnvelope PlayerNextActionsList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ClientFlag(nameof(ClientFunctionalizationFeatureFlags.UseNextActionCards)))
            return ClientFunctionalizationDisabled(context.Request.Command);
        return Ok("Player next actions loaded.", new Dictionary<string, object> { ["items"] = BuildPlayerNextActions(actor), ["builtAtUtc"] = DateTime.UtcNow });
    }

    public ResponseEnvelope CharacterPlayerHubGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!ClientFlag(nameof(ClientFunctionalizationFeatureFlags.UsePlayerCharacterHub)))
            return ClientFunctionalizationDisabled(context.Request.Command);

        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId") ?? string.Empty;
        var characters = ResolvePlayerCharacterHubRows(actor, characterId);
        return Ok("Player character hub loaded.", new Dictionary<string, object>
        {
            ["characters"] = BuildPlayerCharacterCards(characters, actor),
            ["builtAtUtc"] = DateTime.UtcNow
        });
    }

    public ResponseEnvelope CharacterAdminHubGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!ClientFlag(nameof(ClientFunctionalizationFeatureFlags.UseAdminGmConsole)))
            return ClientFunctionalizationDisabled(context.Request.Command);

        var characterId = PayloadReader.GetString(context.Request.Payload, "characterId") ?? string.Empty;
        var filter = string.IsNullOrWhiteSpace(characterId)
            ? Builders<Character>.Filter.Empty
            : Builders<Character>.Filter.Eq(x => x.Id, characterId);
        var actor = RequireAdmin(context);
        var characters = _repositories.Characters.Find(filter).Take(100).Select(c => AdminCharacterHubCard(c, actor)).Cast<object>().ToArray();
        return Ok("Admin character hub loaded.", new Dictionary<string, object>
        {
            ["characters"] = characters,
            ["builtAtUtc"] = DateTime.UtcNow
        });
    }

    private Dictionary<string, object> FunctionalDashboardPayload(bool enabled, object[] metrics, object[] activeProcesses, object[] nextActions, object[] characterCards)
        => new()
        {
            ["isEnabled"] = enabled,
            ["metrics"] = metrics,
            ["activeProcesses"] = activeProcesses,
            ["nextActions"] = nextActions,
            ["characterCards"] = characterCards,
            ["warnings"] = Array.Empty<object>(),
            ["builtAtUtc"] = DateTime.UtcNow
        };

    private ResponseEnvelope ClientFunctionalizationDisabled(string command)
        => Ok("Функциональный пульт выключен настройками модулей.", new Dictionary<string, object>
        {
            ["isEnabled"] = false,
            ["command"] = command,
            ["message"] = "Функциональный пульт выключен настройками модулей.",
            ["metrics"] = Array.Empty<object>(),
            ["activeProcesses"] = Array.Empty<object>(),
            ["nextActions"] = Array.Empty<object>(),
            ["characterCards"] = Array.Empty<object>(),
            ["warnings"] = new object[] { "Включите модуль функционального пульта в разделе «Функции и модули»." },
            ["builtAtUtc"] = DateTime.UtcNow
        });

    private object[] BuildAdminActiveProcesses()
    {
        var items = new List<object>();
        items.AddRange(_repositories.PlayerRequests.Find(Builders<PlayerRequestState>.Filter.Ne(x => x.Status, PlayerRequestStatusIds.Archived))
            .OrderByDescending(x => x.UpdatedAtUtc).Take(8)
            .Select(x => ProcessCard("Заявка", x.Title, ClientStatusLabel(x.Status), ClientPriorityLabel(x.Priority), x.Description, x.Id, "requests")));
        items.AddRange(_mongo.PlayerProposalDrafts.Find(Builders<PlayerProposalDraftState>.Filter.Ne(x => x.ProposalStatus, ProposalStatusIds.Archived))
            .ToEnumerable()
            .OrderByDescending(x => x.UpdatedAtUtc).Take(8)
            .Select(x => ProcessCard("Предложение", x.Title, ClientStatusLabel(x.ProposalStatus), ClientProposalTypeLabel(x.ProposalType), ClientProposalSummary(x), x.Id, "proposals")));
        items.AddRange(_repositories.Projects.Find(Builders<ProjectBaseState>.Filter.Eq(x => x.IsArchived, false))
            .OrderByDescending(x => x.UpdatedAtUtc).Take(8)
            .Select(x => ProcessCard("Проект", x.Name, ClientStatusLabel(x.Status), $"{x.ProgressPercent}%", x.PublicSummary, x.Id, "projects")));
        items.AddRange(_repositories.CraftingProjects.Find(Builders<CraftingProjectState>.Filter.Empty)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(6)
            .Select(x => ProcessCard("Крафт", ClientFirstNonEmpty(x.RecipeName, x.ProjectId, "Проект крафта"), ClientStatusLabel(x.Status), $"{x.ProgressPercent}%", x.QualitySummary, x.Id, "crafting")));
        items.AddRange(_repositories.EngineeringProjects.Find(Builders<EngineeringDesignProjectState>.Filter.Empty)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(6)
            .Select(x => ProcessCard("Инженерия", x.Name, ClientStatusLabel(x.Status), $"{x.ProgressPercent}%", x.IntendedRole, x.Id, "engineering")));
        items.AddRange(_repositories.ManufacturingProjects.Find(Builders<ManufacturingProjectState>.Filter.Eq(x => x.IsArchived, false))
            .OrderByDescending(x => x.UpdatedAtUtc).Take(8)
            .Select(x => ProcessCard("Производство", x.Name, ClientStatusLabel(x.ManufacturingStatus), $"{x.ProgressPercent:0}%", x.Description, x.Id, "production")));
        return items.Take(24).ToArray();
    }

    private object[] BuildPlayerActiveProcesses(UserAccount actor)
    {
        var items = new List<object>();
        items.AddRange(_repositories.PlayerRequests.Find(PlayerRequestOwnerFilter(actor))
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(8)
            .Select(x => ProcessCard("Заявка", x.Title, ClientStatusLabel(x.Status), ClientPriorityLabel(x.Priority), x.Description, x.Id, "requests")));
        items.AddRange(_mongo.PlayerProposalDrafts.Find(PlayerProposalOwnerFilter(actor) & Builders<PlayerProposalDraftState>.Filter.Ne(x => x.ProposalStatus, ProposalStatusIds.Archived))
            .ToEnumerable()
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(8)
            .Select(x => ProcessCard("Предложение", x.Title, ClientStatusLabel(x.ProposalStatus), ClientProposalTypeLabel(x.ProposalType), ClientProposalSummary(x), x.Id, "proposals")));
        items.AddRange(_repositories.Projects.Find(PlayerProjectFilter(actor))
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(8)
            .Select(x => ProcessCard("Проект", x.Name, ClientStatusLabel(x.Status), $"{x.ProgressPercent}%", x.PublicSummary, x.Id, "projects")));
        items.AddRange(_repositories.CraftingProjects.Find(PlayerCraftingFilter(actor))
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(6)
            .Select(x => ProcessCard("Крафт", ClientFirstNonEmpty(x.RecipeName, "Проект крафта"), ClientStatusLabel(x.Status), $"{x.ProgressPercent}%", x.QualitySummary, x.Id, "crafting")));
        items.AddRange(_repositories.EngineeringProjects.Find(PlayerEngineeringFilter(actor))
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(6)
            .Select(x => ProcessCard("Инженерия", x.Name, ClientStatusLabel(x.Status), $"{x.ProgressPercent}%", x.IntendedRole, x.Id, "engineering")));
        items.AddRange(_repositories.ManufacturingProjects.Find(PlayerManufacturingFilter(actor))
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(6)
            .Select(x => ProcessCard("Производство", x.Name, ClientStatusLabel(x.ManufacturingStatus), $"{x.ProgressPercent:0}%", x.Description, x.Id, "production")));
        return items.Take(20).ToArray();
    }

    private object[] BuildAdminNextActions()
    {
        var items = new List<object>();
        items.AddRange(_repositories.PlayerRequests.Find(Builders<PlayerRequestState>.Filter.Eq(x => x.Status, PlayerRequestStatusIds.Submitted))
            .OrderByDescending(x => x.UpdatedAtUtc).Take(6)
            .Select(x => ActionCard("Рассмотреть заявку", x.Title, ClientPriorityLabel(x.Priority), "Открыть заявки игроков", "requests", x.Id)));
        items.AddRange(_mongo.PlayerProposalDrafts.Find(Builders<PlayerProposalDraftState>.Filter.In(x => x.ProposalStatus, new[] { ProposalStatusIds.Submitted, ProposalStatusIds.InGmReview, ProposalStatusIds.ReadyToSubmit }))
            .ToEnumerable()
            .OrderByDescending(x => x.UpdatedAtUtc).Take(6)
            .Select(x => ActionCard("Рассмотреть предложение", x.Title, ClientProposalTypeLabel(x.ProposalType), "Открыть предложения игроков", "proposals", x.Id)));
        items.AddRange(_repositories.Projects.Find(Builders<ProjectBaseState>.Filter.Eq(x => x.ApprovalStatus, ProjectApprovalStatusIds.PendingGmReview))
            .OrderByDescending(x => x.UpdatedAtUtc).Take(4)
            .Select(x => ActionCard("Решить проект", x.Name, ClientStatusLabel(x.Status), "Открыть проекты", "projects", x.Id)));
        items.AddRange(_repositories.ManufacturingProjects.Find(Builders<ManufacturingProjectState>.Filter.Ne(x => x.AcceptanceStatus, ManufacturingAcceptanceStatusIds.NotReady))
            .OrderByDescending(x => x.UpdatedAtUtc).Take(4)
            .Select(x => ActionCard("Проверить приёмку", x.Name, ClientStatusLabel(x.ManufacturingStatus), "Открыть производство", "production", x.Id)));
        return items.ToArray();
    }

    private object[] BuildPlayerNextActions(UserAccount actor)
    {
        var items = new List<object>();
        items.AddRange(_repositories.PlayerRequests.Find(PlayerRequestOwnerFilter(actor))
            .Where(x => x.IsPlayerVisible && (x.Status == PlayerRequestStatusIds.Draft || x.Status == PlayerRequestStatusIds.Rejected))
            .OrderByDescending(x => x.UpdatedAtUtc).Take(5)
            .Select(x => ActionCard("Доработать заявку", x.Title, ClientStatusLabel(x.Status), "Открыть заявки / действия", "requests", x.Id)));
        items.AddRange(_mongo.PlayerProposalDrafts.Find(PlayerProposalOwnerFilter(actor) & Builders<PlayerProposalDraftState>.Filter.In(x => x.ProposalStatus, new[] { ProposalStatusIds.Draft, ProposalStatusIds.ReadyToSubmit, ProposalStatusIds.ChangesRequested }))
            .ToEnumerable()
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(5)
            .Select(x => ActionCard("Доработать предложение", x.Title, ClientStatusLabel(x.ProposalStatus), "Открыть предложения", "proposals", x.Id)));
        items.AddRange(_repositories.Projects.Find(PlayerProjectFilter(actor))
            .Where(x => x.IsPlayerVisible && x.Status == ProjectStatusIds.Draft)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(5)
            .Select(x => ActionCard("Подготовить проект", x.Name, ClientStatusLabel(x.Status), "Открыть проекты", "projects", x.Id)));
        items.AddRange(_repositories.FactoryOrders.Find(PlayerFactoryOrderFilter(actor))
            .Where(x => x.IsPlayerVisible)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(4)
            .Select(x => ActionCard("Проверить заказ", x.Name, ClientStatusLabel(x.Status), "Открыть производство", "production", x.Id)));
        return items.ToArray();
    }

    private sealed class PlayerCharacterHubRow
    {
        public PlayerCharacterHubRow(Character character, CharacterOwnershipState? ownership)
        {
            Character = character;
            Ownership = ownership;
        }

        public Character Character { get; }
        public CharacterOwnershipState? Ownership { get; }
    }

    private PlayerCharacterHubRow[] ResolvePlayerCharacterHubRows(UserAccount actor, string characterId)
    {
        if (!CharacterOwnershipPlayerViewEnabled())
        {
            _logger.Debug("character.player.hub.ownership_required result=disabled");
            return Array.Empty<PlayerCharacterHubRow>();
        }

        var ownerships = _repositories.CharacterOwnerships.Find(FilterDefinition<CharacterOwnershipState>.Empty)
            .Where(x => x.IsPlayerVisible)
            .Where(x => !IsArchivedForPlayer(x))
            .Where(x => string.Equals(x.OwnerUserId, actor.Id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.ControlledByUserId, actor.Id, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(characterId) || string.Equals(x.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .ToArray();

        return ownerships
            .Select(x => new PlayerCharacterHubRow(TryGetCharacter(x.CharacterId), x))
            .Where(x => x.Character != null && !string.IsNullOrWhiteSpace(x.Character.Id))
            .ToArray()!;
    }

    private object[] BuildPlayerCharacterCards(IEnumerable<PlayerCharacterHubRow> rows, UserAccount viewer)
    {
        var cards = new List<object>();
        foreach (var row in rows.Take(12))
        {
            try
            {
                cards.Add(PlayerCharacterHubCard(row.Character, viewer, row.Ownership));
            }
            catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Профиль Character v2 недоступен.", StringComparison.Ordinal))
            {
                _logger.Debug($"character.player.hub.profile_required.controlled characterId={row.Character.Id}");
                cards.Add(PlayerCharacterProfileRequiredCard(row.Character, row.Ownership));
            }
        }

        return cards.ToArray();
    }

    private object[] BuildPlayerCharacterCards(IEnumerable<Character> characters, UserAccount viewer)
        => characters.Take(12).Select(c => PlayerCharacterHubCard(c, viewer, null)).Cast<object>().ToArray();

    private static Dictionary<string, object> PlayerCharacterProfileRequiredCard(Character character, CharacterOwnershipState? ownership)
    {
        var archived = ownership?.IsArchived == true || character.Archived || character.Deleted;
        return new Dictionary<string, object>
        {
            ["characterId"] = character.Id,
            ["name"] = ClientFirstNonEmpty(character.Name, ownership?.CharacterDisplayName, "Без имени"),
            ["campaignId"] = ownership?.CampaignId ?? string.Empty,
            ["race"] = "Не указана",
            ["age"] = "—",
            ["height"] = "—",
            ["health"] = "—",
            ["armor"] = "—",
            ["xpCoins"] = "—",
            ["summary"] = archived
                ? "Персонаж находится в архиве."
                : "Данные персонажа временно недоступны. Обратитесь к мастеру.",
            ["ownerDisplayName"] = ClientFirstNonEmpty(ownership?.OwnerDisplayName, "Не указан"),
            ["controlledByDisplayName"] = ClientFirstNonEmpty(ownership?.ControlledByDisplayName, "Не указан"),
            ["characterKind"] = ClientFirstNonEmpty(ownership?.CharacterKind, MapOwnershipRoleToCharacterKind(ownership?.CharacterRole)),
            ["characterKindDisplayName"] = CharacterKindDisplayName(ClientFirstNonEmpty(ownership?.CharacterKind, MapOwnershipRoleToCharacterKind(ownership?.CharacterRole))),
            ["characterStatus"] = archived ? CharacterStatusIds.Archived : CharacterStatusIds.Inactive,
            ["characterStatusDisplayName"] = CharacterStatusDisplayName(archived ? CharacterStatusIds.Archived : CharacterStatusIds.Inactive),
            ["isActive"] = false,
            ["isArchived"] = archived,
            ["archived"] = archived,
            ["isPlayerVisible"] = ownership?.IsPlayerVisible ?? true,
            ["isSelectable"] = false,
            ["profileState"] = archived ? CharacterStatusIds.Archived : ApplicationContextStates.ProfileMigrationRequired,
            ["availabilityMessage"] = archived
                ? "Персонаж находится в архиве."
                : "Данные персонажа временно недоступны. Обратитесь к мастеру.",
            ["stats"] = new Dictionary<string, object>(),
            ["groupMembership"] = Array.Empty<object>()
        };
    }

    private Dictionary<string, object> PlayerCharacterHubCard(Character character, UserAccount viewer, CharacterOwnershipState? ownership, bool playerProjection = true)
    {
        var card = BuildStrictCharacterProfileCard(character, viewer);
        EnsurePlayerHubProfileSections(card, character.Id);
        var stats = ClientMap(card.TryGetValue("stats", out var statsRaw) ? statsRaw : null);
        _logger.Debug($"character.profile.hub.card.stats characterId={character.Id} profileSource={ClientString(card, "profileSource")} strength={ClientString(stats, "strength")}");
        var inventory = ClientList(card.TryGetValue("inventory", out var inventoryRaw) ? inventoryRaw : null);
        var description = ClientString(card, "description");
        var backstory = ClientString(card, "backstory");
        var health = ClientString(stats, "health");
        var physicalArmor = ClientString(stats, "physicalArmor");
        var magicalArmor = ClientString(stats, "magicalArmor");

        card["name"] = ClientFirstNonEmpty(ClientString(card, "name"), "Без имени");
        card["race"] = ClientFirstNonEmpty(ClientString(card, "race"), "—");
        card["health"] = ClientFirstNonEmpty(health, "—");
        card["armor"] = ClientFirstNonEmpty(physicalArmor, magicalArmor) == string.Empty ? "—" : $"Физ. {ClientFirstNonEmpty(physicalArmor, "0")} / Маг. {ClientFirstNonEmpty(magicalArmor, "0")}";
        if (!card.ContainsKey("xpCoins"))
            card["xpCoins"] = ClientWalletAmount(character.Id, CharacterCurrencyIds.XpCoin);
        card["summary"] = ClientFirstNonEmpty(TrimPreview(description, 120), TrimPreview(backstory, 120), "Данные персонажа доступны в карточке персонажа.");
        card["inventorySummary"] = $"{inventory.Count} предметов";
        card["knownFacts"] = card.ContainsKey("knownFacts") ? card["knownFacts"] : Array.Empty<object>();
        card["knownLanguages"] = card.ContainsKey("knownLanguages") ? card["knownLanguages"] : Array.Empty<object>();
        card["discoveredLocations"] = card.ContainsKey("discoveredLocations") ? card["discoveredLocations"] : Array.Empty<object>();
        card["activeResearch"] = card.ContainsKey("activeResearch") ? card["activeResearch"] : Array.Empty<object>();
        card["researchedTechnologies"] = card.ContainsKey("researchedTechnologies") ? card["researchedTechnologies"] : Array.Empty<object>();
        card["blueprints"] = card.ContainsKey("blueprints") ? card["blueprints"] : Array.Empty<object>();
        card["craftRecipes"] = card.ContainsKey("craftRecipes") ? card["craftRecipes"] : Array.Empty<object>();
        card["craftMaterials"] = card.ContainsKey("craftMaterials") ? card["craftMaterials"] : Array.Empty<object>();
        card["craftJobs"] = card.ContainsKey("craftJobs") ? card["craftJobs"] : Array.Empty<object>();
        card["publicProfileRevision"] = _mongo.CharacterBodyProfiles
            .Find(x => x.CharacterId == character.Id)
            .FirstOrDefault()?.EntityRevision ?? 0;
        if (ownership != null)
        {
            card["campaignId"] = ownership.CampaignId ?? string.Empty;
            if (!playerProjection)
            {
                card["ownerUserId"] = ownership.OwnerUserId ?? string.Empty;
                card["controlledByUserId"] = ownership.ControlledByUserId ?? string.Empty;
            }
            card["ownerDisplayName"] = ClientFirstNonEmpty(ownership.OwnerDisplayName, "Не указан");
            card["controlledByDisplayName"] = ClientFirstNonEmpty(ownership.ControlledByDisplayName, "Не указан");
            card["characterRole"] = ownership.CharacterRole ?? string.Empty;
            card["characterKind"] = ClientFirstNonEmpty(ownership.CharacterKind, MapOwnershipRoleToCharacterKind(ownership.CharacterRole));
            card["characterKindDisplayName"] = CharacterKindDisplayName(ClientFirstNonEmpty(ownership.CharacterKind, MapOwnershipRoleToCharacterKind(ownership.CharacterRole)));
            card["characterStatus"] = ClientFirstNonEmpty(ownership.CharacterStatus, ownership.IsArchived ? CharacterStatusIds.Archived : ownership.IsActive ? CharacterStatusIds.Active : CharacterStatusIds.Inactive);
            card["characterStatusDisplayName"] = CharacterStatusDisplayName(ClientFirstNonEmpty(ownership.CharacterStatus, ownership.IsArchived ? CharacterStatusIds.Archived : ownership.IsActive ? CharacterStatusIds.Active : CharacterStatusIds.Inactive));
            card["isActive"] = ownership.IsActive;
            card["isArchived"] = ownership.IsArchived;
            card["isPlayerVisible"] = ownership.IsPlayerVisible;
            card["ownershipSummary"] = ClientFirstNonEmpty(ownership.OwnerDisplayName, "Не указан");
        }
        else
        {
            card["ownershipSummary"] = "Не указан";
        }
        card["processSummary"] = "Активные процессы персонажа будут показаны здесь после подключения проектных read models.";
        card["requestSummary"] = "Заявки игрока доступны в разделе заявок.";
        if (playerProjection)
            SanitizePlayerCharacterHubCard(card);
        return card;
    }

    private Dictionary<string, object> BuildStrictCharacterProfileCard(Character character, UserAccount viewer)
    {
        var identityShell = _characterDetailsProfileBuilder.BuildProfileIdentityShell(character);
        var result = _characterDetailsProfileBuilder
            .BuildFromProfilesAsync(character, viewer.Id, string.Empty, identityShell)
            .GetAwaiter()
            .GetResult();

        if (result == null || result.Payload == null || !result.UsedProfileFirst || result.UsedFallback)
        {
            var reason = result?.ErrorMessage ?? "profile_result_missing";
            _logger.Debug($"character.player.hub.profile_required characterId={character.Id} reason={reason}");
            throw new InvalidOperationException("Профиль Character v2 недоступен.");
        }

        ApplyPlayerSafeCharacterPayload(result.Payload, viewer);
        return result.Payload;
    }

    private static void SanitizePlayerCharacterHubCard(Dictionary<string, object> card)
    {
        var forbiddenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gmNotes",
            "gmDescription",
            "serverOnlyData",
            "adminOnlyDetails",
            "adminNotes",
            "audit",
            "rawPayload",
            "ownerUserId",
            "controlledByUserId",
            "groupId",
            "profileRuleSetId",
            "profileMissingSections",
            "profileSource",
            "schemaVersion",
            "raceCode",
            "sourceOfTruth",
            "createdByUserId",
            "updatedByUserId"
        };

        foreach (var key in card.Keys.Where(forbiddenKeys.Contains).ToArray())
            card.Remove(key);

        foreach (var key in card.Keys.ToArray())
            card[key] = SanitizePlayerCharacterHubValue(card[key], forbiddenKeys);
    }

    private static object SanitizePlayerCharacterHubValue(object? value, HashSet<string> forbiddenKeys)
    {
        if (value is IDictionary<string, object> typed)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in typed)
            {
                if (forbiddenKeys.Contains(pair.Key)) continue;
                result[pair.Key] = SanitizePlayerCharacterHubValue(pair.Value, forbiddenKeys);
            }
            return result;
        }

        if (value is System.Collections.IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry pair in dictionary)
            {
                var key = Convert.ToString(pair.Key) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key) || forbiddenKeys.Contains(key)) continue;
                result[key] = SanitizePlayerCharacterHubValue(pair.Value, forbiddenKeys);
            }
            return result;
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var result = new List<object>();
            foreach (var item in enumerable)
            {
                var map = item as IDictionary<string, object>;
                if (map != null)
                {
                    if (map.TryGetValue("isPlayerVisible", out var visible) && !Convert.ToBoolean(visible)) continue;
                    if (map.TryGetValue("isHidden", out var hidden) && Convert.ToBoolean(hidden)) continue;
                    if (map.TryGetValue("gmOnly", out var gmOnly) && Convert.ToBoolean(gmOnly)) continue;
                }
                result.Add(SanitizePlayerCharacterHubValue(item, forbiddenKeys));
            }
            return result.ToArray();
        }

        return value ?? string.Empty;
    }

    private Dictionary<string, object> AdminCharacterHubCard(Character character, UserAccount viewer)
    {
        var card = PlayerCharacterHubCard(character, viewer, null, playerProjection: false);
        card["ownerUserId"] = character.OwnerUserId;
        card["sessionId"] = character.SessionId;
        card["inventoryCount"] = ClientList(card.TryGetValue("inventory", out var inventoryRaw) ? inventoryRaw : null).Count;
        card["companionCount"] = ClientList(card.TryGetValue("companions", out var companionsRaw) ? companionsRaw : null).Count;
        card["visibility"] = new Dictionary<string, object>
        {
            ["hideDescription"] = character.Visibility?.HideDescriptionForOthers ?? false,
            ["hideBackstory"] = character.Visibility?.HideBackstoryForOthers ?? false,
            ["hideStats"] = character.Visibility?.HideStatsForOthers ?? false,
            ["hideReputation"] = character.Visibility?.HideReputationForOthers ?? false
        };
        card["adminNotes"] = "Admin-only notes are intentionally not part of the player projection.";
        return card;
    }

    private static Dictionary<string, object> ClientMap(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        if (value is System.Collections.IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value;
            }
            return result;
        }
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private long ClientWalletAmount(string characterId, string currencyId)
    {
        var profile = _mongo.CharacterWalletProfiles
            .Find(Builders<CharacterWalletProfileDocument>.Filter.Eq(x => x.CharacterId, characterId ?? string.Empty))
            .FirstOrDefault()
            ?.Profile;
        return (profile?.Wallets ?? new List<CharacterWalletValue>())
            .FirstOrDefault(x => x != null && string.Equals(x.CurrencyId, currencyId, StringComparison.OrdinalIgnoreCase))
            ?.Amount ?? 0;
    }

    private void EnsurePlayerHubProfileSections(Dictionary<string, object> card, string characterId)
    {
        card["stats"] = ClientProfileStats(characterId);
        card["money"] = ClientProfileMoney(characterId);
        card["inventory"] = ClientProfileInventory(characterId);
        card["reputation"] = ClientProfileReputation(characterId);
        card["holdings"] = ClientProfileHoldings(characterId);
        card["companions"] = ClientProfileCompanions(characterId);
        card["xpCoins"] = ClientWalletAmount(characterId, CharacterCurrencyIds.XpCoin);
        var body = _mongo.CharacterBodyProfiles
            .Find(Builders<CharacterBodyProfileDocument>.Filter.Eq(x => x.CharacterId, characterId ?? string.Empty))
            .FirstOrDefault()
            ?.Profile;
        card["bodyTypeDisplay"] = ClientReadableBodyValue(body?.BodyType, "Не указан");
        card["sizeCategoryDisplay"] = ClientReadableBodyValue(body?.SizeCategory, "Не указана");
        var knowledge = _mongo.CharacterKnowledgeProfiles
            .Find(Builders<CharacterKnowledgeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId ?? string.Empty))
            .FirstOrDefault()
            ?.Profile;
        card["knownLanguages"] = (knowledge?.Languages ?? new List<string>())
            .Select(ClientReadableLanguage)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray();
    }

    private static string ClientReadableBodyValue(string? value, string fallback)
    {
        var original = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(original)) return fallback;
        if (original.Any(ch => ch >= '\u0400' && ch <= '\u04FF')) return original;
        return original.ToLowerInvariant() switch
        {
            "humanoid" => "Гуманоид",
            "biped" => "Двуногий",
            "quadruped" => "Четвероногий",
            "small" => "Малый",
            "medium" => "Средний",
            "large" => "Крупный",
            "huge" => "Огромный",
            _ => fallback
        };
    }

    private static string ClientReadableLanguage(string? value)
    {
        var original = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(original)) return string.Empty;
        if (original.Any(ch => ch >= '\u0400' && ch <= '\u04FF')) return original;
        return original.ToLowerInvariant() switch
        {
            "common" or "common_language" => "Общий",
            "elvish" => "Эльфийский",
            "dwarvish" => "Дварфийский",
            "orcish" => "Орочий",
            "sign_language" => "Язык жестов",
            _ => string.Empty
        };
    }

    private Dictionary<string, object> ClientProfileStats(string characterId)
    {
        var profile = _mongo.CharacterAttributeProfiles
            .Find(Builders<CharacterAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId ?? string.Empty))
            .FirstOrDefault()
            ?.Profile;
        var values = (profile?.Values ?? new List<CharacterAttributeValue>())
            .Where(x => x != null)
            .GroupBy(x => x.AttributeId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        return new Dictionary<string, object>
        {
            ["health"] = ClientAttributeValue(values, CharacterAttributeIds.Health, CharacterVitalStatIds.HealthCurrent),
            ["currentHealth"] = ClientAttributeValue(values, CharacterAttributeIds.Health, CharacterVitalStatIds.HealthCurrent),
            ["maxHealth"] = ClientAttributeValue(values, CharacterVitalStatIds.HealthMax, CharacterAttributeIds.Health),
            ["physicalArmor"] = ClientAttributeValue(values, CharacterAttributeIds.PhysicalArmor, CharacterVitalStatIds.PhysicalDefense),
            ["physicalDefense"] = ClientAttributeValue(values, CharacterAttributeIds.PhysicalArmor, CharacterVitalStatIds.PhysicalDefense),
            ["magicalArmor"] = ClientAttributeValue(values, CharacterAttributeIds.MagicArmor, CharacterVitalStatIds.MagicalDefense),
            ["magicalDefense"] = ClientAttributeValue(values, CharacterAttributeIds.MagicArmor, CharacterVitalStatIds.MagicalDefense),
            ["morale"] = ClientAttributeValue(values, CharacterVitalStatIds.Morale, CharacterAttributeIds.Morale)
        };
    }

    private static int ClientAttributeValue(Dictionary<string, CharacterAttributeValue> values, params string[] ids)
    {
        foreach (var id in ids)
        {
            if (values.TryGetValue(id, out var value))
                return value.CurrentValue != 0 ? value.CurrentValue : value.BaseValue;
        }
        return 0;
    }

    private Dictionary<string, object> ClientProfileMoney(string characterId)
    {
        var profile = _mongo.CharacterWalletProfiles
            .Find(Builders<CharacterWalletProfileDocument>.Filter.Eq(x => x.CharacterId, characterId ?? string.Empty))
            .FirstOrDefault()
            ?.Profile;
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in profile?.Wallets ?? new List<CharacterWalletValue>())
        {
            if (value == null || string.IsNullOrWhiteSpace(value.CurrencyId))
                continue;
            result[value.CurrencyId] = value.Amount;
            if (string.Equals(value.CurrencyId, CharacterCurrencyIds.GoldCoin, StringComparison.OrdinalIgnoreCase)) result["Gold"] = value.Amount;
            if (string.Equals(value.CurrencyId, CharacterCurrencyIds.SilverCoin, StringComparison.OrdinalIgnoreCase)) result["Silver"] = value.Amount;
            if (string.Equals(value.CurrencyId, CharacterCurrencyIds.BronzeCoin, StringComparison.OrdinalIgnoreCase)) result["Bronze"] = value.Amount;
            if (string.Equals(value.CurrencyId, CharacterCurrencyIds.IronCoin, StringComparison.OrdinalIgnoreCase)) result["Iron"] = value.Amount;
            if (string.Equals(value.CurrencyId, CharacterCurrencyIds.XpCoin, StringComparison.OrdinalIgnoreCase)) result["XpCoins"] = value.Amount;
        }
        if (!result.ContainsKey("Gold")) result["Gold"] = 0L;
        if (!result.ContainsKey("Silver")) result["Silver"] = 0L;
        return result;
    }

    private object[] ClientProfileInventory(string characterId)
        => (_mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId ?? string.Empty)).FirstOrDefault()?.Profile?.Items ?? new List<CharacterInventoryItemProfileValue>())
            .Where(x => x != null && x.IsPlayerVisible)
            .OrderBy(x => x.SortOrder)
            .Select(x => new Dictionary<string, object>
            {
                ["id"] = x.ItemId,
                ["name"] = ClientFirstNonEmpty(x.DisplayName, x.Name, x.SnapshotDisplayName, x.DefinitionCode, "Предмет"),
                ["label"] = ClientFirstNonEmpty(x.DisplayName, x.Name, x.SnapshotDisplayName, "Предмет"),
                ["category"] = ClientFirstNonEmpty(x.Category, x.SnapshotCategory, "—"),
                ["quantity"] = x.Quantity,
                ["durability"] = x.Durability,
                ["isEquipped"] = x.IsEquipped,
                ["description"] = ClientFirstNonEmpty(x.Description, x.SnapshotDescription, string.Empty)
            })
            .Cast<object>()
            .ToArray();

    private object[] ClientProfileReputation(string characterId)
        => (_mongo.CharacterReputationProfiles.Find(Builders<CharacterReputationProfileDocument>.Filter.Eq(x => x.CharacterId, characterId ?? string.Empty)).FirstOrDefault()?.Profile?.Entries ?? new List<CharacterReputationProfileValue>())
            .Where(x => x != null && x.IsPlayerVisible && !x.IsArchived)
            .Select(x => new Dictionary<string, object>
            {
                ["id"] = x.EntryId,
                ["scopeType"] = x.ScopeType,
                ["targetType"] = x.TargetType,
                ["targetName"] = ClientFirstNonEmpty(x.Name, "Репутация"),
                ["value"] = x.Value,
                ["isArchived"] = x.IsArchived
            })
            .Cast<object>()
            .ToArray();

    private object[] ClientProfileHoldings(string characterId)
        => (_mongo.CharacterHoldingsProfiles.Find(Builders<CharacterHoldingsProfileDocument>.Filter.Eq(x => x.CharacterId, characterId ?? string.Empty)).FirstOrDefault()?.Profile?.Holdings ?? new List<CharacterHoldingProfileValue>())
            .Where(x => x != null && x.IsPlayerVisible && !x.IsArchived)
            .Select(x => new Dictionary<string, object>
            {
                ["id"] = x.HoldingId,
                ["name"] = ClientFirstNonEmpty(x.Name, "Владение"),
                ["type"] = ClientFirstNonEmpty(x.HoldingType, "—"),
                ["description"] = ClientFirstNonEmpty(x.Description, string.Empty),
                ["ownerDisplayName"] = ClientFirstNonEmpty(x.OwnerDisplayName, string.Empty),
                ["isArchived"] = x.IsArchived
            })
            .Cast<object>()
            .ToArray();

    private object[] ClientProfileCompanions(string characterId)
        => (_mongo.CharacterCompanionProfiles.Find(Builders<CharacterCompanionProfileDocument>.Filter.Eq(x => x.CharacterId, characterId ?? string.Empty)).FirstOrDefault()?.Profile?.Companions ?? new List<CharacterCompanionProfileValue>())
            .Where(x => x != null && x.IsPlayerVisible && !x.IsArchived)
            .Select(x => new Dictionary<string, object>
            {
                ["id"] = x.CompanionId,
                ["name"] = ClientFirstNonEmpty(x.Name, "Компаньон"),
                ["type"] = ClientFirstNonEmpty(x.CompanionType, "—"),
                ["species"] = "Не указана",
                ["description"] = ClientFirstNonEmpty(x.Description, string.Empty),
                ["inventory"] = Array.Empty<object>(),
                ["holdings"] = Array.Empty<object>(),
                ["reputation"] = Array.Empty<object>(),
                ["knownFacts"] = Array.Empty<object>(),
                ["activeResearch"] = Array.Empty<object>(),
                ["craftJobs"] = Array.Empty<object>(),
                ["isArchived"] = x.IsArchived
            })
            .Cast<object>()
            .ToArray();

    private static System.Collections.IList ClientList(object? value)
    {
        return value as System.Collections.IList ?? Array.Empty<object>();
    }

    private static string ClientString(Dictionary<string, object> map, string key)
    {
        return map != null && map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    }

    private static Dictionary<string, object> BuildCharacterHubStats(CharacterStats stats)
        => new()
        {
            ["health"] = stats.Health,
            ["physicalArmor"] = stats.PhysicalArmor,
            ["magicalArmor"] = stats.MagicalArmor,
            ["morale"] = stats.Morale,
            ["strength"] = stats.Strength,
            ["dexterity"] = stats.Dexterity,
            ["endurance"] = stats.Endurance,
            ["wisdom"] = stats.Wisdom,
            ["intellect"] = stats.Intellect,
            ["charisma"] = stats.Charisma
        };

    private static object[] BuildCharacterHubInventory(IEnumerable<InventoryItem>? items)
        => (items ?? Enumerable.Empty<InventoryItem>())
            .Where(x => x != null && !x.Archived && !x.Deleted)
            .Select(x => new Dictionary<string, object>
            {
                ["id"] = x.Id,
                ["name"] = ClientFirstNonEmpty(x.Name, x.Label, x.ItemCode, "Предмет"),
                ["label"] = ClientFirstNonEmpty(x.Label, x.Name, x.ItemCode, "Предмет"),
                ["category"] = ClientFirstNonEmpty(x.Category, "—"),
                ["quantity"] = x.Quantity,
                ["durability"] = x.Durability ?? x.DurabilityOrHealth ?? 0,
                ["isEquipped"] = x.IsEquipped || x.Equipped,
                ["description"] = ClientFirstNonEmpty(x.Description, x.Notes, string.Empty)
            })
            .Cast<object>()
            .ToArray();

    private static object[] BuildCharacterHubHoldings(IEnumerable<HoldingRef>? holdings)
        => (holdings ?? Enumerable.Empty<HoldingRef>())
            .Where(x => x != null && !x.Archived)
            .Select(x => new Dictionary<string, object>
            {
                ["id"] = x.Id,
                ["name"] = ClientFirstNonEmpty(x.Name, "Владение"),
                ["type"] = ClientFirstNonEmpty(x.Type, "—"),
                ["description"] = ClientFirstNonEmpty(x.Description, x.Notes, string.Empty),
                ["notes"] = x.Notes,
                ["owners"] = (x.Owners ?? new List<string>()).Cast<object>().ToArray(),
                ["isArchived"] = x.Archived
            })
            .Cast<object>()
            .ToArray();

    private static object[] BuildCharacterHubReputation(IEnumerable<ReputationRef>? reputation, bool playerSafe)
        => (reputation ?? Enumerable.Empty<ReputationRef>())
            .Where(x => x != null && !x.Archived && (!playerSafe || !x.IsHiddenForOthers))
            .Select(x => new Dictionary<string, object>
            {
                ["id"] = x.Id,
                ["scopeType"] = x.ScopeType.ToString(),
                ["targetType"] = x.TargetType.ToString(),
                ["targetName"] = ClientFirstNonEmpty(x.TargetName, x.GroupKey, "Репутация"),
                ["value"] = x.Value,
                ["notes"] = x.Notes,
                ["isArchived"] = x.Archived
            })
            .Cast<object>()
            .ToArray();

    private static object[] BuildCharacterHubCompanions(IEnumerable<Companion>? companions)
        => (companions ?? Enumerable.Empty<Companion>())
            .Where(x => x != null && !x.IsArchived)
            .Select(x => new Dictionary<string, object>
            {
                ["id"] = x.Id,
                ["name"] = ClientFirstNonEmpty(x.Name, "Компаньон"),
                ["type"] = ClientFirstNonEmpty(x.Type, "—"),
                ["species"] = ClientFirstNonEmpty(x.Species, "—"),
                ["description"] = ClientFirstNonEmpty(x.Description, x.Notes, string.Empty),
                ["notes"] = x.Notes,
                ["statsSummary"] = x.StatsSummary,
                ["inventory"] = BuildCharacterHubInventory(x.Inventory),
                ["holdings"] = BuildCharacterHubHoldings(x.Holdings),
                ["reputation"] = BuildCharacterHubReputation(x.Reputation, playerSafe: true),
                ["knownFacts"] = Array.Empty<object>(),
                ["activeResearch"] = Array.Empty<object>(),
                ["craftJobs"] = Array.Empty<object>(),
                ["isArchived"] = x.IsArchived
            })
            .Cast<object>()
            .ToArray();

    private static Dictionary<string, object> Metric(string label, int value, string hint)
        => new() { ["label"] = label, ["value"] = value, ["hint"] = hint };

    private static Dictionary<string, object> ProcessCard(string type, string title, string status, string progress, string summary, string entityId, string target)
        => new()
        {
            ["type"] = type,
            ["title"] = ClientFirstNonEmpty(title, "Без названия"),
            ["status"] = ClientFirstNonEmpty(status, "—"),
            ["progress"] = ClientFirstNonEmpty(progress, "—"),
            ["summary"] = ClientFirstNonEmpty(TrimPreview(summary, 160), "Описание пока не заполнено."),
            ["entityId"] = entityId,
            ["target"] = target
        };

    private static Dictionary<string, object> ActionCard(string title, string subject, string priority, string actionLabel, string target, string entityId)
        => new()
        {
            ["title"] = title,
            ["subject"] = ClientFirstNonEmpty(subject, "Без названия"),
            ["priority"] = ClientFirstNonEmpty(priority, "обычно"),
            ["actionLabel"] = actionLabel,
            ["target"] = target,
            ["entityId"] = entityId
        };

    private FilterDefinition<PlayerRequestState> PlayerRequestOwnerFilter(UserAccount actor)
        => Builders<PlayerRequestState>.Filter.Eq(x => x.CreatedByUserId, actor.Id);

    private FilterDefinition<PlayerProposalDraftState> PlayerProposalOwnerFilter(UserAccount actor)
        => Builders<PlayerProposalDraftState>.Filter.Eq(x => x.CreatedByUserId, actor.Id);

    private FilterDefinition<ProjectBaseState> PlayerProjectFilter(UserAccount actor)
        => Builders<ProjectBaseState>.Filter.Eq(x => x.OwnerUserId, actor.Id);

    private FilterDefinition<CraftingProjectState> PlayerCraftingFilter(UserAccount actor)
        => Builders<CraftingProjectState>.Filter.Eq(x => x.OwnerUserId, actor.Id);

    private FilterDefinition<EngineeringDesignProjectState> PlayerEngineeringFilter(UserAccount actor)
        => Builders<EngineeringDesignProjectState>.Filter.Eq(x => x.OwnerUserId, actor.Id);

    private FilterDefinition<FactoryOrderState> PlayerFactoryOrderFilter(UserAccount actor)
        => Builders<FactoryOrderState>.Filter.Eq(x => x.OwnerUserId, actor.Id);

    private FilterDefinition<ManufacturingProjectState> PlayerManufacturingFilter(UserAccount actor)
        => Builders<ManufacturingProjectState>.Filter.Or(
            Builders<ManufacturingProjectState>.Filter.Eq(x => x.CreatedByUserId, actor.Id),
            Builders<ManufacturingProjectState>.Filter.Eq(x => x.OwnerEntityId, actor.Id),
            Builders<ManufacturingProjectState>.Filter.Eq(x => x.CustomerEntityId, actor.Id));

    private int Count<T>(IRepository<T> repository, FilterDefinition<T> filter) where T : EntityBase
        => repository.Find(filter).Count;

    private int CountProposalDrafts(FilterDefinition<PlayerProposalDraftState> filter)
        => (int)_mongo.PlayerProposalDrafts.CountDocuments(filter);

    private bool ClientFlag(string flagName) => _featureFlags.IsEnabled(flagName);

    private static string ClientFirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string ClientProposalSummary(PlayerProposalDraftState proposal)
        => ClientFirstNonEmpty(TrimPreview(proposal.PublicSummary, 160), TrimPreview(proposal.Description, 160), proposal.ValidationSummary);

    private static string ClientPriorityLabel(string priority)
    {
        var value = priority?.Trim().ToLowerInvariant();
        return value switch
        {
            "low" => "низкий",
            "normal" => "обычный",
            "high" => "высокий",
            "urgent" => "срочно",
            _ => ClientFirstNonEmpty(priority, "обычный")
        };
    }

    private static string ClientProposalTypeLabel(string type)
    {
        var value = type?.Trim().ToLowerInvariant();
        return value switch
        {
            "research" => "Исследование",
            "crafting" => "Крафт",
            "engineering_design" => "Инженерия",
            "factory_quote" => "Расчёт производства",
            "factory_order" => "Заказ производства",
            "manufacturing" => "Производство",
            "legal_check" => "Правовая проверка",
            "license_application" => "Лицензия",
            "development_purchase" => "Развитие",
            "inventory_action" => "Инвентарь",
            "asset_transfer" => "Активы",
            "custom_project" => "Проект",
            "generic_gm_request" => "Заявка GM",
            "custom" => "Другое",
            _ => ClientFirstNonEmpty(type, "Другое")
        };
    }

    private static string ClientStatusLabel(string status)
    {
        var value = status?.Trim().ToLowerInvariant();
        return value switch
        {
            "draft" => "Черновик",
            "ready_to_submit" => "Готово к отправке",
            "submitted" => "Отправлено GM",
            "linked_to_request" => "Связано с заявкой",
            "in_gm_review" or "in_review" or "pending_gm_review" => "На проверке GM",
            "changes_requested" => "Нужны правки",
            "approved" => "Одобрено",
            "rejected" => "Отклонено",
            "converted" => "Преобразовано",
            "cancelled" => "Отменено",
            "archived" => "Архив",
            "preparation" => "Подготовка",
            "waiting_resources" => "Нужны ресурсы",
            "active" => "В работе",
            "paused" => "Пауза",
            "blocked" => "Заблокировано",
            "testing" => "Проверка",
            "awaiting_acceptance" => "Ожидает приёмки",
            "ready_for_review" => "Готово к проверке",
            "accepted" => "Принято",
            "accepted_with_defects" => "Принято с дефектами",
            "waived_by_gm" => "Принято GM",
            "completed" => "Завершено",
            "fulfilled" => "Выполнено",
            "failed" => "Провалено",
            "scheduled" => "Запланировано",
            "waiting_manufacturing" => "Ожидает производства",
            "not_ready" => "Не готово",
            _ => ClientFirstNonEmpty(status, "—")
        };
    }

    private static string TrimPreview(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value!.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, Math.Max(0, maxLength - 1)) + "…";
    }
}

