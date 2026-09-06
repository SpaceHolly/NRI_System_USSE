# PlayerClient UI Rules

- Prioritize simplicity, immersion, and player-safe information.
- Use shared resources first, then `PlayerTheme.xaml` as a semantic override.
- Never expose GM-only/server-only data, internal identifiers, DTO names, protocol commands, or raw projections.
- Keep write controls absent when a route is read-only.
- Prefer clear Russian labels and grouped player-facing facts over schema-shaped forms.
- Technical data belongs only in explicit diagnostics available to authorized development roles.
