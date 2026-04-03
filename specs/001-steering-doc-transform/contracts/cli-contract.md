# CLI Contract

## Command Surface
- `steergen init <project-root> --target <target>...`
- `steergen update [--version <x.y.z>]`
- `steergen run [--target <target>...]`
- `steergen validate [--config <path>] [--profile <name>...]`
- `steergen inspect [--config <path>] [--profile <name>...]`
- `steergen target add <target-id>`
- `steergen target remove <target-id>`

## Exit Codes
- `0`: success
- `1`: validation error
- `2`: configuration/argument error
- `3`: target generation error

## Behavioral Rules
- `run` without `--target` executes all registered targets
- `run --target ...` executes only specified targets
- `target add` is idempotent and creates missing target folders
- `target remove` updates registration only and does not delete generated artifacts
- `update` without version resolves latest compatible template/metadata release
- `update --version` applies exact requested release or fails safely with diagnostics
- `update --version` accepts stable SemVer (`x.y.z`) and testing preview SemVer (`x.y.z-previewN`) values

## Diagnostics
- Default mode is quiet except command results/errors
- `--verbose` and `--debug` provide additional diagnostics to stderr
