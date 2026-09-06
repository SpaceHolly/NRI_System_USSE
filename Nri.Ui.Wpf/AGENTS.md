# Shared WPF Design System Rules

- Keep this project independent from server DTOs and client view models.
- Add semantic resources instead of feature-specific literal colors.
- Controls expose general dependency properties and commands; they do not know campaign entities.
- Every custom interactive control needs an accessible name contract, visible keyboard focus, and stable AutomationId support at the usage site.
- Keep control templates usable at 1366x768 and under simulated 150% layout scale.
- A new shared control requires a working Gallery example and focused task-flow/screenshot proof.
- Do not copy shared controls into feature projects.
