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
