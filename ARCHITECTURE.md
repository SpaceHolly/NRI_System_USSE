# NRI System Architecture (.NET Framework 4.8.1)

## Что реально работает на текущем этапе

### Сервер (`Nri.Server`)
- TCP JSON сервер с **одновременной обработкой нескольких клиентов** (поток на подключение).
- Простой и стабильный framing: **один JSON envelope = одна строка** (`\n` delimiter).
- Connection manager и cleanup при disconnect.
- Command dispatcher с реестром handler'ов (без giant switch), централизованной обработкой ошибок и auth-gate.
- Рабочие сервисы:
  - регистрация / логин / логаут;
  - выдача и валидация server-side auth token;
  - профиль пользователя (get/update);
  - админ-подтверждение аккаунтов;
  - команды персонажей (минимум из этапа);
  - presence list для администратора;
  - entity lock acquire/release/status.
- MongoDB integration:
  - инициализация клиента и коллекций;
  - базовые индексы (login unique, token unique, owner index, unique lock per entity).
- Разделённые каналы логирования: debug / session / admin / audit.

### Клиенты (`Nri.AdminClient`, `Nri.PlayerClient`)
- Реальный TCP network client (connect/send/read response).
- Передача `RequestEnvelope` и получение `ResponseEnvelope`.
- Хранение `authToken` в клиентском session state.
- Минимальный command API для register/login/profile/characters и admin account commands.

## MongoDB коллекции
- `accounts`
- `profiles`
- `characters`
- `sessions` (presence/session states)
- `locks`
- `audit_logs`
- `requests` (каркас)
- `chat_messages` (каркас)
- `audio_states` (каркас)

## Поддерживаемые команды
- Auth:
  - `auth.register`
  - `auth.login`
  - `auth.logout`
- Profile:
  - `profile.get`
  - `profile.update`
- Admin accounts:
  - `admin.accounts.pending`
  - `admin.accounts.approve`
  - `admin.accounts.archive`
  - `admin.accounts.profile`
- Characters:
  - `character.list.mine`
  - `character.list.byOwner`
  - `character.get.active`
  - `character.create`
  - `character.archive`
  - `character.restore`
  - `character.transfer`
  - `character.assignActive`
- Presence/session:
  - `presence.list`
  - `session.validate`
- Locks:
  - `lock.acquire`
  - `lock.release`
  - `lock.status`

## Статусы аккаунта
- `PendingApproval`
- `Active`
- `Blocked`
- `Archived`

## Как запустить сервер
1. Настроить `Nri.Server/server.config.json`.
2. Убедиться, что MongoDB доступен по `Mongo.ConnectionString`.
3. Запустить `Nri.Server` (через Visual Studio / msbuild).
4. Клиенты подключаются к `ServerHost/ServerPort` из `client.config.json`.

## Конфигурация
- `server.config.json`:
  - host/port
  - mongo connection/database
  - log file paths
  - token lifetime
  - audio folder path
- `client.config.json`:
  - server host/port
- `updater.config.json`:
  - update channel/feed (каркас)

## Что пока оставлено каркасом
- Полная игровая механика (request workflow, бой, чат, музыка) — пока только data/repository каркас.
- Полноценные клиентские UI-сценарии (сейчас только интеграционный сетевой минимум + placeholders).
- Расширенный RBAC/ABAC и гибкий policy DSL.
- Расширенные migration/versioning сценарии для сложных апдейтов схем.
# NRI System Architecture Skeleton (.NET Framework 4.8.1)

## Projects
- **Nri.Shared**: Domain entities, transport contracts, protocol constants, permissions, config models.
- **Nri.Server**: Console host, TCP listener skeleton, command dispatcher, service/repository/auth stubs, split logging channels.
- **Nri.AdminClient**: WPF admin shell, navigation placeholders, network/session client stubs.
- **Nri.PlayerClient**: WPF player shell, navigation placeholders, network/session client stubs.
- **Nri.Updater**: Launcher/updater console skeleton with update feed configuration.

## Layering and responsibilities
1. **Domain models** live in `Nri.Shared.Domain` and include `SchemaVersion`, `Deleted`, `Archived` for migration and soft-delete strategy.
2. **Transport DTO/protocol** is isolated in `Nri.Shared.Contracts` with envelopes and command catalog for TCP+JSON.
3. **Server application layer** (`Nri.Server.Application`) owns command dispatching and business service contracts.
4. **Infrastructure layer** (`Nri.Server.Infrastructure`) contains repository abstractions and auth/session state stubs.
5. **UI clients** keep network and state logic separate from views (XAML + ViewModels + Networking folders).

## Key model coverage
Implemented baseline entities: user/account/profile/roles, world-campaign-session hierarchy, character with companion and inventory, holdings (multi-owner), reputation, wallet/currency map, class/skills, request types, chat, combat initiative, audio, entity locks and audit logs.

## JSON protocol
- `RequestEnvelope`: `Command`, `RequestId`, `AuthToken`, `SessionId`, `Payload`, `TimestampUtc`, `Version`.
- `ResponseEnvelope`: `RequestId`, `Status`, `ErrorCode`, `Message`, `Payload`, `TimestampUtc`, `Version`.
- Command catalog includes auth, session, character, requests, dice, combat, chat and audio command names.

## Security approach
- Roles: `Player`, `Observer`, `Admin`, `SuperAdmin`.
- Role-to-permission map in `AccessPolicy` for server-side checks.
- Clients are intentionally unaware of DB and cannot bypass server authorization.

## Logging structure
`ServerConfig.Logging` supports distinct channels:
- debug log
- session action log
- admin log
- audit log

## Configuration templates
- `Nri.Server/server.config.json`
- `Nri.AdminClient/client.config.json`
- `Nri.PlayerClient/client.config.json`
- `Nri.Updater/updater.config.json`

## What is intentionally stubbed for next stages
- Real TCP request loop and per-connection lifecycle.
- MongoDB 8.0 concrete repository implementations.
- Full authorization/session token lifecycle.
- Full game mechanics (combat resolution, dice engine, requests workflow).
- Production-grade updater logic and package verification.
- Rich WPF UI/UX and module-specific screens.

## Notable architectural decisions
- **Shared contracts and entities in one assembly** to keep protocol/domain coherent early; can be split later into `Domain` and `Contracts` assemblies without breaking app boundaries.
- **Server-first truth model**: all mutable actions route through command dispatcher stubs to enforce future centralized validation.
- **Dictionary-based money model** chosen for extensibility and denomination evolution without schema redesign.
