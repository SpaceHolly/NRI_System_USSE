# NRI System Architecture (.NET Framework 4.8.1)

## Что реализовано сейчас

## Сервер
- TCP+JSON сервер с command dispatcher и MongoDB persistence.
- Auth/roles/presence/characters/locks/request+dice workflows.
- **Новая полнофункциональная подсистема боя**:
  - `combat.start`, `combat.end`, `combat.getState`, `combat.getHistory`;
  - ходы/раунды: `combat.nextTurn`, `combat.previousTurn`, `combat.nextRound`, `combat.skipTurn`, `combat.selectActive`;
  - управление инициативой: `combat.reorderBeforeStart`, `combat.reorderSlotMembers`;
  - участники: `combat.addParticipant`, `combat.removeParticipant`, `combat.detachCompanion`;
  - клиентские фиды: `combat.visibleState`, `combat.participants`, `combat.timeline`.

## Модель инициативы и боя
- Инициатива серверная (`1d100`) с tie-break перебросами до однозначного порядка.
- Особые правила:
  - результат `100`: один победитель (через tie-break между `100`) получает дополнительный ход первого раунда перед общей очередью;
  - результат `1`: участник пропускает первый ход первого раунда.
- Групповые слоты инициативы поддерживаются отдельной структурой `InitiativeSlot` + `InternalOrder`.
- Компаньоны по умолчанию могут идти в слоте владельца (через group-key), при необходимости отделяются `combat.detachCompanion`.
- Добавление новой стороны в активный бой поддерживается, новые участники вставляются в очередь с более низким приоритетом на равных roll со старой очередью.

## Combat + Request интеграция
- Request subsystem и combat subsystem сосуществуют в одном серверном application layer.
- В будущих этапах заявки/навыки/действия в бою будут ссылаться на `CombatState`, текущий раунд и активный слот.

## MongoDB коллекции
- Основные: `accounts`, `profiles`, `characters`, `sessions`, `locks`, `audit_logs`.
- Requests/dice: `action_requests`, `dice_requests`.
- Combat: `combat_states`, `combat_logs`.

## Клиенты
### AdminClient
- Combat tracker с реальным управлением:
  - запуск/завершение боя;
  - переход хода/раунда, пропуск, возврат;
  - добавление/удаление участников, detach компаньона;
  - просмотр текущего state и combat history.
- Request moderation и dice history/feed сохранены.

### PlayerClient
- Просмотр текущего боя:
  - раунд, активный слот,
  - список слотов/участников,
  - timeline событий.
- Управление ходами на стороне игрока отсутствует (серверная authority).

## История и логирование
- Combat события пишутся в `combat_logs`.
- Параллельно пишутся session/admin/audit лог-каналы.

## Синхронизация клиентов
- Используется polling (10 секунд) + refresh-after-command.
- Это временно вместо push/event-stream: проще и стабильнее на текущем этапе TCP request/response без отдельного realtime канала.

## Что остаётся на следующие этапы
- Полная интеграция боевых action requests/активируемых навыков с turn validation.
- Сложные боевые эффекты и автоматические правила статусов/баффов.
- Полноценный push-синк (server events) вместо polling.

## Class/Skill Subsystem (Stage: configurable progression)

- Added server-side configurable definitions for class trees and skills via `ClassTreeDefinition`, `ClassNodeDefinition`, `SkillDefinitionRecord`, and related requirement/effect models.
- Definitions loading strategy is **JSON-first with DB fallback/override persistence hooks**:
  - server tries Mongo collections (`class_tree_definitions`, `skill_definitions`) first;
  - if collections are empty, it loads JSON files from `definitions/classes.json` and `definitions/skills.json`;
  - if files are absent, server uses seeded defaults for six directions (Защитник, Передовой, Рейнджер, Самурай, Маг, Изобретатель).
- Admin can trigger runtime reload via `definitions.reload` without server restart.
- Reload updates `definition_versions` collection for audit/version tracking.

