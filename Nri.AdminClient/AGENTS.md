# AdminClient UI Rules

- Admin layouts may be denser than PlayerClient, but must remain human-readable and task-oriented.
- Use shared resources first, then `AdminTheme.xaml` as a semantic override.
- Put technical identifiers, raw data, audit details, and diagnostics behind explicit Advanced/Diagnostics disclosure.
- Destructive actions require confirmation and a clear target name; ask for a reason when the domain needs one.
- Reference selection must use `NriReferencePicker` or an equivalent searchable picker, not a normal-mode ID textbox.
- Preserve loading, empty, error, validation, success, and unsaved states.
