# Diagnostic Codes

Steergen emits structured diagnostics when configuration, validation, or download issues are detected. Each diagnostic has a unique code, a severity level, and a human-readable message. This document lists all diagnostic codes, their meaning, and how to resolve them.

## Exit Codes

| Code | Meaning |
|------|---------|
| `0`  | Success |
| `2`  | Configuration or validation error (non-recoverable without user action) |

---

## Template Pack Diagnostics (TP001–TP011)

### TP001 — Template pack path does not exist

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | The `templatePack.localPath` configured in `steergen.config.yaml` does not exist on the filesystem. |

**Remediation:**

1. Verify the path in your `steergen.config.yaml` under `templatePack.localPath`.
2. Ensure the directory exists and is accessible from the working directory where `steergen` is invoked.
3. If the path is relative, it is resolved relative to the config file location.

---

### TP002 — Template file exceeds size limit

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | A template file in the configured pack exceeds the 1 MB (1,048,576 bytes) maximum size. |

**Remediation:**

1. Identify the oversized template file from the diagnostic message.
2. Reduce the file size below 1 MB. Template files should contain Scriban template logic, not large embedded data.
3. If the file legitimately needs to be large, consider splitting it into multiple templates composed via Scriban `include`.

---

### TP003 — Template file contains Scriban syntax errors

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | A template file in the pack cannot be parsed by the Scriban template engine. |

**Remediation:**

1. Check the file path, line number, and error description in the diagnostic output.
2. Fix the Scriban syntax error in the template file. Common issues include unclosed `{{` blocks, invalid expressions, and mismatched `if`/`end` pairs.
3. Run `steergen validate` to confirm the fix.

---

### TP004 — Template pack missing pack.yaml (legacy mode)

| | |
|---|---|
| **Severity** | Warning |
| **Condition** | The configured template pack directory does not contain a `pack.yaml` manifest file. The pack is loaded in legacy mode without version constraints. |

**Remediation:**

1. Add a `pack.yaml` file to the root of your template pack directory:

```yaml
name: "my-templates"
version: "1.0.0"
minSteergenVersion: "1.5.0"
targets:
  - kiro
  - speckit
```

2. Declaring a manifest enables version compatibility checks and target-scoped filtering.

---

### TP005 — Template pack version incompatible

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | The template pack's `minSteergenVersion` in `pack.yaml` is higher than the running Steergen version. |

**Remediation:**

1. Upgrade Steergen to a version that satisfies the pack's `minSteergenVersion`:

```bash
dotnet tool update --global aabs.steergen
```

2. Alternatively, use an older version of the template pack that is compatible with your current Steergen version.

---

### TP006 — Template pack contains files for undeclared target

| | |
|---|---|
| **Severity** | Warning |
| **Condition** | The template pack contains template files organised under a target ID directory that is not listed in the pack's `targets` field in `pack.yaml`. |

**Remediation:**

1. Add the target ID to the `targets` list in `pack.yaml` if the templates are intentional.
2. Remove the extraneous template files if they are not needed.
3. If the pack is intended to provide templates for all targets, remove the `targets` field entirely from `pack.yaml`.

---

### TP007 — Configured GitHub pack not in local cache

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | A GitHub-sourced template pack is configured but has not been downloaded to the local cache. |

**Remediation:**

1. Download the template pack:

```bash
steergen update --templates
```

2. If the download fails, verify the repository URL and ref are correct in your configuration.
3. Ensure you have network access to `github.com`.

---

### TP008 — Template pack uses branch ref (recommend pinning)

| | |
|---|---|
| **Severity** | Warning |
| **Condition** | The configured template pack `ref` is a branch name rather than a tag or commit SHA. Branch refs can change over time, leading to non-deterministic template resolution. |

**Remediation:**

1. Pin the template pack to a specific tag or commit SHA for deterministic builds:

```yaml
templatePack:
  source: "github:acme-corp/steergen-templates"
  ref: "v2.1.0"  # tag — preferred
```

