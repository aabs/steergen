# Quickstart

## Prerequisites
- .NET 10 SDK installed
- Existing `steergen.config.yaml` (or initialize with `steergen init`)
- At least one registered target

## 1. Inspect target default layout YAML
Each built-in target ships a default layout profile in source. Use this as baseline before customizing.

Expected locations:
- `src/Steergen.Core/Targets/Speckit/default-layout.yaml`
- `src/Steergen.Core/Targets/Kiro/default-layout.yaml`
- `src/Steergen.Core/Targets/Agents/Copilot/default-layout.yaml`
- `src/Steergen.Core/Targets/Agents/Kiro/default-layout.yaml`

## 2. Add per-target override YAML
Create a user override file, for example `config/layout-overrides/speckit.yaml`, and include only fields you want to change.

Example override fragment:
```yaml
routes:
  - id: project-security
    scope: project
    explicit: true
    match:
      category: security
    destination:
      directory: "${projectRoot}/.speckit/security"
      fileName: "security-rules"
      extension: ".md"
  - id: catch-all
    scope: project
    explicit: true
    match:
      category: "*"
    destination:
      directory: "${projectRoot}/.speckit/${category}"
      fileName: "${category}-rules"
      extension: ".md"
fallback:
  mode: other-at-core-anchor
  fileBaseName: other
purge:
  roots:
    - "${projectRoot}/.speckit"
  globs:
    - "**/*.md"
```

## 3. Link override in config
Add per-target override path in `steergen.config.yaml`.

The `layoutOverridePath` may be a relative path (resolved from the config file's directory) or
an absolute path. Each target has its own independent override; targets without a
`layoutOverridePath` continue to use their built-in default layout.

Example (workspace-local convention):
```yaml
targets:
  - id: speckit
    enabled: true
    layoutOverridePath: config/layout-overrides/speckit.yaml   # relative to steergen.config.yaml
  - id: kiro
    enabled: true
    # no layoutOverridePath — uses built-in default
```

Example (user-home global convention):
```yaml
targets:
  - id: speckit
    enabled: true
    layoutOverridePath: /home/user/.config/steergen/speckit-layout.yaml   # absolute path
```

## 4. Validate before generation
```bash
steergen validate
```

Validation must fail if:
- Route rules are ambiguous
- Unknown variables are referenced
- Core anchor route is missing for any scope
- Paths resolve outside allowed roots

## 5. Run generation with deterministic routing
```bash
steergen run --target speckit
```

Expected behavior:
- Each rule resolves to exactly one destination.
- Rules not matched by specific routes use the catch-all route when configured.
- Unmatched rules route to `other.*` colocated with core-anchor destination.
- Only selected destination files are truncated, then repopulated deterministically.

## 6. Purge stale generated files
Purge uses configured target roots and globs; no prior manifest is required.

```bash
steergen purge --target speckit
```

Expected behavior:
- Only files matching configured purge globs under configured roots are eligible for deletion.
- Targets without purge globs report no-op and perform no deletions.
- Command reports removed and skipped files with reasons; non-success on unsafe purge conditions.

## 7. Verify routing syntax documentation surface
The release docs must include a framework-agnostic routing syntax reference under `docs/`.

Verification checklist:
- Grammar and variable syntax are documented without target-framework coupling.
- Deterministic precedence and single-destination constraints are documented.
- `core` anchor requirements and `other.*` fallback colocation behavior are documented.
- Worked examples include both matched and unmatched routing scenarios.

## 8. Run tests
```bash
dotnet test
```

Focus areas:
- Property tests for single-destination routing + determinism.
- Integration tests for fallback-to-other, deep-merge override behavior, and purge no-manifest operation.