### Progress state separation
- `Character` now stores:
  - runtime progress (`ClassDirections`, `CharacterSkillStates`),
  - computed snapshot (`ClassSkillSnapshot`),
  - content version marker (`ClassSkillDefinitionVersion`).
- This separates:
  - configuration/definitions,
  - runtime progress,
  - computed derived state.

### Recalculation pipeline
- `RecalculateProgress` is the single server pipeline that computes:
  - node-derived stat bonuses,
  - passive effects,
  - unlock states,
  - skill availability/unavailability reasons.
- Clients only render server-calculated state.

### Branch and availability rules
- Enforced rule: one selected branch per direction.
- Node availability includes:
  - branch consistency check,
  - requirement checks (node/stat in this stage),
  - progression edge checks using `nextNodeIds`.
- Skill availability includes:
  - unlocked-by-node requirement,
  - optional requirement checks (node/skill requirement types).

### New protocol command groups
- Definitions: `definitions.classes.get`, `definitions.skills.get`, `definitions.reload`, `definitions.version.get`.
- Class tree: `classTree.get`, `classTree.node.get`, `classTree.available.get`, `classTree.acquireNode`, `classTree.recalculate`.
- Skills: `skills.list`, `skills.available`, `skills.get`, `skills.acquire`.
- Admin overrides: `admin.classTree.setState`, `admin.skills.setState`, `admin.character.progress.recalculate`.

### Client sync strategy
- Current strategy remains lightweight polling + refresh after action.
- Reason: fits current TCP+JSON request/response architecture and keeps implementation deterministic before introducing push streams.

## Session Chat Subsystem (Stage: moderated session chat)

- Added server-authoritative chat pipeline with message types:
  - `Public`
  - `HiddenToAdmins`
  - `AdminOnly`
  - `System`
- Visibility is filtered on server per requester; clients only render returned payload.
- Added persisted chat separation in Mongo:
  - `chat_messages` (message history)
  - `chat_read_states` (per-user read pointer)
  - `session_chat_settings` (slow mode + locks/mutes)
  - `chat_throttle_states` (anti-spam timestamps)

### Read/unread model
- Read state is stored as per-user per-session pointer (`LastReadMessageUtc` + `LastReadMessageId`).
- Messages are marked read when returned in visible history/feed (i.e., after entering visibility window).

### Moderation model
- Session-level player lock (`LockPlayers`).
- Per-user mute entries with reason and moderation metadata.
- Slow mode with independent intervals for `Public`, `HiddenToAdmins`, `AdminOnly`; admins are exempt.

### Chat protocol commands
- Messaging/history/read:
  - `chat.send`
  - `chat.history.get`
  - `chat.history.loadMore`
  - `chat.visibleFeed`
  - `chat.markRead`
  - `chat.unread.get`
- Moderation:
  - `chat.slowMode.get`
  - `chat.slowMode.set`
  - `chat.restrictions.get`
  - `chat.restrictions.muteUser`
  - `chat.restrictions.unmuteUser`
  - `chat.restrictions.lockPlayers`
  - `chat.restrictions.unlockPlayers`

### System messages integration
- System messages are published into the same chat history pipeline for key events:
  - user connect/disconnect
  - combat start/end
  - combat round start

### Sync strategy
- Current chat sync uses polling + refresh-after-send/read actions.
- This keeps behavior stable on current TCP+JSON request/response transport without introducing push channels yet.

## Session Audio Subsystem (Stage: synchronized background music)

- Added server-authoritative session audio state with categories/modes and synchronized playback snapshot.
- Audio files are stored on server filesystem (`AudioFolderPath`), while Mongo stores only metadata/state.

### Categories and modes
- Categories: `Normal`, `Combat`, `Tense`, `Calm`, `Manual`.
- Modes: `Auto` and `Manual`.
- Auto policy currently maps active combat to `Combat`, otherwise `Normal`.
- Manual override has priority until cleared by admin.

### Storage separation
- `audio_tracks`: track metadata/index (`DisplayName`, `FilePath`, `Category`, enabled flag, ordering, duration metadata).
- `audio_states`: runtime per-session playback state (mode, category, track, startedAt, fade, override metadata).
- `audio_client_settings`: per-user local playback prefs (volume/mute).