Or pin to a full 40-character commit SHA:

```yaml
templatePack:
  source: "github:acme-corp/steergen-templates"
  ref: "abc123def456789012345678901234567890abcd"
```

---

### TP009 — Provided target's defaultLayout file missing

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | A template pack declares a `providedTargets` entry whose `defaultLayout` file does not exist within the pack directory. |

**Remediation:**

1. Check the `defaultLayout` path in the pack's `pack.yaml` under `providedTargets`.
2. Ensure the referenced layout YAML file exists at the specified relative path within the pack.
3. If you are the pack author, create the missing layout file or correct the path.

---

### TP010 — Registered target not available (pack removed)

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | A target listed in `registeredTargets` in `steergen.config.yaml` was provided by a template pack that has been removed. The target is no longer available. |

**Remediation:**

1. Re-add the template pack that provides the target:

```bash
steergen template-pack add github:owner/repo --ref v1.0.0
```

2. Or remove the target from `registeredTargets` if it is no longer needed:

```bash
steergen target remove <targetId>
```

---

### TP011 — Target ID already registered (duplicate)

| | |
|---|---|
| **Severity** | Warning |
| **Condition** | A template pack declares a `providedTargets` entry with a target ID that is already registered (either as a built-in target or from another pack). |

**Remediation:**

1. If the pack is intended to override an existing target's templates, use the `targets` field instead of `providedTargets`.
2. If this is a naming collision, contact the pack author to use a unique target ID.
3. The first registration wins — the duplicate is ignored.

---

## Rules Pack Diagnostics (RP001–RP007)

### RP001 — Rules pack missing pack.yaml

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | A configured rules pack directory (in the local cache) does not contain a `pack.yaml` manifest file. |

**Remediation:**

1. If you are the pack author, add a `pack.yaml` to the root of your rules pack:

```yaml
name: "my-rules"
version: "1.0.0"
minSteergenVersion: "1.5.0"
scope: global
```

2. If you are a consumer, re-download the pack in case the cache is corrupted:

```bash
steergen update --rules --force
```

---

### RP002 — Rules pack version incompatible

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | The rules pack's `minSteergenVersion` in `pack.yaml` is higher than the running Steergen version. |

**Remediation:**

1. Upgrade Steergen:

```bash
dotnet tool update --global aabs.steergen
```

2. Or use an older version of the rules pack that is compatible with your current Steergen version.

---

### RP003 — Rules pack document fails validation

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | A steering document within the rules pack failed validation (e.g., malformed YAML frontmatter, invalid rule block syntax, missing required fields). |

**Remediation:**

1. Check the diagnostic output for the pack name, file path, and specific validation errors.
2. If you are the pack author, fix the document using the same format as local steering documents.
3. If you are a consumer, report the issue to the pack maintainer or pin to a known-good version.

---

### RP004 — Duplicate rule ID across same-scope packs

| | |
|---|---|
| **Severity** | Warning |
| **Condition** | Two or more rules packs at the same scope level declare rules with the same rule ID. The rule from the pack declared earlier in the `rulesPacks` list takes precedence. |

**Remediation:**

1. Review the `rulesPacks` order in `steergen.config.yaml` — earlier entries win within the same scope.
2. If the duplicate is unintentional, contact the pack authors to resolve the ID collision.
3. If intentional, reorder the `rulesPacks` list so the preferred pack appears first.

---

### RP005 — Configured rules pack not in local cache

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | A configured rules pack has not been downloaded to the local cache. |

**Remediation:**

1. Download all configured rules packs:

```bash
steergen update --rules
```

2. If the download fails, verify the repository URL and ref in your configuration.
3. Ensure you have network access to `github.com`.

---

### RP006 — Rules pack uses branch ref (recommend pinning)

| | |
|---|---|
| **Severity** | Warning |
| **Condition** | A configured rules pack `ref` is a branch name rather than a tag or commit SHA. Branch refs can change over time, leading to non-deterministic rule resolution. |

