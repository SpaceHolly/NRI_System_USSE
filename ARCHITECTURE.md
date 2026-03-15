# NRI System Architecture (.NET Framework 4.8.1)

## Что реально работает на этом этапе

### Сервер
- TCP JSON listener (multi-client, thread-per-connection).
- JSON framing: **1 сообщение = 1 строка JSON (newline-delimited)**.
- Command dispatcher с единым реестром обработчиков, auth-gate и централизованным error mapping.
- Реальные сервисы auth/profile/admin/characters/presence/locks.
- Расширенный Character API для чтения и редактирования.
- Серверная visibility filtering при выдаче character DTO.
- Серверные locks, привязанные к редактированию персонажа.
- MongoDB-репозитории и индексы.

### Клиенты
- AdminClient: рабочий shell с вкладками `PendingAccountsView`, `PlayersView`, `CharactersView`, `CharacterEditorView`, `LocksStatusView`, `ClassesView`, `SkillsView`.
- PlayerClient: рабочий shell с вкладками `ActiveCharacterView` (внутри `MainShellWindow`), `MyCharactersView`, `ClassesView`, `SkillsView`.
- Реальный TCP клиент в обоих приложениях.
- Runtime auth token state.
- Ручной refresh + авто-обновление polling таймером.
- Отображение offline состояния: **«Вы в режиме оффлайн»**.

## Новые/расширенные команды
- `admin.players.list`
- `character.get.details`
- `character.get.summary`
- `character.get.companions`
- `character.get.inventory`
- `character.get.reputation`
- `character.get.holdings`
- `character.update.basicInfo`
- `character.update.stats`
- `character.update.visibility`
- `character.update.money`
- `character.update.inventory`
- `character.update.reputation`
- `character.update.holdings`
- `lock.forceRelease`

(Плюс команды предыдущего этапа: auth/profile/admin account moderation/list/assign/transfer/archive/restore/presence/session/locks.)

## Visibility filtering
- Админ и superadmin видят полные данные.
- Владелец персонажа видит полные данные.
- Для других игроков сервер применяет filtering по `CharacterVisibilitySettings`.
- Никогда не скрываются: раса, рост, инвентарь.
- Скрытие описания/предыстории/статов/репутации применяется на сервере при построении DTO.

## Character edit locks
- AdminClient при открытии персонажа может брать lock (`lock.acquire`).
- При занятом lock сервер возвращает конфликт.
- Обычный release: `lock.release`.
- Force unlock только для `SuperAdmin`: `lock.forceRelease`.
- Lock status и метаданные (кто и до какого времени) доступны через `lock.status`.

## MongoDB коллекции
- `accounts`
- `profiles`
- `characters`
- `sessions`
- `locks`
- `audit_logs`
- `requests` (каркас)
- `chat_messages` (каркас)
- `audio_states` (каркас)

## Что пока ещё каркас
- Полный модуль заявок/бросков/боя/чата/музыки.
- Полное дерево классов и gameplay flow навыков.
- Realtime push-события (пока polling refresh).
