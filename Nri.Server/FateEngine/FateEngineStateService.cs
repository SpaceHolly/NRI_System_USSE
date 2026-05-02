using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Server.FateEngine;

public sealed class FateEngineStateService
{
    private readonly object _sync = new object();
    private readonly Guid _instanceId = Guid.NewGuid();
    private FateEngineSettings _current = FateEngineSettings.CreateDefault().Normalize();
    public Guid InstanceId => _instanceId;

    public FateEngineSettings GetSnapshot()
    {
        lock (_sync)
        {
            return Clone(_current);
        }
    }

    public FateEngineSettings Update(FateEngineSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        lock (_sync)
        {
            _current = Clone(settings).Normalize();
            return Clone(_current);
        }
    }

    public FateEngineSettings ResetToDefault()
    {
        lock (_sync)
        {
            _current = FateEngineSettings.CreateDefault().Normalize();
            return Clone(_current);
        }
    }

    public FateEngineSettings SetEngineEnabled(bool enabled)
    {
        lock (_sync)
        {
            _current.Enabled = enabled;
            _current.Normalize();
            return Clone(_current);
        }
    }

    public FateEngineSettings SetLayerEnabled(int layerNumber, bool enabled)
    {
        lock (_sync)
        {
            var layer = RequireLayer(layerNumber);
            layer.Enabled = enabled;
            _current.Normalize();
            return Clone(_current);
        }
    }

    public FateEngineSettings SetLayerFlatModifier(int layerNumber, int modifier)
    {
        lock (_sync)
        {
            var layer = RequireLayer(layerNumber);
            layer.FlatModifier = modifier;
            _current.Normalize();
            return Clone(_current);
        }
    }

    private FateLayerSettings RequireLayer(int layerNumber)
    {
        if (layerNumber < 1 || layerNumber > FateEngineSettings.LayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(layerNumber), $"layerNumber must be 1..{FateEngineSettings.LayerCount}.");
        }

        _current.Normalize();
        return _current.Layers[layerNumber - 1];
    }

    private static FateEngineSettings Clone(FateEngineSettings source)
    {
        return new FateEngineSettings
        {
            Enabled = source.Enabled,
            Layers = source.Layers.Select(CloneLayer).ToList()
        }.Normalize();
    }

    private static FateLayerSettings CloneLayer(FateLayerSettings layer)
    {
        return new FateLayerSettings
        {
            LayerNumber = layer.LayerNumber,
            DisplayName = layer.DisplayName,
            Enabled = layer.Enabled,
            Intensity = layer.Intensity,
            Mode = layer.Mode,
            FlatModifier = layer.FlatModifier,
            EffectCode = layer.EffectCode
        };
    }
}
