using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Server.FateEngine;

public sealed class FateEffectCatalog
{
    private static readonly IReadOnlyList<FateLayerEffectDefinition> Effects = Build();

    public IReadOnlyList<FateLayerEffectDefinition> GetAll() => Effects;

    public IReadOnlyList<FateLayerEffectDefinition> GetByLayer(int layerNumber)
    {
        return Effects.Where(x => x.LayerNumber == layerNumber).ToList();
    }

    public FateLayerEffectDefinition? Find(int layerNumber, string effectCode)
    {
        return Effects.FirstOrDefault(x => x.LayerNumber == layerNumber && string.Equals(x.EffectCode, effectCode, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<FateLayerEffectDefinition> Build()
    {
        var items = new List<FateLayerEffectDefinition>
        {
            // Layer 1: Местность
            Def(1, "Местность", "CalmArea", "Спокойная обстановка", "None", "None", false, false, "Модификаторов нет."),
            Def(1, "Местность", "CombatZone", "Боёвка", "PullToMiddle", "Medium", false, false, "Выше шанс выпадения средних значений."),
            Def(1, "Местность", "CursedLand", "Проклятые земли", "BiasDown", "Medium", false, false, "Значения становятся хуже."),
            Def(1, "Местность", "BlessedLand", "Благословлённые земли", "BiasUp", "Medium", false, false, "Значения становятся лучше."),
            Def(1, "Местность", "Hell", "Ад", "BiasDown", "Strong", false, false, "Значения становятся очень плохими."),
            Def(1, "Местность", "ChaosZone", "Хаос", "Chaos", "Strong", true, false, "Использует волновые/хаотичные значения и делает броски непредсказуемыми."),
            Def(1, "Местность", "Drama", "Драма", "PullToExtreme", "Medium", false, false, "Выше шанс краевых значений."),
            Def(1, "Местность", "KeyMoment", "Ключевой момент", "BiasUp", "Strong", false, false, "Значения становятся очень хорошими."),
            Def(1, "Местность", "AnomalousSpace", "Аномальное пространство", "Anomaly", "Strong", false, true, "К броску добавляется большой случайный модификатор в случайную сторону. Итог может выйти за границы кубика."),

            // Layer 2: Эффекты персонажа
            Def(2, "Эффекты персонажа", "Blessing", "Благословение", "BiasUp", "Medium", false, false, "Чаще хорошие значения."),
            Def(2, "Эффекты персонажа", "StrongBlessing", "Сильное благословение", "BiasUp", "Strong", false, false, "Сильно тянет значения вверх."),
            Def(2, "Эффекты персонажа", "Curse", "Проклятие", "BiasDown", "Medium", false, false, "Чаще плохие значения."),
            Def(2, "Эффекты персонажа", "StrongCurse", "Сильное проклятие", "BiasDown", "Strong", false, false, "Сильно тянет значения вниз."),
            Def(2, "Эффекты персонажа", "Wounded", "Ранение", "BiasDown", "Weak", false, false, "Слегка ухудшает значения."),
            Def(2, "Эффекты персонажа", "Exhausted", "Истощение", "BiasDown", "Medium", false, false, "Чаще низкие и средне-низкие значения."),
            Def(2, "Эффекты персонажа", "Poisoned", "Отравление", "Destabilize", "Weak", false, false, "Нестабильно ухудшает результат."),
            Def(2, "Эффекты персонажа", "Regeneration", "Регенерация", "Stabilize", "Weak", false, false, "Слегка стабилизирует результат вверх."),
            Def(2, "Эффекты персонажа", "MagicOverload", "Магическая перегрузка", "PullToExtreme", "Medium", true, false, "Чаще крайние значения из-за перегрузки магией."),
            Def(2, "Эффекты персонажа", "AnomalousMutation", "Аномальная мутация", "Anomaly", "Medium", false, true, "Аномальный сдвиг в случайную сторону из-за изменения тела или сущности персонажа."),
            Def(2, "Эффекты персонажа", "ChaoticAura", "Аура хаоса", "Chaos", "Medium", true, false, "Хаотичное распределение и непредсказуемость результата."),
            Def(2, "Эффекты персонажа", "SacredMark", "Священная метка", "StabilizeUp", "Medium", false, false, "Значения становятся лучше и стабильнее."),
            Def(2, "Эффекты персонажа", "DoomMark", "Метка обречённости", "DestabilizeDown", "Medium", true, false, "Значения становятся хуже и нестабильнее."),

            // Layer 3: Предметы
            Def(3, "Предметы", "BlessedItem", "Благословлённый предмет", "BiasUp", "Medium", false, false, "Чаще хорошие значения."),
            Def(3, "Предметы", "SacredItem", "Священный предмет", "StabilizeUp", "Medium", false, false, "Хорошие значения и стабилизация результата."),
            Def(3, "Предметы", "CursedItem", "Проклятый предмет", "BiasDown", "Medium", false, false, "Чаще плохие значения."),
            Def(3, "Предметы", "DoomedItem", "Предмет обречённости", "BiasDown", "Strong", false, false, "Сильно тянет результат вниз."),
            Def(3, "Предметы", "AnomalousItem", "Аномальный предмет", "Anomaly", "Strong", false, true, "Даёт большой случайный сдвиг и может вывести результат за границы кубика."),
            Def(3, "Предметы", "ChaosItem", "Предмет хаоса", "Chaos", "Strong", true, false, "Делает результат непредсказуемым и хаотичным."),
            Def(3, "Предметы", "StableItem", "Стабильный предмет", "PullToMiddle", "Medium", false, false, "Убирает крайности и тянет результат к середине."),
            Def(3, "Предметы", "RingOfStability", "Кольцо стабильности", "RemoveCriticals", "Special", false, false, "Критическая неудача невозможна, но невозможной становится и критическая удача."),
            Def(3, "Предметы", "CursedSword", "Проклятый меч", "WorstOfTwo", "Special", false, false, "Верхний потолок выше, но бросаются два значения и выбирается худшее."),
            Def(3, "Предметы", "MasterworkTool", "Мастерский инструмент", "BiasUp", "Weak", false, false, "Чаще высокие значения без аномальных эффектов."),
            Def(3, "Предметы", "BrokenTool", "Сломанный инструмент", "BiasDown", "Weak", false, false, "Чаще низкие значения."),
            Def(3, "Предметы", "UnstableWeapon", "Нестабильное оружие", "PullToExtreme", "Medium", true, false, "Чаще крайние значения."),
            Def(3, "Предметы", "ResonantArtifact", "Резонирующий артефакт", "AmplifyDirection", "Medium", false, false, "Усиливает уже возникшее направление результата."),
            Def(3, "Предметы", "HolyRelic", "Святая реликвия", "SuppressChaosAndBiasUp", "Strong", false, false, "Подавляет хаос или проклятие и тянет результат вверх."),
            Def(3, "Предметы", "ProfaneRelic", "Скверная реликвия", "AmplifyChaosAndBiasDown", "Strong", true, false, "Усиливает хаос или проклятие и тянет результат вниз."),

            // Layer 4: Психология
            Def(4, "Психология", "Calm", "Спокойствие", "Stabilize", "Weak", false, false, "Стабилизирует результат."),
            Def(4, "Психология", "Focused", "Фокус", "StabilizeUp", "Medium", false, false, "Чаще хорошие и средне-хорошие значения."),
            Def(4, "Психология", "Inspired", "Воодушевление", "BiasUp", "Medium", false, false, "Чаще высокие значения."),
            Def(4, "Психология", "Confused", "Замешательство", "Destabilize", "Weak", false, false, "Слегка хаотизирует результат."),
            Def(4, "Психология", "Afraid", "Страх", "BiasDown", "Medium", false, false, "Чаще низкие значения."),
            Def(4, "Психология", "Panic", "Паника", "BiasDownAndExtreme", "Medium", false, false, "Низкие и крайние значения становятся чаще."),
            Def(4, "Психология", "Rage", "Ярость", "PullToExtreme", "Medium", false, false, "Значения ближе к краевым."),
            Def(4, "Психология", "Despair", "Отчаяние", "BiasDownAndExtreme", "Strong", false, false, "Чаще низкие или крайние значения."),
            Def(4, "Психология", "Determination", "Решимость", "StabilizeUp", "Medium", false, false, "Стабилизирует результат вверх."),
            Def(4, "Психология", "Overconfidence", "Самоуверенность", "BiasUpAndExtreme", "Medium", false, false, "Может тянуть вверх, но повышает риск крайностей."),
            Def(4, "Психология", "BrokenMorale", "Сломленная мораль", "BiasDown", "Strong", false, false, "Сильно тянет результат вниз."),
            Def(4, "Психология", "BattleTrance", "Боевой транс", "PullToExtreme", "Medium", false, false, "Уменьшает средние значения, чаще крайние."),
            Def(4, "Психология", "Stress", "Стресс", "Destabilize", "Medium", false, false, "Дестабилизирует результат."),
            Def(4, "Психология", "ColdBlood", "Хладнокровие", "Stabilize", "Medium", false, false, "Убирает часть хаоса и тянет к стабильному результату."),

            // Layer 5: Шкала уверенности
            Def(5, "Шкала уверенности", "Empty", "Пустая шкала", "None", "None", false, false, "0% срабатывания."),
            Def(5, "Шкала уверенности", "SlightGoodStreak", "Лёгкая удачная серия", "ConfidenceCorrectionDown", "Weak", false, false, "Малый шанс ухудшить следующий результат."),
            Def(5, "Шкала уверенности", "GoodStreak", "Удачная серия", "ConfidenceCorrectionDown", "Medium", false, false, "Средний шанс ухудшить следующий результат."),
            Def(5, "Шкала уверенности", "ExtremeGoodStreak", "Сильная удачная серия", "ConfidenceCorrectionDown", "Strong", false, false, "Высокий шанс ухудшить следующий результат."),
            Def(5, "Шкала уверенности", "SlightBadStreak", "Лёгкая плохая серия", "ConfidenceCorrectionUp", "Weak", false, false, "Малый шанс улучшить следующий результат."),
            Def(5, "Шкала уверенности", "BadStreak", "Плохая серия", "ConfidenceCorrectionUp", "Medium", false, false, "Средний шанс улучшить следующий результат."),
            Def(5, "Шкала уверенности", "ExtremeBadStreak", "Сильная плохая серия", "ConfidenceCorrectionUp", "Strong", false, false, "Высокий шанс улучшить следующий результат."),
            Def(5, "Шкала уверенности", "Balanced", "Баланс", "None", "None", false, false, "Шкала почти не вмешивается.")
        };

        return items;
    }

    private static FateLayerEffectDefinition Def(
        int layerNumber,
        string layerName,
        string code,
        string displayName,
        string influenceType,
        string strength,
        bool canUseChaos,
        bool canUseAnomaly,
        string description)
    {
        return new FateLayerEffectDefinition
        {
            LayerNumber = layerNumber,
            LayerName = layerName,
            EffectCode = code,
            DisplayName = displayName,
            InfluenceType = influenceType,
            Strength = strength,
            CanUseChaos = canUseChaos,
            CanUseAnomaly = canUseAnomaly,
            Description = description
        };
    }
}
