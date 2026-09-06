# NRI System USSE Engineering Contract

## User-facing WPF changes

For every user-facing WPF change:

1. Identify the primary user and primary task before editing XAML.
2. Reuse `Nri.Ui.Wpf` resources, controls, and page patterns.
3. Do not expose internal IDs, DTO names, command names, collection names, schema names, or raw JSON in normal mode.
4. Use searchable reference pickers instead of manual ID input.
5. Use progressive disclosure.
6. Keep at most three persistent content regions.
7. Implement loading, empty, error, validation, success, and unsaved states where applicable.
8. Do not place unrelated dashboard widgets inside feature routes.
9. Run UI static, text, layout, task-flow, and screenshot quality gates for changed UI.
10. Do not modify a quality gate merely to make a feature pass.
11. A debug-like user-facing screen is NOT PASS even when CRUD works.

Existing legacy UI is debt, not a design precedent. A feature task must not expand the legacy baseline silently.
