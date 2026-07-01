using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string AudioDefaultSessionId = "default";
    private const string AudioDefaultCampaignId = "dev-campaign-core";
    private static readonly string[] SupportedAudioExtensions = { ".mp3", ".wav", ".ogg" };

    public ResponseEnvelope AudioStateGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var sessionId = ReadAudioSessionId(context.Request.Payload);
        var state = EnsureAudioState(sessionId, null);
        return Ok("Audio state loaded.", IsAdminActor(actor) ? AudioStatePayload(state, playerSafe: false) : AudioPlayerStatePayload(state));
    }

    public ResponseEnvelope AudioStateSync(CommandContext context) => AudioStateGet(context);

    public ResponseEnvelope AudioModeGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var sessionId = ReadAudioSessionId(context.Request.Payload);
        var state = EnsureAudioState(sessionId, null);
        return Ok("Audio mode loaded.", IsAdminActor(actor) ? AudioStatePayload(state, playerSafe: false) : AudioPlayerStatePayload(state));
    }

    public ResponseEnvelope AudioModeSet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = ReadAudioSessionId(context.Request.Payload);
        var modeRaw = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "mode"), SessionAudioMode.Manual.ToString());
        var categoryRaw = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "category"), "custom");

        if (!Enum.TryParse(modeRaw, true, out SessionAudioMode mode)) return AudioValidation("Unsupported audio mode.");
        if (!TryNormalizeAudioCategory(categoryRaw, out var category)) return AudioValidation("Unsupported audio category.");

        var state = EnsureAudioState(sessionId, actor.Id);
        state.Mode = mode;
        state.OverrideEnabled = mode == SessionAudioMode.Manual;
        state.OverrideByUserId = state.OverrideEnabled ? actor.Id : string.Empty;
        state.CurrentCategory = state.OverrideEnabled ? category : DetermineAutoCategory(sessionId);
        var track = PickTrackForCategory(state.CurrentCategory, state, advance: false);
        if (track != null) SwitchToTrack(state, track, actor, "audio.mode.set", playing: true);
        TouchAudioState(state, actor);
        SaveAudioState(state);
        WriteAudioEvent(actor, state, "audio.state.category_changed", "Категория аудио изменена.", playerVisible: false);
        return Ok("Audio mode updated.", AudioStatePayload(state, playerSafe: false));
    }

    public ResponseEnvelope AudioOverrideClear(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = ReadAudioSessionId(context.Request.Payload);
        var state = EnsureAudioState(sessionId, actor.Id);
        state.Mode = SessionAudioMode.Auto;
        state.OverrideEnabled = false;
        state.OverrideByUserId = string.Empty;
        state.CurrentCategory = DetermineAutoCategory(sessionId);
        var track = PickTrackForCategory(state.CurrentCategory, state, advance: false);
        if (track != null) SwitchToTrack(state, track, actor, "audio.override.clear", playing: true);
        TouchAudioState(state, actor);
        SaveAudioState(state);
        WriteAudioEvent(actor, state, "audio.state.resynced", "Audio override cleared.", playerVisible: false);
        return Ok("Audio override cleared.", AudioStatePayload(state, playerSafe: false));
    }

    public ResponseEnvelope AudioLibraryGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureAudioLibraryLoaded();
        var admin = IsAdminActor(actor);
        var tracks = LoadEnabledAudioTracks(adminOnly: admin)
            .Select(x => (object)AudioTrackPayload(x, playerSafe: !admin))
            .ToArray();

        var payload = new Dictionary<string, object> { { "items", tracks } };
        if (admin) payload["root"] = _audioFolderPath;
        return Ok("Audio library loaded.", payload);
    }

    public ResponseEnvelope AudioTrackSelect(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = ReadAudioSessionId(context.Request.Payload);
        var trackId = RequireLength(PayloadReader.GetString(context.Request.Payload, "trackId"), 1, 128, "trackId");
        var playRequest = new Dictionary<string, object>(context.Request.Payload ?? new Dictionary<string, object>())
        {
            ["sessionId"] = sessionId,
            ["trackId"] = trackId
        };
        return PlayTrack(actor, playRequest, "audio.track.played");
    }

    public ResponseEnvelope AudioTrackNext(CommandContext context)
    {
        var actor = RequireAdmin(context);
        return NextTrack(actor, context.Request.Payload ?? new Dictionary<string, object>());
    }

    public ResponseEnvelope AudioTrackReload(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureAudioLibraryLoaded(force: true);
        WriteAudit("audio", actor.Id, "track.reload", "library");
        _logger.Admin($"audio.library.reload actor={actor.Login}");
        return Ok("Audio library reloaded.", new Dictionary<string, object> { { "items", LoadEnabledAudioTracks(adminOnly: true).Select(x => (object)AudioTrackPayload(x, false)).ToArray() } });
    }

    public ResponseEnvelope AudioClientSettingsGet(CommandContext context) => AudioPlayerClientSettingsGet(context);
    public ResponseEnvelope AudioClientSettingsSet(CommandContext context) => AudioPlayerClientSettingsUpdate(context);

    public ResponseEnvelope AudioPlayerStateGet(CommandContext context)
    {
        if (!AudioPlayerEnabled()) return AudioDisabled(context.Request.Command);
        GetCurrentAccount(context);
        var state = EnsureAudioState(ReadAudioSessionId(context.Request.Payload), null);
        return Ok("Player audio state loaded.", AudioPlayerStatePayload(state));
    }

    public ResponseEnvelope AudioPlayerTracksVisible(CommandContext context)
    {
        if (!AudioPlayerEnabled()) return AudioDisabled(context.Request.Command);
        GetCurrentAccount(context);
        EnsureAudioLibraryLoaded();
        var items = LoadEnabledAudioTracks(adminOnly: false)
            .Select(x => (object)AudioTrackPayload(x, playerSafe: true))
            .ToArray();
        return Ok("Player-visible audio tracks loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope AudioPlayerClientSettingsGet(CommandContext context)
    {
        if (!AudioBaseEnabled() && IsNewAudioCommand(context.Request.Command)) return AudioDisabled(context.Request.Command);
        var actor = GetCurrentAccount(context);
        var item = EnsureClientSettings(actor.Id, ReadAudioSessionId(context.Request.Payload));
        return Ok("Audio client settings loaded.", AudioClientSettingsPayload(item));
    }

    public ResponseEnvelope AudioPlayerClientSettingsUpdate(CommandContext context)
    {
        if (!AudioClientSettingsEnabled() && IsNewAudioCommand(context.Request.Command)) return AudioDisabled(context.Request.Command);
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var volume = PayloadReader.GetDouble(payload, "localVolume")
            ?? PayloadReader.GetDouble(payload, "volume")
            ?? 0.7;
        volume = NormalizeVolume(volume);
        var muted = payload.ContainsKey("isMuted")
            ? PayloadReader.GetBool(payload, "isMuted")
            : PayloadReader.GetBool(payload, "muted");
        var settings = EnsureClientSettings(actor.Id, ReadAudioSessionId(payload));
        settings.UserId = actor.Id;
        settings.Volume = volume;
        settings.LocalVolume = volume;
        settings.Muted = muted;
        settings.IsMuted = muted;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        settings.Revision += 1;
        _repositories.AudioClientSettings.Replace(settings);
        return Ok("Audio client settings updated.", AudioClientSettingsPayload(settings));
    }

    public ResponseEnvelope AudioAdminTracksList(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        RequireAdmin(context);
        EnsureAudioLibraryLoaded();
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var tracks = _repositories.AudioTracks.Find(FilterDefinition<AudioTrackDefinition>.Empty)
            .Where(x => includeArchived || (!x.Archived && !x.IsArchived))
            .OrderBy(x => CategorySort(x.Category)).ThenBy(x => x.SortOrder).ThenBy(x => x.DisplayName)
            .Select(x => (object)AudioTrackPayload(x, playerSafe: false))
            .ToArray();
        return Ok("Admin audio tracks loaded.", new Dictionary<string, object> { { "items", tracks } });
    }

    public ResponseEnvelope AudioAdminTracksCreateOrUpdate(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var trackId = FirstNonEmpty(PayloadReader.GetString(payload, "trackId"), PayloadReader.GetString(payload, "id"));
        var existing = string.IsNullOrWhiteSpace(trackId) ? null : FindAudioTrack(trackId);
        var track = existing ?? new AudioTrackDefinition { Id = string.IsNullOrWhiteSpace(trackId) ? Guid.NewGuid().ToString("N") : trackId, TrackId = trackId };

        var displayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), track.DisplayName, track.Id), 1, 256, "displayName");
        var categoryRaw = FirstNonEmpty(PayloadReader.GetString(payload, "category"), track.CategoryId, "custom");
        if (!TryNormalizeAudioCategory(categoryRaw, out var category)) return AudioValidation("Unsupported audio category.");
        track.DisplayName = displayName;
        track.TrackId = FirstNonEmpty(track.TrackId, track.Id);
        track.Category = category;
        track.CategoryId = ToAudioCategoryId(category);
        track.FileName = FirstNonEmpty(PayloadReader.GetString(payload, "fileName"), track.FileName, track.TrackId + ".wav");
        track.RelativePath = NormalizeAudioRelativePath(FirstNonEmpty(PayloadReader.GetString(payload, "relativePath"), PayloadReader.GetString(payload, "filePath"), track.RelativePath, track.FileName));
        track.FilePath = track.RelativePath;
        track.IsEnabled = !payload.ContainsKey("isEnabled") || PayloadReader.GetBool(payload, "isEnabled");
        track.IsPlayerVisible = payload.ContainsKey("isPlayerVisible") ? PayloadReader.GetBool(payload, "isPlayerVisible") : track.IsPlayerVisible;
        track.Visibility = NormalizeAudioVisibility(FirstNonEmpty(PayloadReader.GetString(payload, "visibility"), track.Visibility, track.IsPlayerVisible ? AudioVisibilityIds.PlayerVisible : AudioVisibilityIds.GMOnly));
        track.IsArchived = PayloadReader.GetBool(payload, "isArchived");
        track.Archived = track.IsArchived;
        track.UpdatedByUserId = actor.Id;
        track.UpdatedByDisplayName = actor.Login;
        track.UpdatedAtUtc = DateTime.UtcNow;
        track.Revision += 1;
        if (existing == null)
        {
            track.CreatedByUserId = actor.Id;
            track.CreatedByDisplayName = actor.Login;
            track.CreatedAtUtc = DateTime.UtcNow;
            _repositories.AudioTracks.Insert(track);
        }
        else
        {
            _repositories.AudioTracks.Replace(track);
        }

        return Ok("Audio track saved.", new Dictionary<string, object> { { "item", AudioTrackPayload(track, playerSafe: false) } });
    }

    public ResponseEnvelope AudioAdminStateGet(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        RequireAdmin(context);
        var state = EnsureAudioState(ReadAudioSessionId(context.Request.Payload), null);
        return Ok("Admin audio state loaded.", AudioStatePayload(state, playerSafe: false));
    }

    public ResponseEnvelope AudioAdminStatePlay(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        var actor = RequireAdmin(context);
        return PlayTrack(actor, context.Request.Payload ?? new Dictionary<string, object>(), "audio.track.played");
    }

    public ResponseEnvelope AudioAdminStatePause(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        var actor = RequireAdmin(context);
        var state = EnsureAudioState(ReadAudioSessionId(context.Request.Payload), actor.Id);
        state.PositionSeconds = ResolvePositionSeconds(state);
        state.StartOffsetSeconds = state.PositionSeconds;
        state.PausedAtUtc = DateTime.UtcNow;
        state.PlaybackState = AudioPlaybackState.Paused;
        state.PlaybackStateId = "paused";
        TouchAudioState(state, actor);
        SaveAudioState(state);
        WriteAudioEvent(actor, state, "audio.state.paused", "Аудио поставлено на паузу.", playerVisible: false);
        return Ok("Audio paused.", AudioStatePayload(state, playerSafe: false));
    }

    public ResponseEnvelope AudioAdminStateStop(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        var actor = RequireAdmin(context);
        var state = EnsureAudioState(ReadAudioSessionId(context.Request.Payload), actor.Id);
        state.PositionSeconds = 0;
        state.StartOffsetSeconds = 0;
        state.PausedAtUtc = null;
        state.PlaybackState = AudioPlaybackState.Stopped;
        state.PlaybackStateId = "stopped";
        TouchAudioState(state, actor);
        SaveAudioState(state);
        WriteAudioEvent(actor, state, "audio.state.stopped", "Аудио остановлено.", playerVisible: false);
        return Ok("Audio stopped.", AudioStatePayload(state, playerSafe: false));
    }

    public ResponseEnvelope AudioAdminStateNext(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        var actor = RequireAdmin(context);
        return NextTrack(actor, context.Request.Payload ?? new Dictionary<string, object>());
    }

    public ResponseEnvelope AudioAdminStateSetCategory(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (!TryNormalizeAudioCategory(FirstNonEmpty(PayloadReader.GetString(payload, "category"), "custom"), out var category)) return AudioValidation("Unsupported audio category.");
        var state = EnsureAudioState(ReadAudioSessionId(payload), actor.Id);
        state.CurrentCategory = category;
        state.CurrentCategoryId = ToAudioCategoryId(category);
        state.Mode = SessionAudioMode.Manual;
        state.OverrideEnabled = true;
        state.OverrideByUserId = actor.Id;
        var track = PickTrackForCategory(category, state, advance: false);
        if (track != null) SwitchToTrack(state, track, actor, "audio.state.setCategory", playing: true);
        TouchAudioState(state, actor);
        SaveAudioState(state);
        WriteAudioEvent(actor, state, "audio.state.category_changed", "Категория аудио изменена.", playerVisible: false);
        return Ok("Audio category updated.", AudioStatePayload(state, playerSafe: false));
    }

    public ResponseEnvelope AudioAdminStateSetLoopMode(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var loopMode = NormalizeLoopMode(PayloadReader.GetString(payload, "loopMode"));
        if (loopMode == null) return AudioValidation("Unsupported audio loop mode.");
        var state = EnsureAudioState(ReadAudioSessionId(payload), actor.Id);
        state.LoopMode = loopMode;
        TouchAudioState(state, actor);
        SaveAudioState(state);
        return Ok("Audio loop mode updated.", AudioStatePayload(state, playerSafe: false));
    }

    public ResponseEnvelope AudioAdminStateSetFade(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var fade = PayloadReader.GetDouble(payload, "fadeSeconds") ?? 1.8;
        if (fade < 0 || fade > 30) return AudioValidation("fadeSeconds must be between 0 and 30.");
        var state = EnsureAudioState(ReadAudioSessionId(payload), actor.Id);
        state.FadeSeconds = fade;
        state.FadeMilliseconds = (int)Math.Round(fade * 1000.0);
        TouchAudioState(state, actor);
        SaveAudioState(state);
        return Ok("Audio fade updated.", AudioStatePayload(state, playerSafe: false));
    }

    public ResponseEnvelope AudioAdminStateResync(CommandContext context)
    {
        if (!AudioAdminEnabled()) return AudioDisabled(context.Request.Command);
        var actor = RequireAdmin(context);
        var state = EnsureAudioState(ReadAudioSessionId(context.Request.Payload), actor.Id);
        TouchAudioState(state, actor);
        SaveAudioState(state);
        WriteAudioEvent(actor, state, "audio.state.resynced", "Аудио синхронизировано.", playerVisible: false);
        return Ok("Audio state resynced.", AudioStatePayload(state, playerSafe: false));
    }

    private ResponseEnvelope PlayTrack(UserAccount actor, IDictionary<string, object> payload, string eventType)
    {
        var sessionId = ReadAudioSessionId(payload);
        EnsureAudioLibraryLoaded();
        var trackId = FirstNonEmpty(PayloadReader.GetString(payload, "trackId"), PayloadReader.GetString(payload, "id"));
        AudioTrackDefinition? track = null;
        if (!string.IsNullOrWhiteSpace(trackId))
        {
            track = FindAudioTrack(trackId);
            if (track == null) return AudioValidation("Audio track not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        }
        else if (TryNormalizeAudioCategory(FirstNonEmpty(PayloadReader.GetString(payload, "category"), "custom"), out var requestedCategory))
        {
            var stateForPick = EnsureAudioState(sessionId, actor.Id);
            track = PickTrackForCategory(requestedCategory, stateForPick, advance: false);
        }

        if (track == null) return AudioValidation("Audio track not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!track.IsEnabled || track.Archived || track.IsArchived) return AudioValidation("Audio track is disabled or archived.");

        var state = EnsureAudioState(sessionId, actor.Id);
        state.Mode = SessionAudioMode.Manual;
        state.OverrideEnabled = true;
        state.OverrideByUserId = actor.Id;
        state.CurrentCategory = track.Category;
        SwitchToTrack(state, track, actor, eventType, playing: true);
        TouchAudioState(state, actor);
        SaveAudioState(state);
        WriteAudioEvent(actor, state, eventType, $"Трек запущен: {track.DisplayName}.", playerVisible: IsTrackVisibleToPlayer(track));
        return Ok("Audio track started.", AudioStatePayload(state, playerSafe: false));
    }

    private ResponseEnvelope NextTrack(UserAccount actor, IDictionary<string, object> payload)
    {
        var state = EnsureAudioState(ReadAudioSessionId(payload), actor.Id);
        var currentCategory = state.CurrentCategory;
        var track = PickTrackForCategory(currentCategory, state, advance: true);
        if (track == null) return AudioValidation("No tracks in current category.");
        SwitchToTrack(state, track, actor, "audio.track.next", playing: true);
        TouchAudioState(state, actor);
        SaveAudioState(state);
        WriteAudioEvent(actor, state, "audio.track.next", $"Следующий трек: {track.DisplayName}.", playerVisible: IsTrackVisibleToPlayer(track));
        return Ok("Moved to next audio track.", AudioStatePayload(state, playerSafe: false));
    }

    private void SyncAudioPolicyForSession(string sessionId, string actorUserId)
    {
        try
        {
            EnsureAudioState(sessionId, actorUserId);
        }
        catch (Exception ex)
        {
            _logger.Debug("audio.sync.error session=" + sessionId + " error=" + ex.Message);
        }
    }

    private SessionAudioState EnsureAudioState(string sessionId, string? actorUserId)
    {
        EnsureAudioLibraryLoaded();
        var state = _repositories.AudioStates.Find(Builders<SessionAudioState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault();
        if (state == null)
        {
            state = new SessionAudioState
            {
                SessionId = sessionId,
                CampaignId = AudioDefaultCampaignId,
                Mode = SessionAudioMode.Manual,
                CurrentCategory = AudioCategory.Calm,
                CurrentCategoryId = ToAudioCategoryId(AudioCategory.Calm),
                PlaybackState = AudioPlaybackState.Stopped,
                PlaybackStateId = "stopped",
                StartedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow,
                FadeSeconds = 1.8,
                FadeMilliseconds = 1800,
                AutoNextMode = AudioAutoNextModeIds.Category,
                LoopMode = AudioLoopModeIds.None,
                VolumeMaster = 1.0
            };
            var first = PickTrackForCategory(state.CurrentCategory, state, false);
            if (first != null)
            {
                state.CurrentTrackId = first.Id;
                state.CurrentTrackPath = first.FilePath;
                state.CurrentTrackDisplayName = first.DisplayName;
            }
            _repositories.AudioStates.Insert(state);
            return state;
        }

        NormalizeAudioState(state);
        if (!state.OverrideEnabled && state.Mode == SessionAudioMode.Auto)
        {
            var autoCategory = DetermineAutoCategory(sessionId);
            if (state.CurrentCategory != autoCategory)
            {
                state.CurrentCategory = autoCategory;
                var next = PickTrackForCategory(autoCategory, state, advance: false);
                if (next != null) SwitchToTrack(state, next, actorUserId ?? "system", "audio.auto.categoryChange", playing: state.PlaybackState == AudioPlaybackState.Playing);
            }
        }

        var duration = ResolveCurrentTrackDuration(state);
        var elapsed = ResolvePositionSeconds(state);
        if (duration > 0 && elapsed >= duration && state.PlaybackState == AudioPlaybackState.Playing)
        {
            var next = PickTrackForCategory(state.CurrentCategory, state, advance: true);
            if (next != null) SwitchToTrack(state, next, actorUserId ?? "system", "audio.auto.nextAfterEnd", playing: true);
        }

        state.LastUpdatedUtc = DateTime.UtcNow;
        state.UpdatedAtUtc = state.LastUpdatedUtc;
        _repositories.AudioStates.Replace(state);
        return state;
    }

    private AudioCategory DetermineAutoCategory(string sessionId)
    {
        var combat = _repositories.Combats.Find(Builders<CombatState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault();
        if (combat != null && combat.Status == CombatStatus.Active) return AudioCategory.Battle;
        return AudioCategory.Calm;
    }

    private void EnsureAudioLibraryLoaded(bool force = false)
    {
        EnsureAcceptanceAudioFiles();

        var tracks = _repositories.AudioTracks.Find(FilterDefinition<AudioTrackDefinition>.Empty);
        EnsureAcceptanceAudioTracks();
        if (tracks.Count > 0 && !force) return;

        if (!Directory.Exists(_audioFolderPath))
        {
            Directory.CreateDirectory(_audioFolderPath);
            _logger.Debug($"Audio folder created: {_audioFolderPath}");
        }

        var files = Directory.GetFiles(_audioFolderPath, "*.*", SearchOption.AllDirectories)
            .Where(x => SupportedAudioExtensions.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase))
            .ToList();

        var existingByPath = _repositories.AudioTracks.Find(FilterDefinition<AudioTrackDefinition>.Empty)
            .ToDictionary(x => NormalizeAudioRelativePath(FirstNonEmpty(x.RelativePath, x.FilePath)), StringComparer.OrdinalIgnoreCase);

        var index = 0;
        foreach (var file in files)
        {
            var rel = MakeRelativePath(_audioFolderPath, file);
            var cat = InferCategory(file);
            if (existingByPath.TryGetValue(rel, out var ex))
            {
                ex.DisplayName = FirstNonEmpty(ex.DisplayName, Path.GetFileNameWithoutExtension(file));
                ex.FileName = Path.GetFileName(file);
                ex.RelativePath = rel;
                ex.FilePath = rel;
                ex.Category = cat;
                ex.CategoryId = ToAudioCategoryId(cat);
                ex.SortOrder = index++;
                ex.IsEnabled = true;
                ex.UpdatedAtUtc = DateTime.UtcNow;
                _repositories.AudioTracks.Replace(ex);
                continue;
            }

            var item = new AudioTrackDefinition
            {
                DisplayName = Path.GetFileNameWithoutExtension(file),
                FileName = Path.GetFileName(file),
                FilePath = rel,
                RelativePath = rel,
                Category = cat,
                CategoryId = ToAudioCategoryId(cat),
                DurationSeconds = 0,
                IsEnabled = true,
                IsPlayerVisible = true,
                Visibility = AudioVisibilityIds.PlayerVisible,
                SortOrder = index++,
                CreatedByUserId = "system",
                CreatedByDisplayName = "system",
                UpdatedByUserId = "system",
                UpdatedByDisplayName = "system",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            item.TrackId = item.Id;
            _repositories.AudioTracks.Insert(item);
        }
    }

    private void EnsureAcceptanceAudioTracks()
    {
        var tracks = new[]
        {
            AcceptanceTrack("calm_acceptance_01456", "Спокойная тема 0.14.56 PLAYER_VISIBLE_AUDIO_CALM_01456", AudioCategory.Calm, "acceptance_01456/calm_acceptance_01456.wav", true, AudioVisibilityIds.PlayerVisible, 10),
            AcceptanceTrack("battle_acceptance_01456", "Боевая тема 0.14.56 PLAYER_VISIBLE_AUDIO_BATTLE_01456", AudioCategory.Battle, "acceptance_01456/battle_acceptance_01456.wav", true, AudioVisibilityIds.PlayerVisible, 20),
            AcceptanceTrack("siege_acceptance_01456", "Осадная тема 0.14.56 PLAYER_VISIBLE_AUDIO_SIEGE_01456", AudioCategory.Siege, "acceptance_01456/siege_acceptance_01456.wav", true, AudioVisibilityIds.PlayerVisible, 30),
            AcceptanceTrack("gm_hidden_audio_01456", "GM_ONLY_AUDIO_TRACK_01456_DO_NOT_LEAK", AudioCategory.Custom, "acceptance_01456/gm_hidden_audio_01456.wav", false, AudioVisibilityIds.GMOnly, 40)
        };

        foreach (var item in tracks)
        {
            var existing = FindAudioTrack(item.Id) ?? _repositories.AudioTracks.Find(Builders<AudioTrackDefinition>.Filter.Eq(x => x.FilePath, item.FilePath)).FirstOrDefault();
            if (existing == null)
            {
                _repositories.AudioTracks.Insert(item);
                continue;
            }

            existing.TrackId = item.Id;
            existing.DisplayName = item.DisplayName;
            existing.FileName = item.FileName;
            existing.FilePath = item.FilePath;
            existing.RelativePath = item.RelativePath;
            existing.Category = item.Category;
            existing.CategoryId = item.CategoryId;
            existing.DurationSeconds = item.DurationSeconds;
            existing.IsEnabled = true;
            existing.IsPlayerVisible = item.IsPlayerVisible;
            existing.Visibility = item.Visibility;
            existing.Tags = item.Tags;
            existing.SortOrder = item.SortOrder;
            existing.IsArchived = false;
            existing.Archived = false;
            existing.UpdatedByUserId = "system";
            existing.UpdatedByDisplayName = "system";
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.Revision += 1;
            _repositories.AudioTracks.Replace(existing);
        }
    }

    private static AudioTrackDefinition AcceptanceTrack(string id, string displayName, AudioCategory category, string relativePath, bool playerVisible, string visibility, int sortOrder)
    {
        return new AudioTrackDefinition
        {
            Id = id,
            TrackId = id,
            DisplayName = displayName,
            FileName = Path.GetFileName(relativePath),
            FilePath = relativePath,
            RelativePath = relativePath,
            Category = category,
            CategoryId = ToAudioCategoryId(category),
            DurationSeconds = 60,
            LoopDefault = false,
            IsEnabled = true,
            IsPlayerVisible = playerVisible,
            Visibility = visibility,
            Tags = new List<string> { "0.14.56", ToAudioCategoryId(category), playerVisible ? "player_visible" : "gm_only" },
            CreatedByUserId = "system",
            CreatedByDisplayName = "system",
            UpdatedByUserId = "system",
            UpdatedByDisplayName = "system",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Revision = 1,
            SortOrder = sortOrder
        };
    }

    private void EnsureAcceptanceAudioFiles()
    {
        if (string.IsNullOrWhiteSpace(_audioFolderPath)) return;
        Directory.CreateDirectory(_audioFolderPath);
        var dir = Path.Combine(_audioFolderPath, "acceptance_01456");
        Directory.CreateDirectory(dir);
        foreach (var file in new[] { "calm_acceptance_01456.wav", "battle_acceptance_01456.wav", "siege_acceptance_01456.wav", "gm_hidden_audio_01456.wav" })
        {
            var path = Path.Combine(dir, file);
            if (!File.Exists(path)) WriteSilentWav(path);
        }
    }

    private static void WriteSilentWav(string path)
    {
        const int sampleRate = 8000;
        const short bitsPerSample = 16;
        const short channels = 1;
        const int seconds = 1;
        var dataLength = sampleRate * seconds * channels * bitsPerSample / 8;
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream);
        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + dataLength);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
    }

    private static AudioCategory InferCategory(string fullPath)
    {
        var lower = fullPath.ToLowerInvariant();
        if (lower.Contains("battle") || lower.Contains("combat")) return AudioCategory.Battle;
        if (lower.Contains("siege") || lower.Contains("osada")) return AudioCategory.Siege;
        if (lower.Contains("tense")) return AudioCategory.Tense;
        if (lower.Contains("calm") || lower.Contains("pause")) return AudioCategory.Calm;
        if (lower.Contains("manual") || lower.Contains("custom")) return AudioCategory.Custom;
        return AudioCategory.Custom;
    }

    private AudioTrackDefinition? PickTrackForCategory(AudioCategory category, SessionAudioState state, bool advance)
    {
        var tracks = LoadEnabledAudioTracks(adminOnly: true)
            .Where(x => TrackCategoryMatches(x, category))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToList();

        if (tracks.Count == 0 && category != AudioCategory.Custom)
            tracks = LoadEnabledAudioTracks(adminOnly: true)
                .Where(x => x.Category == AudioCategory.Custom || ToAudioCategoryId(x.Category) == "custom")
                .OrderBy(x => x.SortOrder).ThenBy(x => x.DisplayName).ToList();
        if (tracks.Count == 0) return null;

        var categoryKey = ToAudioCategoryId(category);
        var rot = state.Rotation.FirstOrDefault(x => string.Equals(x.Category, categoryKey, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Category, category.ToString(), StringComparison.OrdinalIgnoreCase));
        if (rot == null)
        {
            rot = new AudioTrackRotationState { Category = categoryKey, Cursor = 0 };
            state.Rotation.Add(rot);
        }

        if (advance) rot.Cursor += 1;
        if (rot.Cursor >= tracks.Count) rot.Cursor = 0;
        if (rot.Cursor < 0) rot.Cursor = 0;
        return tracks[rot.Cursor];
    }

    private void SwitchToTrack(SessionAudioState state, AudioTrackDefinition? track, UserAccount actor, string reason, bool playing)
        => SwitchToTrack(state, track, actor.Id, reason, playing);

    private void SwitchToTrack(SessionAudioState state, AudioTrackDefinition? track, string actorUserId, string reason, bool playing)
    {
        if (track == null)
        {
            state.PlaybackState = AudioPlaybackState.Stopped;
            state.PlaybackStateId = "stopped";
            state.CurrentTrackId = null;
            state.CurrentTrackPath = string.Empty;
            state.CurrentTrackDisplayName = string.Empty;
            state.PositionSeconds = 0;
            state.StartOffsetSeconds = 0;
            return;
        }

        state.CurrentTrackId = track.Id;
        state.CurrentTrackPath = track.FilePath;
        state.CurrentTrackDisplayName = track.DisplayName;
        state.CurrentCategory = track.Category;
        state.CurrentCategoryId = ToAudioCategoryId(track.Category);
        state.StartedAtUtc = DateTime.UtcNow;
        state.PausedAtUtc = null;
        state.StartOffsetSeconds = 0;
        state.PositionSeconds = 0;
        state.PlaybackState = playing ? AudioPlaybackState.Playing : AudioPlaybackState.Stopped;
        state.PlaybackStateId = playing ? "playing" : "stopped";
        state.LastUpdatedUtc = DateTime.UtcNow;
        state.UpdatedAtUtc = state.LastUpdatedUtc;
        _logger.Session($"audio.switch session={state.SessionId} track={track.DisplayName} reason={reason} actor={actorUserId}");
    }

    private int ResolveCurrentTrackDuration(SessionAudioState state)
    {
        if (string.IsNullOrWhiteSpace(state.CurrentTrackId)) return 0;
        var t = FindAudioTrack(state.CurrentTrackId);
        return t?.DurationSeconds ?? 0;
    }

    private int ResolvePositionSeconds(SessionAudioState state)
    {
        if (state.PlaybackState == AudioPlaybackState.Paused || state.PlaybackState == AudioPlaybackState.Stopped)
            return Math.Max(0, state.PositionSeconds);
        return Math.Max(0, (int)(DateTime.UtcNow - state.StartedAtUtc).TotalSeconds + state.StartOffsetSeconds);
    }

    private Dictionary<string, object> AudioStatePayload(SessionAudioState state, bool playerSafe)
    {
        NormalizeAudioState(state);
        var track = string.IsNullOrWhiteSpace(state.CurrentTrackId) ? null : FindAudioTrack(state.CurrentTrackId);
        var position = ResolvePositionSeconds(state);
        var playback = state.PlaybackState;
        var playbackId = ToPlaybackStateId(playback);

        var payload = new Dictionary<string, object>
        {
            { "sessionId", state.SessionId },
            { "campaignId", FirstNonEmpty(state.CampaignId, AudioDefaultCampaignId) },
            { "mode", state.Mode.ToString() },
            { "category", ToAudioCategoryId(state.CurrentCategory) },
            { "currentCategory", ToAudioCategoryId(state.CurrentCategory) },
            { "trackId", state.CurrentTrackId ?? string.Empty },
            { "trackName", track != null ? track.DisplayName : FirstNonEmpty(state.CurrentTrackDisplayName, string.Empty) },
            { "trackDisplayName", track != null ? track.DisplayName : FirstNonEmpty(state.CurrentTrackDisplayName, string.Empty) },
            { "startedAtUtc", state.StartedAtUtc },
            { "pausedAtUtc", state.PausedAtUtc.HasValue ? (object)state.PausedAtUtc.Value : string.Empty },
            { "positionSeconds", position },
            { "overrideEnabled", state.OverrideEnabled },
            { "overrideByUserId", state.OverrideByUserId },
            { "fadeMilliseconds", state.FadeMilliseconds },
            { "fadeSeconds", state.FadeSeconds },
            { "loopMode", NormalizeLoopMode(state.LoopMode) ?? AudioLoopModeIds.None },
            { "autoNextMode", FirstNonEmpty(state.AutoNextMode, AudioAutoNextModeIds.Category) },
            { "volumeMaster", state.VolumeMaster },
            { "playbackState", playbackId },
            { "playbackStateText", PlaybackLabel(playbackId) },
            { "updatedByUserId", state.UpdatedByUserId },
            { "updatedByDisplayName", state.UpdatedByDisplayName },
            { "updatedAtUtc", state.UpdatedAtUtc },
            { "revision", state.Revision },
            { "schemaVersion", state.SchemaVersion }
        };

        if (!playerSafe)
        {
            payload["trackPath"] = state.CurrentTrackPath;
            payload["currentTrackPath"] = state.CurrentTrackPath;
            payload["track"] = track != null ? AudioTrackPayload(track, playerSafe: false) : new Dictionary<string, object>();
        }

        return payload;
    }

    private Dictionary<string, object> AudioPlayerStatePayload(SessionAudioState state)
    {
        var track = string.IsNullOrWhiteSpace(state.CurrentTrackId) ? null : FindAudioTrack(state.CurrentTrackId);
        var trackVisible = track != null && IsTrackVisibleToPlayer(track);
        var payload = AudioStatePayload(state, playerSafe: true);
        if (!trackVisible)
        {
            payload["trackId"] = string.Empty;
            payload["trackName"] = state.PlaybackState == AudioPlaybackState.Stopped ? string.Empty : "Музыка мастера";
            payload["trackDisplayName"] = payload["trackName"];
            payload["isCurrentTrackHidden"] = true;
            payload["track"] = new Dictionary<string, object>();
        }
        else
        {
            payload["track"] = AudioTrackPayload(track!, playerSafe: true);
            payload["isCurrentTrackHidden"] = false;
        }

        payload.Remove("trackPath");
        payload.Remove("currentTrackPath");
        payload.Remove("overrideByUserId");
        payload.Remove("updatedByUserId");
        return payload;
    }

    private Dictionary<string, object> AudioTrackPayload(AudioTrackDefinition x, bool playerSafe)
    {
        NormalizeAudioTrack(x);
        var payload = new Dictionary<string, object>
        {
            { "trackId", x.Id },
            { "displayName", x.DisplayName },
            { "category", ToAudioCategoryId(x.Category) },
            { "durationSeconds", x.DurationSeconds },
            { "isEnabled", x.IsEnabled },
            { "isPlayerVisible", x.IsPlayerVisible },
            { "visibility", x.Visibility },
            { "sortOrder", x.SortOrder },
            { "loopDefault", x.LoopDefault },
            { "tags", x.Tags.Cast<object>().ToArray() },
            { "revision", x.Revision },
            { "updatedAtUtc", x.UpdatedAtUtc },
            { "isArchived", x.IsArchived || x.Archived }
        };

        if (!playerSafe)
        {
            payload["filePath"] = x.FilePath;
            payload["relativePath"] = x.RelativePath;
            payload["fileName"] = x.FileName;
            payload["createdByDisplayName"] = x.CreatedByDisplayName;
            payload["updatedByDisplayName"] = x.UpdatedByDisplayName;
        }

        return payload;
    }

    private Dictionary<string, object> AudioClientSettingsPayload(AudioClientSettingsState item) => new Dictionary<string, object>
    {
        { "userId", item.UserId },
        { "sessionId", item.SessionId },
        { "volume", item.LocalVolume > 0 || item.Volume == 0 ? item.LocalVolume : item.Volume },
        { "localVolume", item.LocalVolume > 0 || item.Volume == 0 ? item.LocalVolume : item.Volume },
        { "muted", item.IsMuted || item.Muted },
        { "isMuted", item.IsMuted || item.Muted },
        { "preferredOutput", item.PreferredOutput },
        { "updatedAtUtc", item.UpdatedAtUtc },
        { "revision", item.Revision }
    };

    private List<AudioTrackDefinition> LoadEnabledAudioTracks(bool adminOnly)
    {
        return _repositories.AudioTracks.Find(FilterDefinition<AudioTrackDefinition>.Empty)
            .Select(x => { NormalizeAudioTrack(x); return x; })
            .Where(x => x.IsEnabled && !x.Archived && !x.IsArchived)
            .Where(x => adminOnly || IsTrackVisibleToPlayer(x))
            .ToList();
    }

    private AudioTrackDefinition? FindAudioTrack(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId)) return null;
        var byId = _repositories.AudioTracks.GetById(trackId);
        if (byId != null) return byId;
        return _repositories.AudioTracks.Find(FilterDefinition<AudioTrackDefinition>.Empty)
            .FirstOrDefault(x => string.Equals(x.TrackId, trackId, StringComparison.OrdinalIgnoreCase));
    }

    private AudioClientSettingsState EnsureClientSettings(string userId, string sessionId)
    {
        var item = _repositories.AudioClientSettings.Find(Builders<AudioClientSettingsState>.Filter.Eq(x => x.UserId, userId)).FirstOrDefault();
        if (item == null)
        {
            item = new AudioClientSettingsState
            {
                UserId = userId,
                SessionId = sessionId,
                Volume = 0.7,
                LocalVolume = 0.7,
                Muted = false,
                IsMuted = false,
                UpdatedAtUtc = DateTime.UtcNow,
                Revision = 1
            };
            _repositories.AudioClientSettings.Insert(item);
            return item;
        }

        if (item.LocalVolume <= 0 && item.Volume > 0) item.LocalVolume = item.Volume;
        item.Volume = item.LocalVolume;
        item.IsMuted = item.IsMuted || item.Muted;
        item.Muted = item.IsMuted;
        if (string.IsNullOrWhiteSpace(item.SessionId)) item.SessionId = sessionId;
        return item;
    }

    private void SaveAudioState(SessionAudioState state)
    {
        NormalizeAudioState(state);
        _repositories.AudioStates.Replace(state);
    }

    private void TouchAudioState(SessionAudioState state, UserAccount actor)
    {
        state.UpdatedByUserId = actor.Id;
        state.UpdatedByDisplayName = actor.Login;
        state.UpdatedAtUtc = DateTime.UtcNow;
        state.LastUpdatedUtc = state.UpdatedAtUtc;
        state.Revision += 1;
    }

    private void NormalizeAudioState(SessionAudioState state)
    {
        state.SessionId = FirstNonEmpty(state.SessionId, AudioDefaultSessionId);
        state.CampaignId = FirstNonEmpty(state.CampaignId, AudioDefaultCampaignId);
        state.CurrentCategoryId = ToAudioCategoryId(state.CurrentCategory);
        state.PlaybackStateId = ToPlaybackStateId(state.PlaybackState);
        if (state.FadeSeconds <= 0 && state.FadeMilliseconds > 0) state.FadeSeconds = state.FadeMilliseconds / 1000.0;
        if (state.FadeMilliseconds <= 0 && state.FadeSeconds > 0) state.FadeMilliseconds = (int)Math.Round(state.FadeSeconds * 1000.0);
        if (string.IsNullOrWhiteSpace(state.LoopMode)) state.LoopMode = AudioLoopModeIds.None;
        if (string.IsNullOrWhiteSpace(state.AutoNextMode)) state.AutoNextMode = AudioAutoNextModeIds.Category;
        if (state.VolumeMaster <= 0) state.VolumeMaster = 1.0;
        if (!string.IsNullOrWhiteSpace(state.CurrentTrackId))
        {
            var track = FindAudioTrack(state.CurrentTrackId);
            if (track != null)
            {
                state.CurrentTrackDisplayName = track.DisplayName;
                state.CurrentTrackPath = track.FilePath;
                state.CurrentCategory = track.Category;
                state.CurrentCategoryId = ToAudioCategoryId(track.Category);
            }
        }
    }

    private static void NormalizeAudioTrack(AudioTrackDefinition track)
    {
        if (string.IsNullOrWhiteSpace(track.TrackId)) track.TrackId = track.Id;
        if (string.IsNullOrWhiteSpace(track.CategoryId)) track.CategoryId = ToAudioCategoryId(track.Category);
        if (string.IsNullOrWhiteSpace(track.FileName)) track.FileName = Path.GetFileName(FirstNonEmpty(track.RelativePath, track.FilePath));
        if (string.IsNullOrWhiteSpace(track.RelativePath)) track.RelativePath = NormalizeAudioRelativePath(track.FilePath);
        if (string.IsNullOrWhiteSpace(track.FilePath)) track.FilePath = track.RelativePath;
        if (string.IsNullOrWhiteSpace(track.Visibility)) track.Visibility = track.IsPlayerVisible ? AudioVisibilityIds.PlayerVisible : AudioVisibilityIds.GMOnly;
        track.IsArchived = track.IsArchived || track.Archived;
        if (track.UpdatedAtUtc == default) track.UpdatedAtUtc = track.UpdatedUtc;
        if (track.CreatedAtUtc == default) track.CreatedAtUtc = track.CreatedUtc;
    }

    private static bool TryNormalizeAudioCategory(string? value, out AudioCategory category)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        category = AudioCategory.Custom;
        if (string.IsNullOrWhiteSpace(text)) return false;
        switch (text)
        {
            case "normal":
            case "custom":
            case "manual":
                category = AudioCategory.Custom;
                return true;
            case "calm":
            case "спокойная":
                category = AudioCategory.Calm;
                return true;
            case "battle":
            case "combat":
            case "боевая":
                category = AudioCategory.Battle;
                return true;
            case "siege":
            case "осада":
            case "осадная":
                category = AudioCategory.Siege;
                return true;
            case "tense":
                category = AudioCategory.Tense;
                return true;
            default:
                if (Enum.TryParse(value, true, out category))
                {
                    if (category == AudioCategory.Normal || category == AudioCategory.Manual) category = AudioCategory.Custom;
                    if (category == AudioCategory.Combat) category = AudioCategory.Battle;
                    return true;
                }
                return false;
        }
    }

    private static string ToAudioCategoryId(AudioCategory category)
    {
        return category switch
        {
            AudioCategory.Calm => "calm",
            AudioCategory.Battle or AudioCategory.Combat => "battle",
            AudioCategory.Siege => "siege",
            AudioCategory.Tense => "tense",
            _ => "custom"
        };
    }

    private static int CategorySort(AudioCategory category)
    {
        return ToAudioCategoryId(category) switch
        {
            "calm" => 10,
            "battle" => 20,
            "siege" => 30,
            _ => 100
        };
    }

    private static bool TrackCategoryMatches(AudioTrackDefinition track, AudioCategory category)
    {
        return string.Equals(ToAudioCategoryId(track.Category), ToAudioCategoryId(category), StringComparison.OrdinalIgnoreCase)
               || string.Equals(track.CategoryId, ToAudioCategoryId(category), StringComparison.OrdinalIgnoreCase);
    }

    private static string ToPlaybackStateId(AudioPlaybackState state)
    {
        return state switch
        {
            AudioPlaybackState.Playing or AudioPlaybackState.Transitioning => "playing",
            AudioPlaybackState.Paused => "paused",
            _ => "stopped"
        };
    }

    private static string PlaybackLabel(string playbackId)
    {
        return playbackId switch
        {
            "playing" => "Воспроизведение",
            "paused" => "Пауза",
            _ => "Остановлено"
        };
    }

    private static string NormalizeAudioVisibility(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "player" or "public" or "player_visible" => AudioVisibilityIds.PlayerVisible,
            "gm" or "gm_only" or "master" => AudioVisibilityIds.GMOnly,
            "admin" or "admin_only" => AudioVisibilityIds.AdminOnly,
            "server" or "server_only" => AudioVisibilityIds.ServerOnly,
            _ => AudioVisibilityIds.GMOnly
        };
    }

    private static string? NormalizeLoopMode(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value)) return AudioLoopModeIds.None;
        return value switch
        {
            "none" or "off" or "нет" => AudioLoopModeIds.None,
            "track" or "трек" => AudioLoopModeIds.Track,
            "category" or "категория" => AudioLoopModeIds.Category,
            _ => null
        };
    }

    private static bool IsTrackVisibleToPlayer(AudioTrackDefinition track)
    {
        NormalizeAudioTrack(track);
        return track.IsPlayerVisible
               && string.Equals(track.Visibility, AudioVisibilityIds.PlayerVisible, StringComparison.OrdinalIgnoreCase)
               && !track.Archived
               && !track.IsArchived
               && track.IsEnabled;
    }

    private static double NormalizeVolume(double volume)
    {
        if (volume > 1.0 && volume <= 100.0) volume /= 100.0;
        return Math.Max(0, Math.Min(1, volume));
    }

    private string ReadAudioSessionId(IDictionary<string, object>? payload)
        => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "sessionId"), AudioDefaultSessionId), 1, 128, "sessionId");

    private static string NormalizeAudioRelativePath(string raw)
    {
        var value = (raw ?? string.Empty).Trim().Replace('\\', '/');
        while (value.StartsWith("/", StringComparison.Ordinal)) value = value.Substring(1);
        return value;
    }

    private ResponseEnvelope AudioValidation(string message, ResponseStatus status = ResponseStatus.ValidationFailed, ErrorCode code = ErrorCode.ValidationFailed)
        => Error(message, status, code);

    private static ResponseEnvelope AudioDisabled(string command)
        => Error("Audio / Music MVP is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);

    private bool AudioBaseEnabled()
        => _featureFlags.IsEnabled(nameof(AudioFeatureFlags.UseAudioMusicMvp));

    private bool AudioAdminEnabled()
        => AudioBaseEnabled() && _featureFlags.IsEnabled(nameof(AudioFeatureFlags.UseAudioAdminControls));

    private bool AudioPlayerEnabled()
        => AudioBaseEnabled() && _featureFlags.IsEnabled(nameof(AudioFeatureFlags.UseAudioPlayerView));

    private bool AudioClientSettingsEnabled()
        => AudioBaseEnabled() && _featureFlags.IsEnabled(nameof(AudioFeatureFlags.UseAudioClientSettings));

    private bool IsNewAudioCommand(string command)
        => !string.IsNullOrWhiteSpace(command) && command.StartsWith("audio.", StringComparison.OrdinalIgnoreCase) && command.Contains(".player.");

    private void WriteAudioEvent(UserAccount actor, SessionAudioState state, string eventType, string summary, bool playerVisible)
    {
        var target = state.SessionId + ":" + (state.CurrentTrackId ?? string.Empty);
        WriteAudit("audio", actor.Id, eventType, target);
        _logger.Admin($"audio.event type={eventType} actor={actor.Login} session={state.SessionId} track={state.CurrentTrackId} revision={state.Revision}");

        if (!_featureFlags.IsEnabled(nameof(AudioFeatureFlags.UseAudioEventJournalIntegration))
            || !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp))
            || !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion)))
            return;

        try
        {
            var now = DateTime.UtcNow;
            var entry = new EventJournalEntryState
            {
                CampaignId = FirstNonEmpty(state.CampaignId, AudioDefaultCampaignId),
                SessionId = state.SessionId,
                SourceModule = "audio",
                SourceEventType = eventType,
                SourceEventId = Guid.NewGuid().ToString("N"),
                CorrelationId = target,
                EntryType = EventJournalEntryTypeIds.Automatic,
                Category = EventJournalCategoryIds.Session,
                Severity = EventJournalSeverityIds.Information,
                Title = "Музыка сессии",
                Summary = summary,
                PlayerSummary = playerVisible ? summary : string.Empty,
                GMDetails = $"trackId={state.CurrentTrackId}; category={state.CurrentCategoryId}; state={state.PlaybackStateId}",
                VisibilityMode = playerVisible ? EventJournalVisibilityModeIds.PlayerVisible : EventJournalVisibilityModeIds.GMOnly,
                IsPlayerVisible = playerVisible,
                IsAutomatic = true,
                ActorUserId = actor.Id,
                ActorDisplayName = actor.Login,
                SubjectEntityType = "audio_track",
                SubjectEntityId = state.CurrentTrackId ?? string.Empty,
                SubjectDisplayName = state.CurrentTrackDisplayName,
                OccurredAtUtc = now,
                SequenceNumber = NextJournalSequence(FirstNonEmpty(state.CampaignId, AudioDefaultCampaignId)),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedByUserId = actor.Id,
                Tags = new List<string> { "audio", "0.14.56", eventType }
            };
            _repositories.EventJournalEntries.Insert(entry);
            AddJournalLink(entry, EventJournalEntityTypeIds.CurrentSession, state.SessionId, "Сессия", EventJournalLinkRoleIds.Source, playerVisible);
            WriteJournalAudit(actor, entry, "audio.ingested", "Audio event journal entry created.");
        }
        catch (Exception ex)
        {
            _logger.Debug("audio.event_journal.write_failed message=" + ex.Message);
        }
    }

    private static string MakeRelativePath(string root, string full)
    {
        var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var f = Path.GetFullPath(full);
        if (f.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return f.Substring(r.Length).Replace('\\', '/');
        return Path.GetFileName(full);
    }
}
