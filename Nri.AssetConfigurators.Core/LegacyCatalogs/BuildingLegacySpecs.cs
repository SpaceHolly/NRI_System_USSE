using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nri.AssetConfigurators.Core.LegacyCatalogs
{
    internal static class BuildingLegacySpecs
    {
        public static List<string> BuildingTypes { get; } = new List<string>
        {"Наземное", "Бункер", "Надводное", "Подводное", "Атмосферное", "Космическое"};

        public static List<string> BuildingMethodsTypes { get; } = new List<string>
        {"Собств.силами", "Найм строител.", "Подрядчики"};

        public static Dictionary<string, int> FloorSize { get; } = new Dictionary<string, int>
        {
            { "C", 1 },
            { "XSS", 2 },
            { "SSS", 4 },
            { "SS", 8 },
            { "S", 16 },
            { "M", 32 },
            { "L", 64 },
            { "VL", 128 },
            { "A", 256 },
            { "X", 512 },
            { "XL", 1024 },
            { "XXL", 2048 },
            { "XXXL", 4096 },
            { "E", 8182 },
            { "XE", 16364 }
        };

        public static Dictionary<string, int> BuildingSizeWeaponsCount { get; } = new Dictionary<string, int>
        {
            { "C", 1 },
            { "XSS", 2 },
            { "SSS", 6 },
            { "SS", 8 },
            { "S", 10 },
            { "M", 12 },
            { "L", 14 },
            { "VL", 16 },
            { "A", 18 },
            { "X", 20 },
            { "XL", 22 },
            { "XXL", 24 },
            { "XXXL", 26 },
            { "E", 28 },
            { "XE", 30 }
        };

        public static Dictionary<string, double> HPResourcesTypes { get; } = new Dictionary<string, double>
        {
            { "Ст.металлы", 0.5 },
            { "Структурий", 1 },
            { "Бориформий", 2 }
        };

        public static Dictionary<string, int> APResourcesTypes { get; } = new Dictionary<string, int>
        {
            { "Нет", 0 },
            { "Арморий", 1 },
            { "Сталиниум", 2 }
        };

        public static Dictionary<string, int> SPResourcesTypes { get; } = new Dictionary<string, int>
        {
            { "Нет", 0 },
            { "Хассатий", 1 },
            { "Хассатий-Б", 2 }
        };

        public static Dictionary<string, int> OtherResourcesTypes { get; } = new Dictionary<string, int>
        {
            { "Инерт.газы", 1 }
        };

        public static Dictionary<string, double> ResourcesCost { get; } = new Dictionary<string, double>
        {
            { "Станд.металлы", 0.7 },
            { "Структурий", 4.4 },
            { "Арморий", 5.8 },
            { "Хассатий", 17.1 },
            { "Бориформий", 44.7 },
            { "Сталиниум", 51.2 },
            { "Хассатий-Б", 48.5 },
            { "Инерт.газы", 1.1 }
        };

        public static Dictionary<string, double> QualityCost { get; } = new Dictionary<string, double>
        {
            { "Ужасное", 0.25 },
            { "Плохое", 0.5 },
            { "Стандартное", 1.0 },
            { "Качественное", 1.25 },
            { "Надёжное", 1.5 }
        };

        public static Dictionary<string, int> LevelMultiplier { get; } = new Dictionary<string, int>
        {
            { "1 Ур.", 1 },
            { "2 Ур.", 3 },
            { "3 Ур.", 9 },
            { "4 Ур.", 27 }
        };

        public static Dictionary<string, (int RPower, int RCost)> ReatorPowerAndCost { get; } = new Dictionary<string, (int, int)>
        {
            { "Газовый", (50, 1000)},
            { "Ядерный", (240, 24000)},
            { "Факелевый", (700, 77800)}
        };

        public static Dictionary<string, (int Cost, bool MediumArm, bool HeavyArm, bool ExtremeArm)> Weapons { get; } = new Dictionary<string, (int, bool, bool, bool)>
        {
            {"SGS-30 - Small Gun System", (10500, false, false, false)},
            {"BGS-127 - Basic Gun System", (3500, false, false, false)},
            {"BMS-D - Bolter Mounted System", (15500, false, false, false)},
            {"Лёгк. ракетная уст.", (12590, false, false, false)},
            {"Сред. ракетная уст.", (22350, true, false, false)},
            {"Тяж. ракетная уст.", (45850, true, true, false)},
            {"Водная Торпедная уст.", (35750, true, true, false)},
            {"MAC-152M - Medium Artillery Cannon", (55000, true, false, false)},
            {"BRG-90X - Battle Rail Gun", (68650, true, true, true)},
            {"HBC-305A - Heavy Battle Cannon", (68650, true, true, false)},
            {"TAC-75B - Tactical Auto Cannon", (19650, true, false, false)},
            {"MLP-55B - Multi-purpose Laser", (35000, false, false, false)},
            {"HPC-100 – High-Intensity Plasma Cannon", (45000, true, false, false)},
            {"IPC-70 – Ion Precision Cannon", (75000, true, false, false)},
            {"Репульсор", (82500, true, true, true)},
            {"LPI-36 - Light Photon Impulsor", (72250, true, false, false)},
            {"PXL-72 - Photon X-ray Laser", (95000, true, true, false)},
            {"IPL-150 – Impulse Photon Howitzer", (120000, true, true, true)},
            {"PLB-105 – Plasma Lance Battery", (50000, true, false, false)},
            {"TDC-Ω – Tesla Discharge Cannon", (76500, true, false, false)},
            {"TLC-500 – Tesla Lightning Cannon", (77800, true, true, false)},
            {"Гравицапа 1990", (83550, true, true, false)},
            {"IGG-620 – Ionized Gatling Gun", (17250, false, false, false)},
            {"MRL-07B - Miniature Rail Launcher", (22250, true, false, false)},
            {"TRG-42M - Tactical Rail Gun", (35500, true, false, false)},
            {"TLA-60 – Tactical Laser Accumulator", (35000, false, false, false)},
            {"PLD-170 – Plasma Lance Disruptor", (33225, true, false, false)},
            {"TIB-76HX – Tactical Ion Bolter", (51685, true, true, false)},
            {"WH-40k - War Hammer", (89950, true, true, true)}
        };

        public static Dictionary<string, (int WeponSize, int WeaponConsumtion)> WeaponsSizeAndConsumtion { get; } = new Dictionary<string, (int, int)>
        {
            {"SGS-30 - Small Gun System", (1, 0)},
            {"BGS-127 - Basic Gun System", (1, 0)},
            {"BMS-D - Bolter Mounted System", (2, 0)},
            {"Лёгк. ракетная уст.", (1, 0)},
            {"Сред. ракетная уст.", (2, 0)},
            {"Тяж. ракетная уст.", (4, 0)},
            {"Водная Торпедная уст.", (5, 0)},
            {"MAC-152M - Medium Artillery Cannon", (3, 0)},
            {"BRG-90X - Battle Rail Gun", (5, 4)},
            {"HBC-305A - Heavy Battle Cannon", (3, 0)},
            {"TAC-75B - Tactical Auto Cannon", (1, 0)},
            {"MLP-55B - Multi-purpose Laser", (1, 4)},
            {"HPC-100 – High-Intensity Plasma Cannon", (3, 4)},
            {"IPC-70 – Ion Precision Cannon", (2, 4)},
            {"Репульсор", (1, 1)},
            {"LPI-36 - Light Photon Impulsor", (2, 3)},
            {"PXL-72 - Photon X-ray Laser", (4, 3)},
            {"IPL-150 – Impulse Photon Howitzer", (6, 3)},
            {"PLB-105 – Plasma Lance Battery", (2, 3)},
            {"TDC-Ω – Tesla Discharge Cannon", (1, 4)},
            {"TLC-500 – Tesla Lightning Cannon", (2, 4)},
            {"Гравицапа 1990", (2, 4)},
            {"IGG-620 – Ionized Gatling Gun", (1, 1)},
            {"MRL-07B - Miniature Rail Launcher", (2, 2)},
            {"TRG-42M - Tactical Rail Gun", (2, 2)},
            {"TLA-60 – Tactical Laser Accumulator", (1, 3)},
            {"PLD-170 – Plasma Lance Disruptor", (2, 3)},
            {"TIB-76HX – Tactical Ion Bolter", (2, 2)},
            {"WH-40k - War Hammer", (9, 5)}
        };

        public static Dictionary<string, int> CellCosts { get; } = new Dictionary<string, int>
        {
            { "Склад общий", 2500 },
            { "Склад медицины", 3500 },
            { "Склад боеприпасов", 3500 },
            { "Склад топлива", 2500 },
            { "Комната на 2 человека", 3850 },
            { "Кубрик на 6 человек", 4300 },
            { "Казарма на 20 человек", 6800 },
            { "Лаборатория", 32000 },
            { "СТО Наземной техники", 25000 },
            { "СТО Снаряжения", 10000 },
            { "Верфь", 100000 },
            { "Очиститель руды", 12500 },
            { "Очиститель лома", 7500 },
            { "Очиститель электроники", 23000 },
            { "Очиститель органики", 15000 },
            { "Очиститель химии", 28000 },
            { "Ангар для челноков", 5000 },
            { "Суперкомпьютер", 255000 },
            { "Медблок", 13900 },
            { "Тюрьма", 4500 },
            { "Учебный центр", 12000 },
            { "Мастерская", 9900 },
            { "Лавка/магазин", 8550 },
            { "Бар", 9800 },
            { "Мотель", 6600 },
            { "Часовня/церковь", 1000 },
            { "Бордель", 16000 },
            { "Кафе", 5700 },
            { "Казино", 26500 },
            { "Зал боевой подготовки", 12250 },
            { "Актовый зал", 1500 },
            { "Переговорная комната", 2500 },
            { "Центр связи", 13500 },
            { "Комната крио-анабиоза", 4700 },
            { "3D-принтер", 50000 },
            { "Модуль глубокого сканирования", 50000 },
            { "Мануфакторум", 75600 },
            { "Ангар общего назначения", 56700 },
            { "Модуль радио-оптич. маскировки", 125000 },
            { "Модуль магнитной маскировки", 100000 },
            { "Дэмпер щитов", 2500000 },
            { "Усилитель щита", 372000 },
            { "Усилитель брони", 256000 },
            { "Тактический центр", 45600 },
            { "Модуль антиматерии", 126500 },
            { "Сенсорный массив", 66000 },
            { "Клетка Фарадея", 136900 }
        };

        public static readonly Dictionary<string, int> EnergyCostByItem = new Dictionary<string, int>
        {
            {"Склад общий", 0},
            {"Склад медицины", 0},
            {"Склад боеприпасов", 0},
            {"Склад топлива", 0},
            {"Комната на 2 человека", 1},
            {"Кубрик на 6 человек", 1},
            {"Казарма на 20 человек", 2},
            {"Лаборатория", 7},
            {"СТО Наземной техники", 5},
            {"СТО Снаряжения", 3},
            {"Верфь", 25},
            {"Очиститель руды", 5},
            {"Очиститель лома", 4},
            {"Очиститель электроники", 5},
            {"Очиститель органики", 3},
            {"Очиститель химии", 6},
            {"Ангар для челноков", 10},
            {"Суперкомпьютер", 5},
            {"Медблок", 2},
            {"Тюрьма", 1},
            {"Учебный центр", 1},
            {"Мастерская", 3},
            {"Лавка/магазин", 1},
            {"Бар", 1},
            {"Мотель", 1},
            {"Часовня/церковь", 0},
            {"Бордель", 1},
            {"Кафе", 1},
            {"Казино", 3},
            {"Зал боевой подготовки", 2},
            {"Актовый зал", 1},
            {"Переговорная комната", 1},
            {"Центр связи", 4},
            {"Комната крио-анабиоза", 8},
            {"3D-принтер", 4},
            {"Модуль глубокого сканирования", 2},
            {"Мануфакторум", 15},
            {"Модуль радио-оптич. маскировки", 15},
            {"Модуль магнитной маскировки", 5},
            {"Ангар общего назначения", 10},
            {"Дэмпер щитов", 7},
            {"Усилитель щита", 9},
            {"Усилитель брони", 3},
            {"Тактический центр", 3},
            {"Модуль антиматерии", 19},
            {"Сенсорный массив", 7},
            {"Клетка Фарадея", 0}
        };

        public static readonly Dictionary<string, double> LaborFactor = new Dictionary<string, double>
        {
            ["Собств.силами"] = 0.0,
            ["Найм строител."] = 0.25,
            ["Подрядчики"] = 0.10
        };

        public static readonly Dictionary<string, double> Thickness = new Dictionary<string, double>
        {
            ["structural"] = 0.2,
            ["armor"] = 0.4,
            ["shield"] = 0.001
        };

        public const double FloorHeight = 5.0;

        public const double UsdPerAr = 20.89;
    }
}