### Library indexing
- Server scans configured audio directory and indexes `.mp3`, `.wav`, `.ogg` files.
- Category is inferred from path/name (`combat`, `tense`, `calm`, `manual`, else `normal`).
- Missing files disable stale metadata entries; files without explicit metadata are indexed automatically.

### Sync model
- Time-based synchronization via `startedAtUtc + startOffsetSeconds`.
- Clients compute playback position from server timestamp snapshot.
- This avoids heavy server-side position writes while keeping usable sync quality.

### Track transitions
- Session state exposes fixed fade duration (`fadeMilliseconds`, default 1800).
- Track switch sets transition playback state, then settles into playing state on subsequent sync.

### Audio protocol commands
- State/mode:
  - `audio.state.get`
  - `audio.state.sync`
  - `audio.mode.get`
  - `audio.mode.set`
  - `audio.override.clear`
- Library/tracks:
  - `audio.library.get`
  - `audio.track.select`
  - `audio.track.next`
  - `audio.track.reload`
- Client local settings:
  - `audio.clientSettings.get`
  - `audio.clientSettings.set`

### Integration points
- Combat start/end/new round triggers audio policy sync.
- Music mode/track updates emit system messages into the shared session chat stream.

### Client behavior
- Admin can control mode/category/track/reload and view full library/state.
- Player gets synchronized playback state and only controls local volume + mute.
- Client sync currently uses polling/refresh-after-actions for stability on current TCP+JSON transport.

## Final Integration Layer (visibility, notes, references, updater, backup, diagnostics)

### Character visibility
- Added dedicated visibility commands and server-side public-view projection:
  - `visibility.get`
  - `visibility.update`
  - `character.publicView.get`
  - `character.visibleToMe.get`
- Server remains authoritative for hidden-field filtering. Owner/admin/superadmin get full details; other users get filtered DTO.
- Race/height/inventory hiding is gated by `AllowAdvancedVisibilityOverrides` and remains disabled by default.

### Notes subsystem
- Added persistent text notes with typed targets and visibility:
  - Personal, AdminOnly, SharedWithOwner, SessionShared.
- Commands:
  - `notes.create`
  - `notes.list`
  - `notes.get`
  - `notes.update`
  - `notes.archive`

### Reference data subsystem
- Added world-bound reference entries (`references` collection) with revision and archive support.
- SuperAdmin-only editing flows:
  - `reference.list/get/create/update/archive/reload`
- Intended reference types include races/classes/skills/states/settlements/factions/reputation/item templates and audio links (by `ReferenceType`).

### Updater/version layer
- Added update/version API commands:
  - `update.version.get`
  - `update.manifest.get`
  - `update.client.downloadInfo`
- `Nri.Updater` now performs practical flow:
  - reads local version,
  - downloads manifest,
  - updates files,
  - writes version,
  - launches selected client target.

### Backup/restore/export
- Added backup commands:
  - `backup.create`
  - `backup.list`
  - `backup.restore`
  - `backup.export`
- Strategy: logical snapshot serialized in `backups` collection with optional JSON export.
- `backup.restore` is restricted to SuperAdmin due destructive risk.

### Admin tools & diagnostics
- Added admin operations:
  - `admin.locks.list`
  - `admin.locks.forceRelease`
  - `admin.server.status`
  - `admin.sessions.list`
  - `admin.diagnostics.get`
- AdminClient includes dedicated final panels for visibility, notes, reference data, backups and diagnostics.

### Integration notes
- Important admin/audio/combat actions continue to publish system chat messages through shared chat pipeline.
- Runtime gameplay data, reference definitions, notes, update metadata, and backups are kept in separate collections.

### Simplifications still present
- Backup is logical JSON snapshot (not filesystem-level Mongo dump).
- Updater transport currently uses manifest/file download flow and assumes reachable feed URLs from config.
- Reference schema is generic (`DataJson`) to preserve extensibility without rigid typed editors in this stage.
