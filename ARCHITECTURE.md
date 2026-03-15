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