**Remediation:**

1. Pin the rules pack to a specific tag or commit SHA:

```yaml
rulesPacks:
  - source: "github:acme-corp/baseline-rules"
    ref: "v1.0.0"  # tag — preferred
```

Or use a full 40-character commit SHA:

```yaml
rulesPacks:
  - source: "github:acme-corp/baseline-rules"
    ref: "abc123def456789012345678901234567890abcd"
```

---

### RP007 — Rules pack document exceeds size limit

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | A steering document file within the rules pack exceeds the 1 MB (1,048,576 bytes) maximum size. |

**Remediation:**

1. Identify the oversized file from the diagnostic message.
2. Split large documents into multiple smaller files. Each file should cover a focused set of related rules.
3. Steering documents should contain rule definitions, not large embedded data.

---

## Download Diagnostics (DL001–DL004)

### DL001 — GitHub repository not accessible

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | The pack download failed due to an HTTP error when accessing the GitHub archive URL. The diagnostic includes the HTTP status code and repository URL. |

**Remediation:**

1. Verify the repository exists and is public: `https://github.com/{owner}/{repo}`
2. Check your network connectivity to `github.com`.
3. If the repository is private, note that Steergen only supports public repositories. Private repository access is not supported.
4. Common HTTP status codes:
   - `404` — Repository or ref does not exist. Check the owner, repo name, and ref value.
   - `403` — Rate limited or access denied. Wait and retry, or verify the repository is public.
   - `5xx` — GitHub server error. Retry after a short delay.

---

### DL002 — Downloaded archive missing pack.yaml

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | The downloaded GitHub archive was extracted successfully but does not contain a `pack.yaml` manifest file at the expected location. The download is discarded. |

**Remediation:**

1. Verify the repository contains a `pack.yaml` at its root (or at the configured `path` subdirectory).
2. If using a `path` field in your configuration, ensure the subdirectory within the repository contains `pack.yaml`.
3. Contact the pack author if the manifest is missing from the repository.

---

### DL003 — Archive contains path traversal sequences

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | The downloaded archive contains file entries with path traversal sequences (`../`) that would place files outside the expected pack directory. The archive is rejected for security reasons. |

**Remediation:**

1. Do not use this pack — it may be malicious or corrupted.
2. Report the issue to the pack maintainer.
3. If you control the repository, ensure no files or directory names contain `../` sequences.

---

### DL004 — Archive contains files outside expected structure

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | The downloaded archive contains file entries that resolve to paths outside the expected pack directory structure (e.g., absolute paths or entries escaping the root). The archive is rejected for security reasons. |

**Remediation:**

1. Do not use this pack — it may be malicious or corrupted.
2. Report the issue to the pack maintainer.
3. If you control the repository, ensure all files are contained within the repository root without absolute paths.

---

## Configuration Diagnostics (CFG001)

### CFG001 — Deprecated globalRoot field present

| | |
|---|---|
| **Severity** | Error |
| **Exit code** | 2 |
| **Condition** | The `steergen.config.yaml` file contains a `globalRoot` field. This field has been removed and its functionality is replaced by rules packs with `scope: global`. |

**Remediation:**

1. Remove the `globalRoot` field from `steergen.config.yaml`.
2. Convert your existing global rules directory into a rules pack:

   a. Add a `pack.yaml` to the root of your global rules directory:

   ```yaml
   name: "my-global-rules"
   version: "1.0.0"
   minSteergenVersion: "1.5.0"
   scope: global
   ```

   b. Publish the directory as a GitHub repository (or use it as a local path).

   c. Add the rules pack to your configuration:

   ```yaml
   rulesPacks:
     - source: "github:your-org/global-rules"
       ref: "v1.0.0"
       scope: global
   ```

3. Run `steergen update --rules` to download the pack.
4. Run `steergen run` to verify generation produces the expected output.

See the [migration guide](./migration-globalroot.md) for detailed step-by-step instructions.
