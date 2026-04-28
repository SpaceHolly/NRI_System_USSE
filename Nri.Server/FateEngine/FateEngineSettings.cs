using System.Collections.Generic;
using System.Linq;

namespace Nri.Server.FateEngine;

public sealed class FateEngineSettings
{
    public const int LayerCount = 5;

    public bool Enabled { get; set; } = true;
    public List<FateLayerSettings> Layers { get; set; } = CreateDefaultLayers();

    public FateEngineSettings Normalize()
    {
        Layers ??= new List<FateLayerSettings>();

        var normalized = new FateLayerSettings[LayerCount];

        foreach (var layer in Layers)
        {
            if (layer.LayerNumber < 1 || layer.LayerNumber > LayerCount)
            {
                continue;
            }

            normalized[layer.LayerNumber - 1] = Clone(layer);
        }

        for (var i = 0; i < LayerCount; i++)
        {
            normalized[i] ??= CreateDefaultLayer(i + 1);
        }

        Layers = normalized.ToList();
        return this;
    }

    public static FateEngineSettings CreateDefault()
    {
        return new FateEngineSettings
        {
            Enabled = true,
            Layers = CreateDefaultLayers()
        };
    }

    private static List<FateLayerSettings> CreateDefaultLayers()
    {
        var layers = new List<FateLayerSettings>(LayerCount);
        for (var i = 1; i <= LayerCount; i++)
        {
            layers.Add(CreateDefaultLayer(i));
        }

        return layers;
    }

    private static FateLayerSettings CreateDefaultLayer(int layerNumber)
    {
        return new FateLayerSettings
        {
            LayerNumber = layerNumber,
            DisplayName = GetDefaultLayerName(layerNumber),
            Enabled = true,
            Intensity = 0,
            Mode = "flat",
            FlatModifier = 0,
            EffectCode = "None"
        };
    }

    private static string GetDefaultLayerName(int layerNumber)
    {
        return layerNumber switch
        {
            1 => "Местность",
            2 => "Эффекты персонажа",
            3 => "Предметы",
            4 => "Психология",
            5 => "Шкала уверенности",
            _ => $"Layer {layerNumber}"
        };
    }

    private static FateLayerSettings Clone(FateLayerSettings layer)
    {
        return new FateLayerSettings
        {
            LayerNumber = layer.LayerNumber,
            DisplayName = string.IsNullOrWhiteSpace(layer.DisplayName) ? GetDefaultLayerName(layer.LayerNumber) : layer.DisplayName,
            Enabled = layer.Enabled,
            Intensity = layer.Intensity,
            Mode = string.IsNullOrWhiteSpace(layer.Mode) ? "flat" : layer.Mode,
            FlatModifier = layer.FlatModifier,
            EffectCode = string.IsNullOrWhiteSpace(layer.EffectCode) ? "None" : layer.EffectCode
        };
    }
}
