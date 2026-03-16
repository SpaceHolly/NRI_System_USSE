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
    private static readonly string[] SupportedAudioExtensions = { ".mp3", ".wav", ".ogg" };

    public ResponseEnvelope AudioStateGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var state = EnsureAudioState(sessionId, null);
        return Ok("Audio state loaded.", AudioStatePayload(state));
    }

    public ResponseEnvelope AudioStateSync(CommandContext context) => AudioStateGet(context);

    public ResponseEnvelope AudioModeGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var state = EnsureAudioState(sessionId, null);
        return Ok("Audio mode loaded.", new Dictionary<string, object>
        {
            { "mode", state.Mode.ToString() },
            { "category", state.CurrentCategory.ToString() },
            { "overrideEnabled", state.OverrideEnabled },
            { "overrideByUserId", state.OverrideByUserId }
        });
    }

    public ResponseEnvelope AudioModeSet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var modeRaw = RequireLength(PayloadReader.GetString(context.Request.Payload, "mode"), 3, 64, "mode");
        var categoryRaw = PayloadReader.GetString(context.Request.Payload, "category") ?? AudioCategory.Manual.ToString();

        if (!Enum.TryParse<SessionAudioMode>(modeRaw, true, out var mode)) throw new ArgumentException("Unsupported audio mode.");
        if (!Enum.TryParse<AudioCategory>(categoryRaw, true, out var category)) throw new ArgumentException("Unsupported audio category.");

        var state = EnsureAudioState(sessionId, null);
        state.Mode = mode;
        state.OverrideEnabled = mode == SessionAudioMode.Manual;
        state.OverrideByUserId = state.OverrideEnabled ? actor.Id : string.Empty;
        if (state.OverrideEnabled)
        {
            state.CurrentCategory = category;
            SwitchToTrack(state, PickTrackForCategory(category, state, true), actor.Id, "audio.mode.set");
            PublishSystemMessage(sessionId, $"Audio mode set to Manual ({category}).");
        }
        else
        {
            var autoCategory = DetermineAutoCategory(sessionId);
            state.CurrentCategory = autoCategory;
            SwitchToTrack(state, PickTrackForCategory(autoCategory, state, false), actor.Id, "audio.mode.set.auto");
            PublishSystemMessage(sessionId, $"Audio mode set to Auto ({autoCategory}).");
        }

        state.LastUpdatedUtc = DateTime.UtcNow;
        _repositories.AudioStates.Replace(state);
        WriteAudit("audio", actor.Id, "mode.set", sessionId + ":" + state.Mode);
        return Ok("Audio mode updated.", AudioStatePayload(state));
    }

    public ResponseEnvelope AudioOverrideClear(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var state = EnsureAudioState(sessionId, null);
        state.Mode = SessionAudioMode.Auto;
        state.OverrideEnabled = false;
        state.OverrideByUserId = string.Empty;
        state.CurrentCategory = DetermineAutoCategory(sessionId);
        SwitchToTrack(state, PickTrackForCategory(state.CurrentCategory, state, false), actor.Id, "audio.override.clear");
        _repositories.AudioStates.Replace(state);
        PublishSystemMessage(sessionId, $"Audio override cleared. Mode=Auto ({state.CurrentCategory}).");
        WriteAudit("audio", actor.Id, "override.clear", sessionId);
        return Ok("Audio override cleared.", AudioStatePayload(state));
    }

    public ResponseEnvelope AudioLibraryGet(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureAudioLibraryLoaded();
        var tracks = _repositories.AudioTracks.Find(FilterDefinition<AudioTrackDefinition>.Empty)
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.DisplayName)
            .Select(AudioTrackPayload)
            .Cast<object>()
            .ToArray();
        return Ok("Audio library loaded.", new Dictionary<string, object> { { "items", tracks }, { "root", _audioFolderPath } });
    }

    public ResponseEnvelope AudioTrackSelect(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var trackId = RequireLength(PayloadReader.GetString(context.Request.Payload, "trackId"), 8, 128, "trackId");
        EnsureAudioLibraryLoaded();
        var track = _repositories.AudioTracks.GetById(trackId) ?? throw new KeyNotFoundException("Track not found.");
        if (!track.IsEnabled) throw new InvalidOperationException("Track is disabled.");

        var state = EnsureAudioState(sessionId, null);
        state.Mode = SessionAudioMode.Manual;
        state.OverrideEnabled = true;
        state.OverrideByUserId = actor.Id;
        state.CurrentCategory = AudioCategory.Manual;
        SwitchToTrack(state, track, actor.Id, "audio.track.select");
        _repositories.AudioStates.Replace(state);
        PublishSystemMessage(sessionId, $"Audio track selected manually: {track.DisplayName}.");
        WriteAudit("audio", actor.Id, "track.select", sessionId + ":" + trackId);
        return Ok("Audio track selected.", AudioStatePayload(state));
    }

    public ResponseEnvelope AudioTrackNext(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var state = EnsureAudioState(sessionId, null);
        var track = PickTrackForCategory(state.CurrentCategory, state, true);
        if (track == null) throw new InvalidOperationException("No tracks in current category.");
        SwitchToTrack(state, track, actor.Id, "audio.track.next");
        _repositories.AudioStates.Replace(state);
        PublishSystemMessage(sessionId, $"Audio switched to next track: {track.DisplayName}.");
        WriteAudit("audio", actor.Id, "track.next", sessionId + ":" + track.Id);
        return Ok("Moved to next track.", AudioStatePayload(state));
    }

    public ResponseEnvelope AudioTrackReload(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureAudioLibraryLoaded(true);
        WriteAudit("audio", actor.Id, "track.reload", "library");
        _logger.Admin($"audio.library.reload actor={actor.Id}");
        return Ok("Audio library reloaded.");
    }

    public ResponseEnvelope AudioClientSettingsGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var item = _repositories.AudioClientSettings.Find(Builders<AudioClientSettingsState>.Filter.Eq(x => x.UserId, actor.Id)).FirstOrDefault();
        if (item == null)
        {
            item = new AudioClientSettingsState { UserId = actor.Id, Volume = 0.7, Muted = false };
            _repositories.AudioClientSettings.Insert(item);
        }

        return Ok("Audio client settings loaded.", new Dictionary<string, object>
        {
            { "volume", item.Volume },
            { "muted", item.Muted }
        });
    }

    public ResponseEnvelope AudioClientSettingsSet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var volume = PayloadReader.GetDouble(context.Request.Payload, "volume") ?? 0.7;
        volume = Math.Max(0, Math.Min(1, volume));
        var muted = PayloadReader.GetBool(context.Request.Payload, "muted");
        var item = _repositories.AudioClientSettings.Find(Builders<AudioClientSettingsState>.Filter.Eq(x => x.UserId, actor.Id)).FirstOrDefault();
        if (item == null)
        {
            item = new AudioClientSettingsState { UserId = actor.Id, Volume = volume, Muted = muted };
            _repositories.AudioClientSettings.Insert(item);
        }
        else
        {
            item.Volume = volume;
            item.Muted = muted;
            _repositories.AudioClientSettings.Replace(item);
        }

        return Ok("Audio client settings updated.", new Dictionary<string, object> { { "volume", item.Volume }, { "muted", item.Muted } });
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
            state = new SessionAudioState { SessionId = sessionId, Mode = SessionAudioMode.Auto, CurrentCategory = DetermineAutoCategory(sessionId), PlaybackState = AudioPlaybackState.Stopped, StartedAtUtc = DateTime.UtcNow };
            var first = PickTrackForCategory(state.CurrentCategory, state, true);
            if (first != null) SwitchToTrack(state, first, actorUserId ?? "system", "audio.init");
            _repositories.AudioStates.Insert(state);
            return state;
        }

        if (!state.OverrideEnabled)
        {
            var autoCategory = DetermineAutoCategory(sessionId);
            if (state.CurrentCategory != autoCategory)
            {
                state.CurrentCategory = autoCategory;
                SwitchToTrack(state, PickTrackForCategory(autoCategory, state, true), actorUserId ?? "system", "audio.auto.categoryChange");
                PublishSystemMessage(sessionId, $"Audio category changed to {autoCategory}.");
            }
        }

        var duration = ResolveCurrentTrackDuration(state);
        var elapsed = (int)(DateTime.UtcNow - state.StartedAtUtc).TotalSeconds + state.StartOffsetSeconds;
        if (duration > 0 && elapsed >= duration)
        {
            var next = PickTrackForCategory(state.CurrentCategory, state, true);
            if (next != null)
            {
                SwitchToTrack(state, next, actorUserId ?? "system", "audio.auto.nextAfterEnd");
                PublishSystemMessage(sessionId, $"Audio switched to {next.DisplayName}.");
            }
        }

        state.LastUpdatedUtc = DateTime.UtcNow;
        _repositories.AudioStates.Replace(state);
        return state;
    }

    private AudioCategory DetermineAutoCategory(string sessionId)
    {
        var combat = _repositories.Combats.Find(Builders<CombatState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault();
        if (combat != null && combat.Status == CombatStatus.Active) return AudioCategory.Combat;
        return AudioCategory.Normal;
    }

    private void EnsureAudioLibraryLoaded(bool force = false)
    {
        var tracks = _repositories.AudioTracks.Find(FilterDefinition<AudioTrackDefinition>.Empty);
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
            .ToDictionary(x => x.FilePath, StringComparer.OrdinalIgnoreCase);

        var index = 0;
        foreach (var file in files)
        {
            var rel = MakeRelativePath(_audioFolderPath, file);
            var cat = InferCategory(file);
            if (existingByPath.TryGetValue(rel, out var ex))
            {
                ex.DisplayName = Path.GetFileNameWithoutExtension(file);
                ex.Category = cat;
                ex.SortOrder = index++;
                ex.IsEnabled = true;
                _repositories.AudioTracks.Replace(ex);
                continue;
            }

            _repositories.AudioTracks.Insert(new AudioTrackDefinition
            {
                DisplayName = Path.GetFileNameWithoutExtension(file),
                FilePath = rel,
                Category = cat,
                DurationSeconds = 0,
                IsEnabled = true,
                SortOrder = index++
            });
        }

        foreach (var stale in existingByPath.Values.Where(x => !files.Any(f => string.Equals(MakeRelativePath(_audioFolderPath, f), x.FilePath, StringComparison.OrdinalIgnoreCase))))
        {
            stale.IsEnabled = false;
            _repositories.AudioTracks.Replace(stale);
            _logger.Debug($"Audio metadata points to missing file: {stale.FilePath}");
        }
    }

    private static AudioCategory InferCategory(string fullPath)
    {
        var lower = fullPath.ToLowerInvariant();
        if (lower.Contains("combat")) return AudioCategory.Combat;
        if (lower.Contains("tense")) return AudioCategory.Tense;
        if (lower.Contains("calm") || lower.Contains("pause")) return AudioCategory.Calm;
        if (lower.Contains("manual")) return AudioCategory.Manual;
        return AudioCategory.Normal;
    }

    private AudioTrackDefinition PickTrackForCategory(AudioCategory category, SessionAudioState state, bool advance)
    {
        var tracks = _repositories.AudioTracks.Find(Builders<AudioTrackDefinition>.Filter.Eq(x => x.Category, category) & Builders<AudioTrackDefinition>.Filter.Eq(x => x.IsEnabled, true))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToList();

        if (tracks.Count == 0 && category != AudioCategory.Normal)
        {
            tracks = _repositories.AudioTracks.Find(Builders<AudioTrackDefinition>.Filter.Eq(x => x.Category, AudioCategory.Normal) & Builders<AudioTrackDefinition>.Filter.Eq(x => x.IsEnabled, true)).OrderBy(x => x.SortOrder).ThenBy(x => x.DisplayName).ToList();
        }
        if (tracks.Count == 0) return null;

        var rot = state.Rotation.FirstOrDefault(x => x.Category == category.ToString());
        if (rot == null)
        {
            rot = new AudioTrackRotationState { Category = category.ToString(), Cursor = 0 };
            state.Rotation.Add(rot);
        }

        if (advance) rot.Cursor += 1;
        if (rot.Cursor >= tracks.Count) rot.Cursor = 0;
        if (rot.Cursor < 0) rot.Cursor = 0;
        return tracks[rot.Cursor];
    }

    private void SwitchToTrack(SessionAudioState state, AudioTrackDefinition track, string actorUserId, string reason)
    {
        if (track == null)
        {
            state.PlaybackState = AudioPlaybackState.Stopped;
            state.CurrentTrackId = null;
            state.CurrentTrackPath = string.Empty;
            return;
        }

        var changed = state.CurrentTrackId != track.Id;
        state.CurrentTrackId = track.Id;
        state.CurrentTrackPath = track.FilePath;
        state.StartedAtUtc = DateTime.UtcNow;
        state.StartOffsetSeconds = 0;
        state.PlaybackState = changed ? AudioPlaybackState.Transitioning : AudioPlaybackState.Playing;
        state.LastUpdatedUtc = DateTime.UtcNow;

        _logger.Session($"audio.switch session={state.SessionId} track={track.DisplayName} reason={reason} actor={actorUserId}");
    }

    private int ResolveCurrentTrackDuration(SessionAudioState state)
    {
        if (string.IsNullOrWhiteSpace(state.CurrentTrackId)) return 0;
        var t = _repositories.AudioTracks.GetById(state.CurrentTrackId);
        return t?.DurationSeconds ?? 0;
    }

    private Dictionary<string, object> AudioStatePayload(SessionAudioState state)
    {
        var track = string.IsNullOrWhiteSpace(state.CurrentTrackId) ? null : _repositories.AudioTracks.GetById(state.CurrentTrackId);
        var nowOffset = Math.Max(0, (int)(DateTime.UtcNow - state.StartedAtUtc).TotalSeconds + state.StartOffsetSeconds);
        var playback = state.PlaybackState;
        if (playback == AudioPlaybackState.Transitioning && (DateTime.UtcNow - state.StartedAtUtc).TotalMilliseconds > state.FadeMilliseconds)
            playback = AudioPlaybackState.Playing;

        return new Dictionary<string, object>
        {
            { "sessionId", state.SessionId },
            { "mode", state.Mode.ToString() },
            { "category", state.CurrentCategory.ToString() },
            { "trackId", state.CurrentTrackId ?? string.Empty },
            { "trackName", track != null ? track.DisplayName : string.Empty },
            { "trackPath", state.CurrentTrackPath },
            { "startedAtUtc", state.StartedAtUtc },
            { "positionSeconds", nowOffset },
            { "overrideEnabled", state.OverrideEnabled },
            { "overrideByUserId", state.OverrideByUserId },
            { "fadeMilliseconds", state.FadeMilliseconds },
            { "playbackState", playback.ToString() }
        };
    }

    private Dictionary<string, object> AudioTrackPayload(AudioTrackDefinition x)
    {
        return new Dictionary<string, object>
        {
            { "trackId", x.Id },
            { "displayName", x.DisplayName },
            { "filePath", x.FilePath },
            { "category", x.Category.ToString() },
            { "durationSeconds", x.DurationSeconds },
            { "isEnabled", x.IsEnabled },
            { "sortOrder", x.SortOrder }
        };
    }

    private static string MakeRelativePath(string root, string full)
    {
        var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var f = Path.GetFullPath(full);
        if (f.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return f.Substring(r.Length).Replace('\\', '/');
        return Path.GetFileName(full);
    }
}
