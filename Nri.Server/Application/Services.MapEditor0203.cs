using System;
using System.Collections.Generic;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope MapEditorAdminGetState0203(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapEditor0203Enabled()) return MapEditor0203Disabled(context.Request.Command);
        try
        {
            var mapId = PayloadReader.GetString(context.Request.Payload, "mapId") ?? PayloadReader.GetString(context.Request.Payload, "sceneMapId") ?? string.Empty;
            var state = _mapEditorMutationService.GetState(mapId);
            return Ok("Состояние редактора карты загружено.", new Dictionary<string, object>
            {
                ["mapId"] = state.CanonicalMapId,
                ["mapRevision"] = state.MapRevision,
                ["widthMeters"] = state.WidthMeters,
                ["heightMeters"] = state.HeightMeters,
                ["gridCellSizeMeters"] = state.GridCellSizeMeters,
                ["layers"] = state.Layers,
                ["objects"] = state.Objects
            });
        }
        catch (MapEditorMutationException0203 ex)
        {
            return MapEditor0203Error(ex);
        }
    }

    public ResponseEnvelope MapEditorAdminMutate0203(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapEditor0203Enabled()) return MapEditor0203Disabled(context.Request.Command);
        try
        {
            var payload = context.Request.Payload ?? new Dictionary<string, object>();
            // Wire clients may deserialize nested JSON objects as non-generic dictionaries.
            // Normalize through the shared reader so the command contract is transport-neutral.
            var values = PayloadReader.GetDictionary(payload, "values")
                         ?? new Dictionary<string, object>(payload, StringComparer.OrdinalIgnoreCase);
            var request = new MapEditorMutationRequest0203
            {
                OperationId = PayloadReader.GetString(payload, "operationId") ?? string.Empty,
                MapId = PayloadReader.GetString(payload, "mapId") ?? PayloadReader.GetString(payload, "sceneMapId") ?? string.Empty,
                Mutation = PayloadReader.GetString(payload, "mutation") ?? string.Empty,
                TargetId = PayloadReader.GetString(payload, "targetId") ?? string.Empty,
                LayerId = PayloadReader.GetString(payload, "layerId") ?? string.Empty,
                ExpectedMapRevision = PayloadReader.GetLong(payload, "expectedMapRevision") ?? 0L,
                ExpectedLayerRevision = PayloadReader.GetLong(payload, "expectedLayerRevision"),
                ExpectedObjectRevision = PayloadReader.GetLong(payload, "expectedObjectRevision"),
                ActorUserId = actor.Id,
                Values = values
            };
            _logger.Admin($"map.editor.0203.mutate.start user={actor.Login} mutation={request.Mutation} map={request.MapId} operation={request.OperationId}");
            var result = _mapEditorMutationService.Mutate(request);
            _logger.Admin($"map.editor.0203.mutate.done user={actor.Login} mutation={result.Mutation} map={result.CanonicalMapId} target={result.TargetId} revision={result.MapRevision} replay={result.IsReplay}");
            return Ok(result.IsReplay ? "Операция уже была применена." : "Изменение карты сохранено.", new Dictionary<string, object>
            {
                ["operationId"] = request.OperationId,
                ["isReplay"] = result.IsReplay,
                ["mapId"] = result.CanonicalMapId,
                ["mutation"] = result.Mutation,
                ["targetId"] = result.TargetId,
                ["mapRevision"] = result.MapRevision,
                ["layerRevision"] = result.LayerRevision,
                ["objectRevision"] = result.ObjectRevision,
                ["target"] = result.Target
            });
        }
        catch (MapEditorMutationException0203 ex)
        {
            _logger.Admin($"map.editor.0203.mutate.rejected kind={ex.Kind} message={ex.Message}");
            return MapEditor0203Error(ex);
        }
    }

    private bool MapEditor0203Enabled()
        => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapV1));

    private ResponseEnvelope MapEditor0203Disabled(string command)
    {
        _logger.Admin($"map.editor.0203.disabled command={command}");
        return Error("Редактор карты выключен feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    public ResponseEnvelope MapEditorLegacyWriteRejected0203(CommandContext context)
    {
        RequireAdmin(context);
        _logger.Admin($"map.editor.0203.legacy_write_rejected command={context.Request.Command}");
        return Error("Команда устарела. Перезагрузите редактор карты и повторите действие через canonical editor.",
            ResponseStatus.Conflict, ErrorCode.Conflict);
    }

    private static ResponseEnvelope MapEditor0203Error(MapEditorMutationException0203 exception)
    {
        if (exception.Kind == "not_found") return Error(exception.Message, ResponseStatus.NotFound, ErrorCode.NotFound);
        if (exception.Kind == "conflict" || exception.Kind == "locked") return Error(exception.Message, ResponseStatus.Conflict, ErrorCode.Conflict);
        return Error(exception.Message, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
    }
}
