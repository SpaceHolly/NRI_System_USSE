using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private static readonly string[] RaceDefinitionCategories0182 =
    {
        "race_definition",
        "subspecies_definition",
        "hybrid_definition",
        "hybrid_subtype_definition",
        "race_trait_definition",
        "body_zone_definition",
        "racial_sense_definition",
        "racial_movement_ability_definition",
        "natural_attack_definition",
        "elemental_resistance_definition",
        "environmental_tolerance_modifier_definition",
        "title_definition",
        "race_equipment_fit_profile",
        "race_npc_reaction_rule",
        "race_language_grant",
        "race_knowledge_grant"
    };

    private static readonly string[] AttributeDefinitionCategories0182 =
    {
        "attribute_definition",
        "subattribute_definition",
        "derived_stat_definition",
        "attribute_set_profile",
        "derived_stat_set_profile"
    };

    private static readonly string[] SkillDefinitionCategories0182 =
    {
        "skill_definition",
        "skill_group_definition",
        "skill_roll_context_template",
        "skill_technique_definition"
    };

    private static readonly string[] ResolutionDefinitionCategories0219 =
    {
        "resolution_profile",
        "ability_modifier_profile",
        "skill_mastery_profile",
        "modifier_category_profile",
        "advantage_policy",
        "difficulty_profile",
        "degree_of_success_profile",
        "attempt_gate_profile",
        "hit_resolution_profile",
        "penetration_damage_profile"
    };

    private static readonly string[] DevelopmentDefinitionCategories0182 =
    {
        "development_node_definition",
        "development_requirement_definition",
        "development_reward_definition",
        "development_direction_definition",
        "development_hexagon_profile"
    };

    private static List<DefinitionEditorProfile> BuildCharacterDefinitionEditorProfiles0182()
    {
        var profiles = new List<DefinitionEditorProfile>
        {
            Profile0181("race_definition", "race_definition", "Расы", "Playable and GM-facing race definitions.", new[]
            {
                Field0181("availabilityType", "Доступность", ContentDefinitionFieldTypes.Enum, true, new[] { "Playable", "PlayableWithCampaignPermission", "GMOnly", "NPCOnly", "MonsterOnly", "Hidden", "WildOnly", "Archived" }),
                Field0181("gameplayDifficulty", "Сложность", ContentDefinitionFieldTypes.Enum, true, new[] { "Normal", "Medium", "Hard", "Extreme" }),
                Field0181("raceLanguageId", "Язык расы", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "language"),
                Field0181("minHeightCm", "Мин. рост, см", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("maxHeightCm", "Макс. рост, см", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("minAgeYears", "Мин. возраст", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 100000),
                Field0181("maxAgeYears", "Макс. возраст", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("adultAgeYears", "Возраст взросления", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100000),
                Field0181("averageLifespanYears", "Средняя продолжительность жизни", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 100000),
                Field0181("maximumLifespanYears", "Предельная продолжительность жизни", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 100000),
                Field0181("baseHealth", "Базовое здоровье", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 100000),
                Field0181("naturalArmorRating", "Естественная броня", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 1000),
                Field0181("naturalPenetrationResistance", "Естественная стойкость к пробитию", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 1000),
                Field0181("bodyZoneIds", "Зоны тела", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: "body_zone_definition"),
                Field0181("equipmentFitProfileId", "Посадка экипировки", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "race_equipment_fit_profile"),
                Field0181("racialSenseIds", "Особые чувства", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "racial_sense_definition"),
                Field0181("movementAbilityIds", "Особые способы движения", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "racial_movement_ability_definition"),
                Field0181("naturalAttackIds", "Естественные атаки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "natural_attack_definition"),
                Field0181("elementalResistanceIds", "Стихийная устойчивость", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "elemental_resistance_definition"),
                Field0181("environmentalToleranceModifierIds", "Адаптация к среде", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "environmental_tolerance_modifier_definition"),
                Field0181("typicalMinHeightCm", "Типичный мин. рост", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("typicalMaxHeightCm", "Типичный макс. рост", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("ageProfile", "Возрастной профиль", ContentDefinitionFieldTypes.LongText, false),
                Field0181("traitIds", "Расовые свойства", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "race_trait_definition"),
                Field0181("defaultModifiers", "Модификаторы", ContentDefinitionFieldTypes.LongText, false),
                Field0181("attributeBonuses", "Бонусы характеристик", ContentDefinitionFieldTypes.LongText, false),
                Field0181("subAttributeBonuses", "Бонусы подхарактеристик", ContentDefinitionFieldTypes.LongText, false),
                Field0181("strongSides", "Сильные стороны", ContentDefinitionFieldTypes.Tags, false),
                Field0181("weakSides", "Слабые стороны", ContentDefinitionFieldTypes.Tags, false),
                Field0181("publicTraits", "Публичные свойства", ContentDefinitionFieldTypes.Tags, false),
                Field0181("gmOnlyTraits", "Скрытые свойства GM", ContentDefinitionFieldTypes.Tags, false, isPlayerVisible: false, isGmOnly: true),
                Field0181("startingLanguages", "Стартовые языки", ContentDefinitionFieldTypes.Tags, false),
                Field0181("knowledgeGrants", "Стартовые знания", ContentDefinitionFieldTypes.Tags, false),
                Field0181("developmentCostRules", "Стоимость развития", ContentDefinitionFieldTypes.LongText, false),
                Field0181("equipmentFitTags", "Совместимость экипировки", ContentDefinitionFieldTypes.Tags, false),
                Field0181("fullPlayerDescription", "Полное описание для игрока", ContentDefinitionFieldTypes.LongText, true),
                Field0181("gmRules", "GM-правила", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            }),
            Profile0181("subspecies_definition", "subspecies_definition", "Подвиды", "Subspecies definitions linked to a race.", new[]
            {
                Field0181("raceId", "Раса", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "race_definition"),
                Field0181("subspeciesKinds", "Типы подвида", ContentDefinitionFieldTypes.Tags, true),
                Field0181("availabilityType", "Доступность", ContentDefinitionFieldTypes.Enum, true, new[] { "Playable", "PlayableWithCampaignPermission", "GMOnly", "NPCOnly", "MonsterOnly", "Hidden", "WildOnly", "Archived" }),
                Field0181("minHeightCm", "Мин. рост, см", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("maxHeightCm", "Макс. рост, см", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("minAgeYears", "Мин. возраст", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 100000),
                Field0181("maxAgeYears", "Макс. возраст", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("adultAgeYears", "Возраст взросления", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 100000),
                Field0181("averageLifespanYears", "Средняя продолжительность жизни", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("maximumLifespanYears", "Предельная продолжительность жизни", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("baseHealth", "Базовое здоровье", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("naturalArmorRating", "Естественная броня", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("naturalPenetrationResistance", "Естественная стойкость к пробитию", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("bodyZoneIds", "Переопределение зон тела", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "body_zone_definition"),
                Field0181("equipmentFitProfileId", "Посадка экипировки", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "race_equipment_fit_profile"),
                Field0181("racialSenseIds", "Особые чувства", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "racial_sense_definition"),
                Field0181("movementAbilityIds", "Особые способы движения", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "racial_movement_ability_definition"),
                Field0181("naturalAttackIds", "Естественные атаки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "natural_attack_definition"),
                Field0181("elementalResistanceIds", "Стихийная устойчивость", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "elemental_resistance_definition"),
                Field0181("environmentalToleranceModifierIds", "Адаптация к среде", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "environmental_tolerance_modifier_definition"),
                Field0181("attributeBonuses", "Бонусы характеристик", ContentDefinitionFieldTypes.LongText, false),
                Field0181("subAttributeBonuses", "Бонусы подхарактеристик", ContentDefinitionFieldTypes.LongText, false),
                Field0181("traitIds", "Свойства", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "race_trait_definition"),
                Field0181("languageGrants", "Языки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "language"),
                Field0181("knowledgeGrants", "Знания", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "knowledge"),
                Field0181("npcReactionRules", "Реакции NPC", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "race_npc_reaction_rule")
            }),
            Profile0181("hybrid_definition", "hybrid_definition", "Гибриды", "Explicit hybrid lineage definitions.", new[]
            {
                Field0181("availabilityType", "Доступность", ContentDefinitionFieldTypes.Enum, true, new[] { "Playable", "PlayableWithCampaignPermission", "GMOnly", "NPCOnly", "MonsterOnly", "Hidden", "WildOnly", "Archived" }),
                Field0181("gameplayDifficulty", "Сложность", ContentDefinitionFieldTypes.Enum, true, new[] { "Normal", "Medium", "Hard", "Extreme" }),
                Field0181("parentLineages", "Родительские линии", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: "race_definition"),
                Field0181("parentOrderMatters", "Порядок родителей важен", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("minHeightCm", "Мин. рост, см", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("maxHeightCm", "Макс. рост, см", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("minAgeYears", "Мин. возраст", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 100000),
                Field0181("maxAgeYears", "Макс. возраст", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("adultAgeYears", "Возраст взросления", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100000),
                Field0181("averageLifespanYears", "Средняя продолжительность жизни", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 100000),
                Field0181("maximumLifespanYears", "Предельная продолжительность жизни", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 100000),
                Field0181("baseHealth", "Базовое здоровье", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 100000),
                Field0181("naturalArmorRating", "Естественная броня", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 1000),
                Field0181("naturalPenetrationResistance", "Естественная стойкость к пробитию", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 1000),
                Field0181("bodyZoneIds", "Зоны тела", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: "body_zone_definition"),
                Field0181("equipmentFitProfileId", "Посадка экипировки", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "race_equipment_fit_profile"),
                Field0181("racialSenseIds", "Особые чувства", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "racial_sense_definition"),
                Field0181("movementAbilityIds", "Особые способы движения", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "racial_movement_ability_definition"),
                Field0181("naturalAttackIds", "Естественные атаки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "natural_attack_definition"),
                Field0181("elementalResistanceIds", "Стихийная устойчивость", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "elemental_resistance_definition"),
                Field0181("environmentalToleranceModifierIds", "Адаптация к среде", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "environmental_tolerance_modifier_definition"),
                Field0181("attributeBonuses", "Бонусы характеристик", ContentDefinitionFieldTypes.LongText, false),
                Field0181("subAttributeBonuses", "Бонусы подхарактеристик", ContentDefinitionFieldTypes.LongText, false),
                Field0181("strongSides", "Сильные стороны", ContentDefinitionFieldTypes.Tags, false),
                Field0181("weakSides", "Слабые стороны", ContentDefinitionFieldTypes.Tags, false),
                Field0181("publicTraits", "Публичные свойства", ContentDefinitionFieldTypes.Tags, false),
                Field0181("equipmentFitTags", "Совместимость экипировки", ContentDefinitionFieldTypes.Tags, false),
                Field0181("knowledgeGrants", "Стартовые знания", ContentDefinitionFieldTypes.Tags, false),
                Field0181("traitIds", "Свойства", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "race_trait_definition"),
                Field0181("startingLanguages", "Стартовые языки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "language"),
                Field0181("languageRules", "Правила языков", ContentDefinitionFieldTypes.LongText, true),
                Field0181("hybridLanguageId", "Язык гибрида", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "language"),
                Field0181("inheritanceNotes", "Наследование", ContentDefinitionFieldTypes.LongText, false),
                Field0181("compatibilityRules", "Совместимость", ContentDefinitionFieldTypes.LongText, false),
                Field0181("canHaveChildren", "Может иметь детей", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("requiresMagic", "Требует магии", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("requiresRitual", "Требует ритуала", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("isSterileByDefault", "Стерилен по умолчанию", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("fullPlayerDescription", "Полное описание для игрока", ContentDefinitionFieldTypes.LongText, true)
            }),
            Profile0181("hybrid_subtype_definition", "hybrid_subtype_definition", "Подвиды гибридов", "Hybrid subtype definitions.", new[]
            {
                Field0181("hybridId", "Гибрид", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "hybrid_definition"),
                Field0181("minHeightCm", "Мин. рост, см", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("maxHeightCm", "Макс. рост, см", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("minAgeYears", "Мин. возраст", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 100000),
                Field0181("maxAgeYears", "Макс. возраст", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("adultAgeYears", "Возраст взросления", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 100000),
                Field0181("averageLifespanYears", "Средняя продолжительность жизни", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("maximumLifespanYears", "Предельная продолжительность жизни", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("baseHealth", "Базовое здоровье", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 100000),
                Field0181("naturalArmorRating", "Естественная броня", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("naturalPenetrationResistance", "Естественная стойкость к пробитию", ContentDefinitionFieldTypes.Integer, false, min: 1, max: 1000),
                Field0181("parent1SubtypeId", "Подвид первой родительской линии", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "subspecies_definition"),
                Field0181("parent2SubtypeId", "Подвид второй родительской линии", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "subspecies_definition"),
                Field0181("elementalLineageId", "Стихийная линия", ContentDefinitionFieldTypes.String, false),
                Field0181("inheritedAspectId", "Наследуемый аспект", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "race_trait_definition"),
                Field0181("flightInheritancePermissionId", "Разрешение наследования полёта", ContentDefinitionFieldTypes.String, false),
                Field0181("bodyZoneIds", "Переопределение зон тела", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "body_zone_definition"),
                Field0181("equipmentFitProfileId", "Посадка экипировки", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "race_equipment_fit_profile"),
                Field0181("racialSenseIds", "Особые чувства", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "racial_sense_definition"),
                Field0181("movementAbilityIds", "Особые способы движения", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "racial_movement_ability_definition"),
                Field0181("naturalAttackIds", "Естественные атаки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "natural_attack_definition"),
                Field0181("elementalResistanceIds", "Стихийная устойчивость", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "elemental_resistance_definition"),
                Field0181("environmentalToleranceModifierIds", "Адаптация к среде", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "environmental_tolerance_modifier_definition"),
                Field0181("attributeBonuses", "Бонусы характеристик", ContentDefinitionFieldTypes.LongText, false),
                Field0181("subAttributeBonuses", "Бонусы подхарактеристик", ContentDefinitionFieldTypes.LongText, false),
                Field0181("lineageNotes", "Линия", ContentDefinitionFieldTypes.LongText, false),
                Field0181("traitOverrides", "Переопределение свойств", ContentDefinitionFieldTypes.LongText, false),
                Field0181("languageOverrides", "Переопределение языков", ContentDefinitionFieldTypes.LongText, false),
                Field0181("availabilityType", "Доступность", ContentDefinitionFieldTypes.Enum, true, new[] { "Playable", "PlayableWithCampaignPermission", "GMOnly", "NPCOnly", "MonsterOnly", "Hidden", "WildOnly", "Archived" }),
                Field0181("gameplayDifficulty", "Сложность", ContentDefinitionFieldTypes.Enum, true, new[] { "Normal", "Medium", "Hard", "Extreme" })
            }),
            Profile0181("race_trait_definition", "race_trait_definition", "Расовые свойства", "Race trait definitions.", new[]
            {
                Field0181("traitType", "Тип свойства", ContentDefinitionFieldTypes.Enum, true, new[] { "AttributeModifier", "Vision", "Movement", "Resistance", "Vulnerability", "Nutrition", "Regeneration", "MagicNature", "EquipmentRule", "SocialTrait", "DevelopmentCostRule", "KnowledgeGrant", "LanguageGrant", "FateInteraction", "SpecialMechanic", "Custom" }),
                Field0181("isPassive", "Пассивное", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("isActivated", "Активируемое", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("isVisibleToPlayer", "Видно игроку", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("rules", "Правила", ContentDefinitionFieldTypes.LongText, false),
                Field0181("gmTraitNotes", "GM-заметки свойства", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            }),
            Profile0181("body_zone_definition", "body_zone_definition", "Зоны тела", "Зоны тела и правила прицельного удара.", new[]
            {
                Field0181("randomWeight", "Вес случайного попадания", ContentDefinitionFieldTypes.Decimal, true, min: 0, max: 1),
                Field0181("calledShotAccuracyModifier", "Модификатор прицельного удара", ContentDefinitionFieldTypes.Integer, true, min: -20, max: 20),
                Field0181("naturalPenetrationResistanceModifier", "Поправка естественной стойкости", ContentDefinitionFieldTypes.Integer, false, min: -20, max: 20),
                Field0181("capabilityTags", "Возможности части тела", ContentDefinitionFieldTypes.Tags, false)
            }),
            Profile0181("racial_sense_definition", "racial_sense_definition", "Особые чувства", "Типизированные чувства без безусловного бонуса Восприятия.", new[]
            {
                Field0181("modality", "Модальность", ContentDefinitionFieldTypes.Enum, true, new[] { "visual_low_light", "visual_dark", "visual_long_range", "hearing_directional", "smell", "thermal", "vibration", "magic_presence", "rune_structure" }),
                Field0181("passiveRangeMeters", "Пассивная дальность, м", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100000),
                Field0181("focusedRangeMeters", "Дальность при сосредоточении, м", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100000),
                Field0181("rangeMultiplier", "Множитель дальности", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("requiresConnectedSurface", "Требует связанной поверхности", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("blockedBySealedBarrier", "Блокируется герметичной преградой", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("penetratesWalls", "Проникает сквозь стены", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("worksInAbsoluteDarkness", "Работает в абсолютной тьме", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("publicLimitations", "Ограничения", ContentDefinitionFieldTypes.LongText, true)
            }),
            Profile0181("racial_movement_ability_definition", "racial_movement_ability_definition", "Особое движение", "Полёт, планирование и другие способы движения.", new[]
            {
                Field0181("movementMode", "Режим движения", ContentDefinitionFieldTypes.Enum, true, new[] { "powered_flight", "glide" }),
                Field0181("actionCostHalfActions", "Стоимость, полу-действий", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10),
                Field0181("speedMeters", "Скорость, м", ContentDefinitionFieldTypes.Decimal, true, min: 0, max: 10000),
                Field0181("maximumLoadFraction", "Предельная доля нагрузки", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 1),
                Field0181("reducedSpeedLoadFraction", "Порог сниженной скорости", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 1),
                Field0181("reducedSpeedMultiplier", "Множитель сниженной скорости", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 1),
                Field0181("requiredClearanceMeters", "Требуемое свободное пространство, м", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("maximumIndependentTakeoffWindMetersPerSecond", "Предельный ветер для взлёта, м/с", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("glideRatio", "Качество планирования", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("canHover", "Может зависать", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("requiredBodyZoneIds", "Необходимые части тела", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: "body_zone_definition"),
                Field0181("requiredEquipmentFitTags", "Требования к экипировке", ContentDefinitionFieldTypes.Tags, false)
            }),
            Profile0181("natural_attack_definition", "natural_attack_definition", "Естественные атаки", "Естественные атаки через общий боевой расчёт.", new[]
            {
                Field0181("attackType", "Тип атаки", ContentDefinitionFieldTypes.String, true),
                Field0181("actionCostHalfActions", "Стоимость, полу-действий", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10),
                Field0181("accuracyModifier", "Точность", ContentDefinitionFieldTypes.Integer, true, min: -100, max: 100),
                Field0181("rangeMeters", "Дальность, м", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100000),
                Field0181("diceCount", "Количество костей", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 1000),
                Field0181("dieSides", "Граней у кости", ContentDefinitionFieldTypes.Integer, true, min: 2, max: 1000000),
                Field0181("perDieModifier", "Модификатор каждой кости", ContentDefinitionFieldTypes.Integer, true, min: -1000000, max: 1000000),
                Field0181("totalModifier", "Общий модификатор", ContentDefinitionFieldTypes.Integer, true, min: -1000000, max: 1000000),
                Field0181("damageTypeIds", "Типы урона", ContentDefinitionFieldTypes.Tags, true),
                Field0181("physicalPenetration", "Пробитие", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100000),
                Field0181("failedPenetrationDamageTransfer", "Доля урона без пробития", ContentDefinitionFieldTypes.Decimal, true, min: 0, max: 1),
                Field0181("areaShape", "Форма области", ContentDefinitionFieldTypes.Enum, true, new[] { "single", "cone", "line" }),
                Field0181("areaAngleDegrees", "Угол области", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 360),
                Field0181("areaWidthMeters", "Ширина области, м", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 10000),
                Field0181("cooldownRounds", "Перезарядка, раундов", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 1000),
                Field0181("friendlyFire", "Затрагивает союзников", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("fateEligibleForHitCheck", "Судьба влияет на проверку попадания", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("requiredBodyZoneIds", "Необходимые части тела", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "body_zone_definition"),
                Field0181("appliedConditionId", "Накладываемое состояние", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "condition_definition"),
                Field0181("appliedConditionRounds", "Длительность состояния", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 1000)
            }),
            Profile0181("elemental_resistance_definition", "elemental_resistance_definition", "Стихийная устойчивость", "Типизированный уровень стихийной устойчивости.", new[]
            {
                Field0181("damageTypeId", "Тип урона", ContentDefinitionFieldTypes.String, true),
                Field0181("tier", "Уровень устойчивости", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100)
            }),
            Profile0181("environmental_tolerance_modifier_definition", "environmental_tolerance_modifier_definition", "Адаптация к среде", "Модификаторы физиологической переносимости среды.", new[]
            {
                Field0181("comfortMinDeltaC", "Сдвиг нижней границы, °C", ContentDefinitionFieldTypes.Decimal, false, min: -100, max: 100),
                Field0181("comfortMaxDeltaC", "Сдвиг верхней границы, °C", ContentDefinitionFieldTypes.Decimal, false, min: -100, max: 100),
                Field0181("coldSensitivityMultiplier", "Чувствительность к холоду", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("heatSensitivityMultiplier", "Чувствительность к жаре", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("wetSensitivityMultiplier", "Чувствительность к сырости", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("windSensitivityMultiplier", "Чувствительность к ветру", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("humiditySensitivityMultiplier", "Чувствительность к влажности", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("hypoxiaSensitivityMultiplier", "Чувствительность к нехватке кислорода", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100),
                Field0181("hydrationConsumptionMultiplier", "Расход воды", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100)
            }),
            Profile0181("title_definition", "title_definition", "Титулы персонажей", "Display titles unlocked by development, events or GM decisions.", new[]
            {
                Field0181("titleCategory", "Категория", ContentDefinitionFieldTypes.String, false),
                Field0181("sortOrder", "Порядок", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 10000)
            }),
            Profile0181("race_equipment_fit_profile", "race_equipment_fit_profile", "Совместимость экипировки", "Race equipment fit profile.", new[]
            {
                Field0181("raceId", "Раса", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "race_definition"),
                Field0181("fitTags", "Теги совместимости", ContentDefinitionFieldTypes.Tags, true),
                Field0181("rules", "Правила", ContentDefinitionFieldTypes.LongText, false)
            }),
            Profile0181("race_npc_reaction_rule", "race_npc_reaction_rule", "Отношения NPC", "NPC reaction rules by race.", new[]
            {
                Field0181("raceId", "Раса", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "race_definition"),
                Field0181("factionId", "Фракция", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "faction"),
                Field0181("reaction", "Реакция", ContentDefinitionFieldTypes.Enum, true, new[] { "friendly", "neutral", "suspicious", "hostile", "custom" }),
                Field0181("rules", "Правила", ContentDefinitionFieldTypes.LongText, false)
            }),
            Profile0181("race_language_grant", "race_language_grant", "Языки расы", "Race language grants.", new[]
            {
                Field0181("raceId", "Раса", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "race_definition"),
                Field0181("languageId", "Язык", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "language"),
                Field0181("minLevel", "Минимальный уровень", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10)
            }),
            Profile0181("race_knowledge_grant", "race_knowledge_grant", "Знания расы", "Race knowledge grants.", new[]
            {
                Field0181("raceId", "Раса", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "race_definition"),
                Field0181("knowledgeId", "Знание", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "knowledge"),
                Field0181("grantMode", "Режим", ContentDefinitionFieldTypes.Enum, true, new[] { "known", "available", "discount", "custom" })
            }),
            Profile0181("attribute_definition", "attribute_definition", "Характеристики", "Primary attribute definitions.", new[]
            {
                Field0181("ruleSetId", "RuleSet", ContentDefinitionFieldTypes.String, true),
                Field0181("shortName", "Кратко", ContentDefinitionFieldTypes.String, true),
                Field0181("description", "Описание", ContentDefinitionFieldTypes.LongText, false),
                Field0181("minValue", "Минимум", ContentDefinitionFieldTypes.Integer, true, min: -1000, max: 1000),
                Field0181("maxValue", "Максимум", ContentDefinitionFieldTypes.Integer, true, min: -1000, max: 1000),
                Field0181("defaultValue", "По умолчанию", ContentDefinitionFieldTypes.Integer, true, min: -1000, max: 1000),
                Field0181("displayOrder", "Порядок", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10000),
                Field0181("isPrimary", "Основная", ContentDefinitionFieldTypes.Boolean, false)
            }),
            Profile0181("subattribute_definition", "subattribute_definition", "Подхарактеристики", "Subattribute definitions.", new[]
            {
                Field0181("parentAttributeId", "Родительская характеристика", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "attribute_definition"),
                Field0181("ruleSetId", "RuleSet", ContentDefinitionFieldTypes.String, true),
                Field0181("shortName", "Кратко", ContentDefinitionFieldTypes.String, true),
                Field0181("description", "Описание", ContentDefinitionFieldTypes.LongText, false),
                Field0181("minValue", "Минимум", ContentDefinitionFieldTypes.Integer, true, min: -1000, max: 1000),
                Field0181("maxValue", "Максимум", ContentDefinitionFieldTypes.Integer, true, min: -1000, max: 1000),
                Field0181("defaultValue", "По умолчанию", ContentDefinitionFieldTypes.Integer, true, min: -1000, max: 1000),
                Field0181("displayOrder", "Порядок", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10000)
            }),
            Profile0181("derived_stat_definition", "derived_stat_definition", "Производные параметры", "Derived stat definitions.", new[]
            {
                Field0181("ruleSetId", "RuleSet", ContentDefinitionFieldTypes.String, true),
                Field0181("shortName", "Кратко", ContentDefinitionFieldTypes.String, true),
                Field0181("description", "Описание", ContentDefinitionFieldTypes.LongText, false),
                Field0181("calculationMode", "Расчёт", ContentDefinitionFieldTypes.Enum, true, new[] { "Manual", "Formula", "ProfileBased", "Custom" }),
                Field0181("formulaText", "Формула", ContentDefinitionFieldTypes.LongText, false),
                Field0181("displayOrder", "Порядок", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10000),
                Field0181("serverFormulaNotes", "Служебные заметки формулы", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isServerOnly: true)
            }),
            Profile0181("attribute_set_profile", "attribute_set_profile", "Набор характеристик", "Attribute set profile.", new[]
            {
                Field0181("ruleSetId", "RuleSet", ContentDefinitionFieldTypes.String, true),
                Field0181("attributeIds", "Характеристики", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: "attribute_definition")
            }),
            Profile0181("derived_stat_set_profile", "derived_stat_set_profile", "Набор производных", "Derived stat set profile.", new[]
            {
                Field0181("ruleSetId", "RuleSet", ContentDefinitionFieldTypes.String, true),
                Field0181("derivedStatIds", "Параметры", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: "derived_stat_definition")
            }),
            Profile0181("resolution_profile", "resolution_profile", "Профили основной проверки", "Политика основной проверки, которая связывает d20, мастерство, сложность и результат.", new[]
            {
                Field0181("ruleSetId", "Профиль правил", ContentDefinitionFieldTypes.String, true),
                Field0181("primaryDie", "Основной бросок", ContentDefinitionFieldTypes.Enum, true, new[] { "1d20" }),
                Field0181("abilityContributionPolicy", "Вклад способности", ContentDefinitionFieldTypes.Enum, true, new[] { "attribute_or_subattribute" }),
                Field0181("abilityModifierProfileId", "Профиль характеристики", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "ability_modifier_profile"),
                Field0181("skillMasteryProfileId", "Профиль мастерства", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "skill_mastery_profile"),
                Field0181("modifierCategoryProfileId", "Категории модификаторов", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "modifier_category_profile"),
                Field0181("advantagePolicyId", "Преимущество и помеха", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "advantage_policy"),
                Field0181("difficultyProfileId", "Шкала сложности", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "difficulty_profile"),
                Field0181("degreeOfSuccessProfileId", "Степени успеха", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "degree_of_success_profile"),
                Field0181("attemptGateProfileId", "Допуск к попытке", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "attempt_gate_profile"),
                Field0181("publicDescription", "Описание для игроков", ContentDefinitionFieldTypes.LongText, false),
                Field0181("gmDescription", "Пояснение для мастера", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            }),
            Profile0181("ability_modifier_profile", "ability_modifier_profile", "Модификаторы характеристик", "Преобразование значения характеристики или подхарактеристики в ограниченный модификатор.", new[]
            {
                Field0181("ruleSetId", "Профиль правил", ContentDefinitionFieldTypes.String, true),
                Field0181("mappingMode", "Способ преобразования", ContentDefinitionFieldTypes.Enum, true, new[] { "score_to_modifier", "identity_clamped", "threshold_bands", "lookup_table" }),
                Field0181("minimumModifier", "Минимальный модификатор", ContentDefinitionFieldTypes.Integer, true, min: -20, max: 0),
                Field0181("maximumModifier", "Максимальный модификатор", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 20)
            }),
            Profile0181("skill_mastery_profile", "skill_mastery_profile", "Мастерство навыков", "Ранги 0–20 преобразуются в один ограниченный бонус мастерства.", new[]
            {
                Field0181("ruleSetId", "Профиль правил", ContentDefinitionFieldTypes.String, true),
                Field0181("minimumRank", "Минимальный ранг", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 20),
                Field0181("maximumRank", "Максимальный ранг", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 20),
                Field0181("rank1To4Bonus", "Новичок: бонус", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10),
                Field0181("rank5To8Bonus", "Обученный: бонус", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10),
                Field0181("rank9To12Bonus", "Профессионал: бонус", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10),
                Field0181("rank13To16Bonus", "Эксперт: бонус", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10),
                Field0181("rank17To20Bonus", "Мастер: бонус", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10)
            }),
            Profile0181("modifier_category_profile", "modifier_category_profile", "Категории модификаторов", "Границы временных бонусов и штрафов без суммирования одинаковых источников.", new[]
            {
                Field0181("ruleSetId", "Профиль правил", ContentDefinitionFieldTypes.String, true),
                Field0181("maximumPositiveTemporaryTotal", "Общий предел временных бонусов", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 20),
                Field0181("equipmentPositiveCap", "Снаряжение: максимум бонуса", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10),
                Field0181("enhancementPositiveCap", "Усиление: максимум бонуса", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10),
                Field0181("circumstancePositiveCap", "Обстоятельства: максимум бонуса", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 10),
                Field0181("conditionNegativeCap", "Состояния: предел штрафа", ContentDefinitionFieldTypes.Integer, true, min: -20, max: 0)
            }),
            Profile0181("advantage_policy", "advantage_policy", "Преимущество и помеха", "Правила выбора результата двух d20 без плоского числового бонуса.", new[]
            {
                Field0181("ruleSetId", "Профиль правил", ContentDefinitionFieldTypes.String, true),
                Field0181("advantageMode", "Преимущество", ContentDefinitionFieldTypes.Enum, true, new[] { "highest_of_2d20" }),
                Field0181("hindranceMode", "Помеха", ContentDefinitionFieldTypes.Enum, true, new[] { "lowest_of_2d20" }),
                Field0181("opposedStatesCancel", "Преимущество и помеха взаимно отменяются", ContentDefinitionFieldTypes.Boolean, true)
            }),
            Profile0181("difficulty_profile", "difficulty_profile", "Шкалы сложности", "Именованные сложности для основной проверки.", new[]
            {
                Field0181("ruleSetId", "Профиль правил", ContentDefinitionFieldTypes.String, true),
                Field0181("easy", "Легко", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 40),
                Field0181("standard", "Обычно", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 40),
                Field0181("hard", "Сложно", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 40),
                Field0181("severe", "Очень сложно", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 40),
                Field0181("extreme", "Предельно сложно", ContentDefinitionFieldTypes.Integer, true, min: 1, max: 40)
            }),
            Profile0181("degree_of_success_profile", "degree_of_success_profile", "Степени успеха", "Качество успеха определяется запасом над сложностью.", new[]
            {
                Field0181("ruleSetId", "Профиль правил", ContentDefinitionFieldTypes.String, true),
                Field0181("ordinaryMinimumMargin", "Обычный успех: от", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 20),
                Field0181("strongMinimumMargin", "Сильный успех: от", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 20),
                Field0181("exceptionalMinimumMargin", "Выдающийся успех: от", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 20)
            }),
            Profile0181("attempt_gate_profile", "attempt_gate_profile", "Допуск к попытке", "Проверяет знания, инструменты и совместимость до броска.", new[]
            {
                Field0181("ruleSetId", "Профиль правил", ContentDefinitionFieldTypes.String, true),
                Field0181("rejectMissingKnowledge", "Запрет без необходимого знания", ContentDefinitionFieldTypes.Boolean, true),
                Field0181("rejectMissingTool", "Запрет без необходимого инструмента", ContentDefinitionFieldTypes.Boolean, true),
                Field0181("rejectBodyIncompatibility", "Запрет при несовместимости тела", ContentDefinitionFieldTypes.Boolean, true),
                Field0181("naturalTwentyBypassesGate", "Натуральная 20 обходит запрет", ContentDefinitionFieldTypes.Boolean, true)
            }),
            Profile0181("hit_resolution_profile", "hit_resolution_profile", "Попадание и защита", "Разрешение попадания отдельно от брони и пробития.", new[]
            {
                Field0181("ruleSetId", "Профиль правил", ContentDefinitionFieldTypes.String, true),
                Field0181("passiveDefenseBase", "Базовая пассивная защита", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 40),
                Field0181("naturalTwentyGuaranteesHit", "Натуральная 20 гарантирует попадание", ContentDefinitionFieldTypes.Boolean, true),
                Field0181("naturalTwentyGuaranteesPenetration", "Натуральная 20 гарантирует пробитие", ContentDefinitionFieldTypes.Boolean, true),
                Field0181("armorAddsToHitDefense", "Броня увеличивает защиту от попадания", ContentDefinitionFieldTypes.Boolean, true)
            }),
            Profile0181("penetration_damage_profile", "penetration_damage_profile", "Пробитие и урон", "Порядок пробития, защиты, урона и смягчения.", new[]
            {
                Field0181("ruleSetId", "Профиль правил", ContentDefinitionFieldTypes.String, true),
                Field0181("hitAndPenetrationAreSeparate", "Попадание и пробитие разделены", ContentDefinitionFieldTypes.Boolean, true),
                Field0181("mitigationAppliesAfterPenetration", "Снижение урона после пробития", ContentDefinitionFieldTypes.Boolean, true),
                Field0181("penetrationTypes", "Типы пробития", ContentDefinitionFieldTypes.Tags, true)
            }),
            Profile0181("skill_definition", "skill_definition", "Навыки", "Skill definitions.", new[]
            {
                Field0181("displayGroup", "Группа", ContentDefinitionFieldTypes.Enum, true, new[] { "Physical", "Dexterity", "Endurance", "Knowledge", "Technical", "Field", "Military", "Social", "Vehicle/Control", "Magic", "Custom" }),
                Field0181("defaultAttribute", "Характеристика по умолчанию", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "attribute_definition"),
                Field0181("allowedAttributes", "Допустимые характеристики", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: "attribute_definition"),
                Field0181("allowedSubAttributes", "Допустимые подхарактеристики", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "subattribute_definition"),
                Field0181("rankMin", "Мин. ранг", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100),
                Field0181("rankMax", "Макс. ранг", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100),
                Field0181("isRollable", "Можно бросать", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("description", "Описание", ContentDefinitionFieldTypes.LongText, false),
                Field0181("gmSkillNotes", "GM-заметки навыка", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            }),
            Profile0181("skill_technique_definition", "skill_technique_definition", "Техники навыков", "Действия и эффекты, открываемые определённым рангом навыка.", new[]
            {
                Field0181("skillId", "Навык", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "skill_definition"),
                Field0181("requiredRank", "Требуемый ранг", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 20),
                Field0181("actionDefinitionId", "Открываемое действие", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "action_definition"),
                Field0181("publicDescription", "Описание для игрока", ContentDefinitionFieldTypes.LongText, true),
                Field0181("gmNotes", "Пояснение для мастера", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            }),
            Profile0181("skill_group_definition", "skill_group_definition", "Группы навыков", "Skill group definitions.", new[]
            {
                Field0181("groupKey", "Ключ группы", ContentDefinitionFieldTypes.String, true),
                Field0181("displayOrder", "Порядок", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 10000)
            }),
            Profile0181("skill_roll_context_template", "skill_roll_context_template", "Шаблоны проверок", "Skill roll context templates.", new[]
            {
                Field0181("skillId", "Навык", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "skill_definition"),
                Field0181("contextText", "Контекст", ContentDefinitionFieldTypes.LongText, true),
                Field0181("difficultyNotes", "Сложность", ContentDefinitionFieldTypes.LongText, false)
            }),
            Profile0181("development_node_definition", "development_node_definition", "Узлы развития", "Development node definitions without runtime purchase.", new[]
            {
                Field0181("ruleSetId", "RuleSet", ContentDefinitionFieldTypes.String, true),
                Field0181("nodeType", "Тип узла", ContentDefinitionFieldTypes.Enum, true, new[] { "Class", "Skill", "Profession", "Specialization", "Augmentation", "Implant", "Cyberware", "MagicPath", "SpellSchool", "CombatDoctrine", "License", "Training", "FactionSchool", "TechnologyAccess", "ShipClassUnlock", "ResearchDiscipline", "PsionicDiscipline", "Mutation", "AnomalyAdaptation", "Custom" }),
                Field0181("directionId", "Направление", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "development_direction_definition"),
                Field0181("tier", "Уровень", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100),
                Field0181("maxTier", "Макс. уровень", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 100),
                Field0181("requirements", "Требования", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "development_requirement_definition"),
                Field0181("cost", "Стоимость", ContentDefinitionFieldTypes.LongText, false),
                Field0181("rewards", "Награды", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "development_reward_definition"),
                Field0181("linkedSkills", "Связанные навыки", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "skill_definition"),
                Field0181("linkedAttributes", "Связанные характеристики", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "attribute_definition"),
                Field0181("linkedSubAttributes", "Связанные подхарактеристики", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "subattribute_definition"),
                Field0181("linkedModules", "Модули", ContentDefinitionFieldTypes.Tags, false),
                Field0181("isHidden", "Скрытый узел", ContentDefinitionFieldTypes.Boolean, false)
            }),
            Profile0181("development_requirement_definition", "development_requirement_definition", "Требования развития", "Reusable development requirements.", new[]
            {
                Field0181("requirementType", "Тип требования", ContentDefinitionFieldTypes.Enum, true, new[] { "required_node", "required_skill", "required_attribute", "required_knowledge", "required_race", "required_item_tag", "gm_approval", "custom_text" }),
                Field0181("targetId", "Цель", ContentDefinitionFieldTypes.String, false),
                Field0181("publicText", "Текст для игрока", ContentDefinitionFieldTypes.LongText, false),
                Field0181("gmText", "GM-текст", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            }),
            Profile0181("development_reward_definition", "development_reward_definition", "Награды развития", "Reusable development rewards.", new[]
            {
                Field0181("rewardType", "Тип награды", ContentDefinitionFieldTypes.Enum, true, new[] { "attribute_bonus", "subattribute_bonus", "skill_bonus", "unlock_node", "unlock_action", "unlock_equipment_access", "unlock_knowledge", "title", "legal_access", "custom_text" }),
                Field0181("targetId", "Цель", ContentDefinitionFieldTypes.String, false),
                Field0181("publicText", "Текст для игрока", ContentDefinitionFieldTypes.LongText, false),
                Field0181("gmText", "GM-текст", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            }),
            Profile0181("development_direction_definition", "development_direction_definition", "Направления развития", "Development direction and hexagon metadata.", new[]
            {
                Field0181("directionKey", "Ключ направления", ContentDefinitionFieldTypes.String, true),
                Field0181("attributeId", "Характеристика", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "attribute_definition"),
                Field0181("hexagonLabel", "Подпись гекса", ContentDefinitionFieldTypes.String, false),
                Field0181("displayOrder", "Порядок", ContentDefinitionFieldTypes.Integer, false, min: 0, max: 10000)
            }),
            Profile0181("development_hexagon_profile", "development_hexagon_profile", "Hexagon profile", "Development hexagon display profile.", new[]
            {
                Field0181("ruleSetId", "RuleSet", ContentDefinitionFieldTypes.String, true),
                Field0181("mode", "Режим", ContentDefinitionFieldTypes.Enum, true, new[] { "main", "magic", "custom" }),
                Field0181("directionIds", "Направления", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: "development_direction_definition"),
                Field0181("notes", "Заметки", ContentDefinitionFieldTypes.LongText, false)
            })
        };

        foreach (var profile in profiles)
        {
            profile.SchemaVersion = 2;
            profile.DefaultTags = profile.DefaultTags.Concat(new[] { "foundation_0_18_2", "character_foundation" }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!profile.ValidationRules.Contains("character-definition-family", StringComparer.OrdinalIgnoreCase))
                profile.ValidationRules.Add("character-definition-family");
        }
        return profiles;
    }

    private static void ApplyCharacterDefinitionValidation0182(ContentDefinitionRecord record, DefinitionEditorProfile profile, ContentDefinitionValidationResult result)
    {
        switch (record.Category)
        {
            case "race_definition":
            case "subspecies_definition":
            case "hybrid_definition":
            case "hybrid_subtype_definition":
                ValidateRaceRecord0182(record, result);
                break;
            case "attribute_definition":
            case "subattribute_definition":
            case "derived_stat_definition":
                ValidateAttributeRecord0182(record, result);
                break;
            case "skill_definition":
            case "skill_technique_definition":
                ValidateSkillRecord0182(record, result);
                break;
            case "development_node_definition":
                ValidateDevelopmentNodeRecord0182(record, result);
                break;
            case "resolution_profile":
            case "ability_modifier_profile":
            case "skill_mastery_profile":
            case "modifier_category_profile":
            case "advantage_policy":
            case "difficulty_profile":
            case "degree_of_success_profile":
            case "attempt_gate_profile":
            case "hit_resolution_profile":
            case "penetration_damage_profile":
                ValidateResolutionRecord0219(record, result);
                break;
        }
    }

    private static void ValidateResolutionRecord0219(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(FieldText0182(record, "ruleSetId")))
            result.Errors.Add("Выберите профиль правил.");
        if (string.Equals(record.Category, "ability_modifier_profile", StringComparison.OrdinalIgnoreCase))
        {
            var minimum = FieldInt0182(record, "minimumModifier");
            var maximum = FieldInt0182(record, "maximumModifier");
            if (minimum > maximum) result.Errors.Add("Минимальный модификатор не может быть больше максимального.");
        }
        if (string.Equals(record.Category, "skill_mastery_profile", StringComparison.OrdinalIgnoreCase)
            && FieldInt0182(record, "maximumRank") != 20)
            result.Warnings.Add("Для fantasy_nri_default принят диапазон рангов 0–20.");
        if (string.Equals(record.Category, "attempt_gate_profile", StringComparison.OrdinalIgnoreCase)
            && FieldBool0182(record, "naturalTwentyBypassesGate"))
            result.Errors.Add("В fantasy_nri_default натуральная 20 не обходит запрет попытки.");
        if (string.Equals(record.Category, "hit_resolution_profile", StringComparison.OrdinalIgnoreCase)
            && FieldBool0182(record, "naturalTwentyGuaranteesPenetration"))
            result.Errors.Add("Натуральная 20 не может автоматически пробивать броню.");
    }

    private static bool IsCharacterDefinitionPlayerVisible0182(ContentDefinitionRecord record)
    {
        var availability = FieldText0182(record, "availabilityType");
        if (!string.IsNullOrWhiteSpace(availability) && IsHiddenAvailability0182(availability))
            return false;
        if (string.Equals(record.Category, "race_trait_definition", StringComparison.OrdinalIgnoreCase)
            && !FieldBool0182(record, "isVisibleToPlayer"))
            return false;
        if (string.Equals(record.Category, "development_node_definition", StringComparison.OrdinalIgnoreCase)
            && FieldBool0182(record, "isHidden"))
            return false;
        return true;
    }

    public ResponseEnvelope ContentDefinitionAdminListRaceFamily(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return Ok("Race definition family loaded.", new Dictionary<string, object>
        {
            ["definitions"] = AdminRecordsByCategories0182(RaceDefinitionCategories0182, context.Request.Payload),
            ["categories"] = RaceDefinitionCategories0182.Cast<object>().ToArray()
        });
    }

    public ResponseEnvelope ContentDefinitionAdminValidateRaceFamily(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return ValidateDefinitionForCommand0182(context, actor, "Race family validation completed.");
    }

    public ResponseEnvelope ContentDefinitionAdminPreviewRaceAsPlayer(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        var profile = FindDefinitionProfileByCategory0181(record.Category);
        return Ok("Race player preview built.", new Dictionary<string, object>
        {
            ["definition"] = _contentDefinitionProjection0181.PlayerRecordPayload(record, profile),
            ["isPlayerVisible"] = IsDefinitionPlayerVisible0181(record)
        });
    }

    public ResponseEnvelope ContentDefinitionAdminListAttributeFamily(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return Ok("Attribute definition family loaded.", new Dictionary<string, object>
        {
            ["definitions"] = AdminRecordsByCategories0182(AttributeDefinitionCategories0182, context.Request.Payload),
            ["categories"] = AttributeDefinitionCategories0182.Cast<object>().ToArray()
        });
    }

    public ResponseEnvelope ContentDefinitionAdminValidateAttributeFamily(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return ValidateDefinitionForCommand0182(context, actor, "Attribute validation completed.");
    }

    public ResponseEnvelope ContentDefinitionAdminListSkillDefinitions(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return Ok("Skill definitions loaded.", new Dictionary<string, object>
        {
            ["definitions"] = AdminRecordsByCategories0182(SkillDefinitionCategories0182, context.Request.Payload)
        });
    }

    public ResponseEnvelope ContentDefinitionAdminValidateSkillDefinition(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return ValidateDefinitionForCommand0182(context, actor, "Skill validation completed.");
    }

    public ResponseEnvelope ContentDefinitionAdminPreviewSkillRow(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        if (!string.Equals(record.Category, "skill_definition", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Definition is not a skill definition.");
        var allowed = SplitRefs0182(FieldText0182(record, "allowedAttributes")).ToArray();
        var preview = new Dictionary<string, object>
        {
            ["definitionId"] = record.Id,
            ["name"] = record.DisplayName,
            ["displayGroup"] = FieldText0182(record, "displayGroup"),
            ["defaultAttribute"] = FieldText0182(record, "defaultAttribute"),
            ["sampleBonus"] = "+0",
            ["rollIconVisible"] = FieldBool0182(record, "isRollable"),
            ["settingsIconVisible"] = allowed.Length > 1,
            ["playerSafe"] = _contentDefinitionProjection0181.PlayerRecordPayload(record, FindDefinitionProfileByCategory0181(record.Category))
        };
        return Ok("Skill preview row built.", new Dictionary<string, object> { ["preview"] = preview });
    }

    public ResponseEnvelope ContentDefinitionAdminListDevelopmentDefinitions(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return Ok("Development definitions loaded.", new Dictionary<string, object>
        {
            ["definitions"] = AdminRecordsByCategories0182(DevelopmentDefinitionCategories0182, context.Request.Payload)
        });
    }

    public ResponseEnvelope ContentDefinitionAdminValidateDevelopmentNode(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return ValidateDefinitionForCommand0182(context, actor, "Development node validation completed.");
    }

    public ResponseEnvelope ContentDefinitionAdminPreviewDevelopmentNode(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        if (!string.Equals(record.Category, "development_node_definition", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Definition is not a development node definition.");
        var preview = new Dictionary<string, object>
        {
            ["definitionId"] = record.Id,
            ["name"] = record.DisplayName,
            ["nodeType"] = FieldText0182(record, "nodeType"),
            ["tier"] = FieldText0182(record, "tier"),
            ["isHidden"] = FieldBool0182(record, "isHidden"),
            ["warning"] = FieldBool0182(record, "isHidden") ? "Hidden node is not shown in Player preview." : string.Empty,
            ["playerSafe"] = _contentDefinitionProjection0181.PlayerRecordPayload(record, FindDefinitionProfileByCategory0181(record.Category))
        };
        return Ok("Development node preview built.", new Dictionary<string, object> { ["preview"] = preview });
    }

    public ResponseEnvelope ContentDefinitionPlayerListPlayableRaces(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return Ok("Playable races loaded.", new Dictionary<string, object>
        {
            ["definitions"] = PlayerRecordsByCategories0182(new[] { "race_definition", "subspecies_definition", "hybrid_definition", "hybrid_subtype_definition", "race_trait_definition" }, context.Request.Payload)
        });
    }

    public ResponseEnvelope ContentDefinitionPlayerGetPlayableRace(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        if (!RaceDefinitionCategories0182.Contains(record.Category, StringComparer.OrdinalIgnoreCase) || !IsDefinitionPlayerVisible0181(record))
            throw new KeyNotFoundException("Playable race definition not found.");
        var profile = FindDefinitionProfileByCategory0181(record.Category);
        return Ok("Playable race loaded.", new Dictionary<string, object> { ["definition"] = _contentDefinitionProjection0181.PlayerRecordPayload(record, profile) });
    }

    public ResponseEnvelope ContentDefinitionPlayerListVisibleSkills(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return Ok("Visible skills loaded.", new Dictionary<string, object>
        {
            ["definitions"] = PlayerRecordsByCategories0182(new[] { "skill_definition" }, context.Request.Payload)
        });
    }

    public ResponseEnvelope ContentDefinitionPlayerListVisibleDevelopmentNodes(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureInitialDefinitionEditorProfiles0181();
        return Ok("Visible development nodes loaded.", new Dictionary<string, object>
        {
            ["definitions"] = PlayerRecordsByCategories0182(new[] { "development_node_definition" }, context.Request.Payload)
        });
    }

    private ResponseEnvelope ValidateDefinitionForCommand0182(CommandContext context, UserAccount actor, string message)
    {
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        var profile = FindDefinitionProfileByCategory0181(record.Category) ?? throw new KeyNotFoundException("Definition editor profile not found.");
        var validation = ValidateAndStoreContentDefinition0181(record, profile, actor.Id);
        return Ok(message, new Dictionary<string, object> { ["validation"] = _contentDefinitionProjection0181.ValidationPayload(validation) });
    }

    private object[] AdminRecordsByCategories0182(IEnumerable<string> categories, IDictionary<string, object> payload)
    {
        var categorySet = categories.ToArray();
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var search = PayloadReader.GetString(payload, "search") ?? string.Empty;
        var ruleSet = PayloadReader.GetString(payload, "ruleSetId") ?? string.Empty;
        var visibility = PayloadReader.GetString(payload, "visibilityRule") ?? string.Empty;
        var filter = Builders<ContentDefinitionRecord>.Filter.In(x => x.Category, categorySet);
        if (!includeArchived)
            filter &= Builders<ContentDefinitionRecord>.Filter.Ne(x => x.IsArchived, true);
        if (!string.IsNullOrWhiteSpace(ruleSet))
            filter &= Builders<ContentDefinitionRecord>.Filter.Eq(x => x.RuleSetId, ruleSet);
        if (!string.IsNullOrWhiteSpace(visibility))
            filter &= Builders<ContentDefinitionRecord>.Filter.Eq(x => x.VisibilityRule, visibility);
        var records = _mongo.ContentDefinitionRecords.Find(filter).ToList();
        if (!string.IsNullOrWhiteSpace(search))
            records = records.Where(x => Contains0182(x.Name, search) || Contains0182(x.DisplayName, search) || Contains0182(x.Id, search) || Contains0182(x.ShortCode, search) || x.Tags.Any(t => Contains0182(t, search)) || Contains0182(x.Category, search)).ToList();
        var profiles = BuildDefinitionProfileLookup0181();
        return records
            .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => _contentDefinitionProjection0181.AdminRecordPayload(x, profiles.TryGetValue(x.Category, out var p) ? p : null, GetLatestValidation0181(x.Id), includeAuditSummary: false))
            .Cast<object>()
            .ToArray();
    }

    private object[] PlayerRecordsByCategories0182(IEnumerable<string> categories, IDictionary<string, object> payload)
    {
        var categorySet = categories.ToArray();
        var search = PayloadReader.GetString(payload, "search") ?? string.Empty;
        var filter = Builders<ContentDefinitionRecord>.Filter.In(x => x.Category, categorySet)
                     & Builders<ContentDefinitionRecord>.Filter.Ne(x => x.IsArchived, true);
        var profiles = BuildDefinitionProfileLookup0181();
        var records = _mongo.ContentDefinitionRecords.Find(filter).ToList().Where(IsDefinitionPlayerVisible0181).ToList();
        if (!string.IsNullOrWhiteSpace(search))
            records = records.Where(x => MatchesPlayerVisibleSearch0181(x, profiles.TryGetValue(x.Category, out var profile) ? profile : null, search)).ToList();
        return records
            .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => _contentDefinitionProjection0181.PlayerRecordPayload(x, profiles.TryGetValue(x.Category, out var p) ? p : null))
            .Cast<object>()
            .ToArray();
    }

    private static void ValidateRaceRecord0182(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var availability = FieldText0182(record, "availabilityType");
        if (IsHiddenAvailability0182(availability) && IsPlayerVisibility0182(record.VisibilityRule))
            result.VisibilityWarnings.Add("Hidden/GM/NPC/Monster/Wild race availability must not be player-visible.");
        var minHeight = FieldInt0182(record, "minHeightCm");
        var maxHeight = FieldInt0182(record, "maxHeightCm");
        if (minHeight.HasValue && maxHeight.HasValue && minHeight.Value > maxHeight.Value)
            result.Errors.Add("Минимальный рост не может превышать максимальный.");
        var adultAge = FieldInt0182(record, "adultAgeYears");
        var averageLifespan = FieldInt0182(record, "averageLifespanYears");
        var maximumLifespan = FieldInt0182(record, "maximumLifespanYears");
        if (adultAge.HasValue && averageLifespan.HasValue && adultAge.Value > averageLifespan.Value)
            result.Errors.Add("Возраст взросления не может превышать среднюю продолжительность жизни.");
        if (averageLifespan.HasValue && maximumLifespan.HasValue && averageLifespan.Value > maximumLifespan.Value)
            result.Errors.Add("Средняя продолжительность жизни не может превышать предельную.");
        if (IsPlayerVisibility0182(record.VisibilityRule))
        {
            if ((FieldInt0182(record, "baseHealth") ?? 0) <= 0) result.Errors.Add("Для игрового происхождения требуется положительное базовое здоровье.");
            if ((FieldInt0182(record, "naturalArmorRating") ?? 0) < 1) result.Errors.Add("Естественная броня игрового происхождения должна быть не ниже 1.");
            if ((FieldInt0182(record, "naturalPenetrationResistance") ?? 0) < 1) result.Errors.Add("Естественная стойкость к пробитию игрового происхождения должна быть не ниже 1.");
        }
        if (string.Equals(record.Category, "race_definition", StringComparison.OrdinalIgnoreCase)
            && IsPlayerVisibility0182(record.VisibilityRule)
            && string.IsNullOrWhiteSpace(FieldText0182(record, "fullPlayerDescription")))
            result.Errors.Add("Player-visible race must have FullPlayerDescription.");
        if (string.Equals(record.Category, "hybrid_definition", StringComparison.OrdinalIgnoreCase))
        {
            var parents = SplitRefs0182(FieldText0182(record, "parentLineages")).ToArray();
            if (parents.Length != 2)
                result.Errors.Add("Гибрид должен явно содержать ровно две родительские линии.");
            if (parents.Distinct(StringComparer.OrdinalIgnoreCase).Count() != parents.Length)
                result.Errors.Add("Родительские линии гибрида должны различаться.");
            if (string.IsNullOrWhiteSpace(FieldText0182(record, "languageRules")))
                result.Errors.Add("Для гибрида требуется явное правило языков.");
        }
        if (string.Equals(record.Category, "natural_attack_definition", StringComparison.OrdinalIgnoreCase))
        {
            if ((FieldInt0182(record, "diceCount") ?? 0) < 1) result.Errors.Add("Количество костей должно быть не меньше 1.");
            if ((FieldInt0182(record, "dieSides") ?? 0) < 2) result.Errors.Add("У кости должно быть не меньше 2 граней.");
            var transfer = FieldDecimal0182(record, "failedPenetrationDamageTransfer");
            if (transfer.HasValue && (transfer.Value < 0m || transfer.Value > 1m)) result.Errors.Add("Доля урона без пробития должна находиться от 0 до 1.");
            if ((FieldInt0182(record, "cooldownRounds") ?? 0) < 0) result.Errors.Add("Перезарядка не может быть отрицательной.");
        }
        if (Contains0182(record.Name + record.DisplayName + record.PublicDescription, "spirit") && Contains0182(record.CustomFieldsText0182(), "undead"))
            result.Warnings.Add("Spirit is not automatically undead; keep undead as an explicit trait/rule.");
        if (Contains0182(record.Name + record.DisplayName, "vampire") && !Contains0182(record.CustomFieldsText0182(), "sunlight"))
            result.Warnings.Add("Vampire sunlight penalty should be encoded as a trait/rule, not hardcoded death.");
        if (Contains0182(record.Name + record.DisplayName, "lich") && !Contains0182(record.CustomFieldsText0182(), "phylactery"))
            result.Warnings.Add("Lich should have phylactery trait/rule or explicit warning.");
    }

    private static void ValidateAttributeRecord0182(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var min = FieldInt0182(record, "minValue");
        var max = FieldInt0182(record, "maxValue");
        var def = FieldInt0182(record, "defaultValue");
        if (min.HasValue && max.HasValue && min.Value > max.Value)
            result.Errors.Add("MinValue must be <= MaxValue.");
        if (min.HasValue && max.HasValue && def.HasValue && (def.Value < min.Value || def.Value > max.Value))
            result.Errors.Add("DefaultValue must be between MinValue and MaxValue.");
        if (IsPlayerVisibility0182(record.VisibilityRule) && string.IsNullOrWhiteSpace(record.PublicDescription))
            result.Warnings.Add("Player-visible stat should have PublicDescription.");
    }

    private static void ValidateSkillRecord0182(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var defaultAttribute = FieldText0182(record, "defaultAttribute");
        var allowed = SplitRefs0182(FieldText0182(record, "allowedAttributes")).ToArray();
        if (!string.IsNullOrWhiteSpace(defaultAttribute) && allowed.Length > 0 && !allowed.Contains(defaultAttribute, StringComparer.OrdinalIgnoreCase))
            result.Errors.Add("DefaultAttribute must be inside AllowedAttributes.");
        var min = FieldInt0182(record, "rankMin");
        var max = FieldInt0182(record, "rankMax");
        if (min.HasValue && max.HasValue && min.Value > max.Value)
            result.Errors.Add("RankMin must be <= RankMax.");
        if (IsPlayerVisibility0182(record.VisibilityRule) && string.IsNullOrWhiteSpace(FieldText0182(record, "displayGroup")))
            result.Errors.Add("Player-facing skill must have DisplayGroup.");
    }

    private static void ValidateDevelopmentNodeRecord0182(ContentDefinitionRecord record, ContentDefinitionValidationResult result)
    {
        var tier = FieldInt0182(record, "tier");
        var maxTier = FieldInt0182(record, "maxTier");
        if (tier.HasValue && maxTier.HasValue && tier.Value > maxTier.Value)
            result.Errors.Add("Tier must be <= MaxTier.");
        if (FieldBool0182(record, "isHidden") && IsPlayerVisibility0182(record.VisibilityRule))
            result.VisibilityWarnings.Add("Hidden development nodes are stripped from Player preview.");
    }

    private static bool IsHiddenAvailability0182(string availability)
        => string.Equals(availability, "GMOnly", StringComparison.OrdinalIgnoreCase)
           || string.Equals(availability, "NPCOnly", StringComparison.OrdinalIgnoreCase)
           || string.Equals(availability, "MonsterOnly", StringComparison.OrdinalIgnoreCase)
           || string.Equals(availability, "Hidden", StringComparison.OrdinalIgnoreCase)
           || string.Equals(availability, "WildOnly", StringComparison.OrdinalIgnoreCase)
           || string.Equals(availability, "Archived", StringComparison.OrdinalIgnoreCase);

    private static bool IsPlayerVisibility0182(string visibility)
        => string.Equals(visibility, ContentDefinitionVisibilityRules.PlayerVisible, StringComparison.OrdinalIgnoreCase)
           || string.Equals(visibility, ContentDefinitionVisibilityRules.Public, StringComparison.OrdinalIgnoreCase);

    private static string FieldText0182(ContentDefinitionRecord record, string field)
        => record.CustomFields.TryGetValue(field, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private static int? FieldInt0182(ContentDefinitionRecord record, string field)
    {
        var text = FieldText0182(record, field);
        return int.TryParse(text, out var value) ? value : null;
    }

    private static decimal? FieldDecimal0182(ContentDefinitionRecord record, string field)
    {
        var text = FieldText0182(record, field);
        return decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : (decimal?)null;
    }

    private static bool FieldBool0182(ContentDefinitionRecord record, string field)
    {
        var text = FieldText0182(record, field);
        return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SplitRefs0182(string text)
        => (text ?? string.Empty).Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0);

    private static bool Contains0182(string? value, string search)
        => !string.IsNullOrWhiteSpace(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
}

internal static class ContentDefinitionRecord0182Extensions
{
    public static string CustomFieldsText0182(this ContentDefinitionRecord record)
    {
        if (record.CustomFields.Count == 0) return string.Empty;
        return string.Join("|", record.CustomFields.Select(x => x.Key + "=" + Convert.ToString(x.Value)));
    }
}
