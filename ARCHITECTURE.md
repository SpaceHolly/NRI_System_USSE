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
