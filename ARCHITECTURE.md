# NRI System Architecture (.NET Framework 4.8.1)

## Что реализовано на текущем этапе

### Сервер
- Рабочая подсистема заявок (Action Requests + Dice Requests) с хранением в MongoDB.
- Полный request workflow:
  - create
  - cancel
  - list mine
  - list pending
  - get details
  - approve / reject
  - history
- Реальные статусы заявок: `Pending`, `Approved`, `Rejected`, `Cancelled`, `Expired`, `Archived`.
- Серверная проверка эквивалентности действий через **fingerprint**.
- Ограничения:
  - нельзя держать одновременно две pending заявки на одно и то же действие;
  - после 2 отклонений эквивалентного действия повторная отправка блокируется.
- Dice subsystem:
  - отдельные компоненты `DiceFormulaParser`, `DiceFormulaValidator`, `DiceRollExecutor`;
  - безопасный разбор формул `XdY[+/-Z]`;
  - лимиты на количество кубов/граней/модификатор;
  - итоговый roll рассчитывается **только на сервере** при approve;
  - результат сохраняется в заявке (rolls/modifier/total/visibility/approvedBy/approvedAt).
- Visibility rules для бросков применяются на сервере.

### Клиенты
- PlayerClient:
  - создание dice-заявки;
  - выбор visibility (Public / PlayerShadow / AdminOnlyShadow);
  - просмотр своих заявок и статусов;
  - отмена pending заявки;
  - просмотр видимой ленты бросков.
- AdminClient:
  - список pending заявок;
  - approve/reject с комментарием;
  - просмотр истории заявок;
  - просмотр общей ленты бросков.

## Fingerprint подход
Эквивалентность действий определяется детерминированно по строке:
- `actionType`
- `actorUserId`
- `characterId`
- `normalizedPayload`

Формат: `action|user|character|payload` (нижний регистр, trim).

Для dice payload берётся нормализованная формула + visibility.

## Команды протокола (добавленные на этом этапе)
- Requests:
  - `request.create`
  - `request.cancel`
  - `request.list.mine`
  - `request.list.pending`
  - `request.get.details`
  - `request.approve`
  - `request.reject`
  - `request.history`
- Dice:
  - `dice.request`
  - `dice.history`
  - `dice.visibleFeed`
  - `dice.get.details`

## Visibility бросков
- `Public`: доступно всем авторизованным игрокам и админам.
- `PlayerShadow`: доступно только автору броска и админам.
- `AdminOnlyShadow`: доступно только admin/superadmin.

## MongoDB коллекции по заявкам
- `action_requests`
- `dice_requests`

Также продолжают использоваться:
- `accounts`, `profiles`, `characters`, `sessions`, `locks`, `audit_logs`, `chat_messages`, `audio_states`.

## Автообновление
Используется polling (умеренный интервал 10 секунд) + refresh after action.
Это выбранный компромисс на текущем этапе вместо push/stream, чтобы сохранить стабильный TCP request/response контракт и не усложнять протокол до отдельной event-шины.

## Что пока остаётся следующим этапом
- Полная интеграция request workflow в бой/инициативу/навыки/чат.
- Расширенный UI фильтр/поиск заявок (сортировки, сложные фильтры).
- Event-driven push обновления вместо polling.
