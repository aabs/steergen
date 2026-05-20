# Pack Infrastructure Security Analysis

## Overview

This document provides an explicit misuse and abuse analysis for the Custom Template Packs and Rules Packs feature. It covers attack vectors relevant to the pack infrastructure, documents implemented mitigations, and identifies residual risks with recommendations.

**Scope:** Template packs (local and GitHub-sourced), rules packs (GitHub-sourced), pack download and caching, template resolution, and rules loading/merge.

**Requirements coverage:** 14.1–14.8

---

## 1. Template Injection (Prompt-Injection-Style Payloads)

### Attack Vector

A malicious template pack author could craft Scriban template content designed to:
- Execute arbitrary code on the user's machine during template rendering
- Exfiltrate data from the rendering context (environment variables, file paths, secrets)
- Inject malicious content into generated output files that downstream tools interpret as instructions

### Analysis

Scriban is a text templating engine that operates in a sandboxed evaluation model. It does **not** provide:
- Filesystem access (no `File.Read`, `File.Write`, or equivalent)
- Process execution (no `System.Diagnostics.Process` or shell invocation)
- Network access (no HTTP clients or socket operations)
- Reflection or dynamic type loading

The Scriban engine evaluates expressions against an explicitly constructed render model. Only fields explicitly exposed in the model (`rules`, `targetId`, `filePath`, `formatOptions`) are accessible to template expressions. There is no mechanism for a template to escape the render model boundary.

### Threat: Output Poisoning

A template could generate output that contains prompt-injection payloads targeting downstream AI tools (Kiro, Copilot, etc.). For example, a template could emit steering rules containing `<!-- ignore all previous instructions -->` or similar injection attempts.

**Assessment:** This is a supply-chain trust issue rather than a runtime execution issue. The generated output is deterministic and inspectable. Users can validate output via `steergen validate` and review generated files before committing.

### Mitigations Implemented

| Mitigation | Requirement |
|------------|-------------|
| All template content parsed exclusively through Scriban engine — no arbitrary code execution | 14.1 |
| Render model exposes only declared fields; no ambient environment access | 14.1 |
| Template validation (`steergen validate`) reports syntax errors with file path and line number | 6.1, 6.2 |
| File size limit (1 MB) prevents resource exhaustion during parsing | 14.2 |

### Residual Risk

- **Output poisoning:** Low severity. Mitigated by deterministic output and user review. Users should review generated output before committing, especially when adopting new template packs.
- **Scriban engine vulnerabilities:** Low likelihood. Scriban is a mature, widely-used library. Pinned to version 7.0.6 with vulnerability scanning in CI.

---

## 2. Rule Document Injection

### Attack Vector

A malicious rules pack author could craft Markdown documents with YAML frontmatter designed to:
- Exploit parser vulnerabilities in the YAML frontmatter parser (YamlDotNet deserialization attacks)
- Inject content that bypasses validation and produces unexpected merge behaviour
- Include frontmatter fields that trigger unintended code paths

### Analysis

Rules pack documents are parsed by the existing `SteeringMarkdownParser`, which:
- Extracts YAML frontmatter using YamlDotNet with a strict, typed deserialization model
- Parses `:::rule` blocks as structured content with known field schemas
- Validates all parsed documents through `SteeringValidator` before they enter the merge pipeline

YamlDotNet is configured for safe deserialization — no type discriminators, no arbitrary object instantiation, no `!!python/object` or equivalent unsafe tags. The parser expects specific known fields and ignores unknown fields.

### Mitigations Implemented

| Mitigation | Requirement |
|------------|-------------|
| All rule document content parsed exclusively through the existing steering document parser | 14.6 |
| Strict typed deserialization — no arbitrary object construction from YAML | 14.6 |
| Full validation via `SteeringValidator` before documents enter the merge pipeline | 11.3 |
| File size limit (1 MB) on individual steering document files | 14.7 |
| Diagnostic reporting for validation failures with pack name and file path | 11.4 |

### Residual Risk

- **YamlDotNet vulnerabilities:** Low likelihood. Library is pinned, scanned, and uses safe deserialization patterns.
- **Semantic injection:** A rules pack could declare rules with IDs that collide with project-local rules, effectively overriding local governance. Mitigated by scope-based precedence (project-local always wins) and duplicate-ID warnings.

---

## 3. Path Traversal in Downloaded Archives

### Attack Vector

A malicious GitHub repository could publish a tarball containing entries with path traversal sequences designed to:
- Write files outside the expected cache directory (e.g., `../../../.ssh/authorized_keys`)
- Overwrite system files or other cached packs
- Place executable files in locations where they would be automatically executed

### Analysis

GitHub archive tarballs (`/archive/{ref}.tar.gz`) contain entries prefixed with `{repo}-{ref}/`. A crafted repository could include files with names like:
- `../../etc/cron.d/malicious`
- `repo-main/../../../home/user/.bashrc`
- Entries with absolute paths (`/etc/passwd`)

### Mitigations Implemented

| Mitigation | Requirement |
|------------|-------------|
| All archive entry paths validated for `../` sequences before extraction | 14.3 |
| Entries resolving outside the expected pack directory structure are rejected | 14.4 |
| Absolute paths in archive entries are rejected | 14.4 |
| Extraction to temporary directory with validation before atomic swap into cache | 4.8 |
| Failed validation discards the entire download — no partial extraction persists | 4.7 |

### Implementation Detail

The `PackDownloader` validates each archive entry path by:
1. Checking for literal `../` sequences in the entry name
2. Resolving the full path and verifying it remains within the target extraction directory
3. Rejecting entries with absolute paths (starting with `/` or drive letter on Windows)

### Residual Risk

- **Platform-specific path tricks:** Windows alternate data streams (`:` in filenames), reserved device names (`CON`, `NUL`). Low severity — these don't escape the cache directory but could cause extraction failures. The atomic replacement pattern ensures partial failures don't corrupt the cache.

---

## 4. Symlink-Based Escape Attempts

### Attack Vector

A malicious pack directory (either local or cached from GitHub) could contain symbolic links designed to:
- Point template files to sensitive locations outside the pack directory (e.g., `/etc/shadow`, `~/.ssh/id_rsa`)
- Create circular symlink chains causing infinite loops during file discovery
- Point to other packs' directories to create confusion about template provenance

### Analysis

Symlinks in pack directories could allow a template resolution or rules file discovery operation to read files outside the intended pack boundary, potentially exposing sensitive data in generated output.

### Mitigations Implemented

| Mitigation | Requirement |
|------------|-------------|
| `TemplateResolver` does not follow symbolic links when resolving template files | 14.5 |
| `RulesPackLoader` does not follow symbolic links when discovering `.md` files | 14.8 |
| File attributes checked before reading — symlinks are skipped | 14.5, 14.8 |
| Ordinal file path comparison used for deterministic enumeration (no symlink resolution) | 5.4 |

### Implementation Detail

Both `TemplateResolver` and `RulesPackLoader` check `FileAttributes` for the `ReparsePoint` flag before reading any file. Files identified as symbolic links or junction points are silently skipped during discovery and resolution.

### Residual Risk

- **Hard links:** Hard links cannot be detected via file attributes on all platforms. However, hard links cannot point outside the filesystem volume and cannot reference directories, limiting their attack surface. On extraction from tarballs, hard links within the archive are resolved to regular files.
- **TOCTOU (time-of-check-time-of-use):** A symlink could theoretically be created between the attribute check and the file read. This requires local filesystem write access to the cache directory, which implies the attacker already has code execution on the machine. Accepted as out-of-scope for this threat model.

---

## 5. Denial-of-Service via Oversized Files

### Attack Vector

A malicious pack could contain:
- Individual template or rule files exceeding reasonable size, causing memory exhaustion during parsing
- A large number of small files causing excessive I/O and processing time
- Compressed archives with high compression ratios (zip bombs / tar bombs) that expand to enormous size on disk

### Analysis

Without size limits, a single multi-gigabyte template file could exhaust process memory during Scriban parsing. A tarball with thousands of files could cause excessive disk I/O during extraction.

### Mitigations Implemented

| Mitigation | Requirement |
|------------|-------------|
| Individual template files rejected if > 1,048,576 bytes (1 MB) | 14.2 |
| Individual steering document files rejected if > 1,048,576 bytes (1 MB) | 14.7 |
| Size check performed before file content is read into memory | 14.2, 14.7 |
| Diagnostic error emitted with file path when size limit is exceeded | 14.2, 14.7 |

### Residual Risk

- **Archive decompression bombs:** The current implementation extracts the full archive before validating individual files. A tarball containing many files just under 1 MB each could still consume significant disk space. Mitigation: extraction to a temporary directory with atomic swap means disk space is reclaimed on failure.
- **File count explosion:** No explicit limit on the number of files in a pack. A pack with thousands of small files would be processed but could be slow. This is a usability issue rather than a security issue — the user chose to configure this pack.
- **Network bandwidth:** Large archives consume bandwidth during download. Mitigated by caching (download happens once) and immutable SHA pinning (pinned packs skip re-download).

---

## 6. Supply-Chain Risks from Unauthenticated GitHub Downloads

### Attack Vector

The pack download mechanism uses unauthenticated public GitHub archive URLs. This creates several supply-chain risks:

#### 6.1 Pack Substitution

An attacker who gains control of a GitHub repository (compromised credentials, social engineering) could push malicious content that would be downloaded by all consumers on next update.

#### 6.2 Typosquatting

An attacker could create repositories with names similar to legitimate packs (e.g., `acme-corp/steergen-tempaltes` vs `acme-corp/steergen-templates`) hoping users misconfigure their source reference.

#### 6.3 Branch Mutability

When a pack is referenced by branch name (e.g., `ref: main`), the content can change at any time without the consumer's knowledge. A compromised repository could push malicious content to a branch that consumers are tracking.

#### 6.4 No Signature Verification

Downloaded archives are not cryptographically verified. There is no mechanism to confirm that the archive content matches what the repository owner intended to publish.

#### 6.5 Man-in-the-Middle

Although GitHub archive URLs use HTTPS (providing transport encryption), there is no content-level integrity verification beyond what TLS provides.

### Mitigations Implemented

| Mitigation | Description |
|------------|-------------|
| SHA pinning detection | 40-character lowercase hex refs are treated as immutable pins, skipping re-download |
| Pinning recommendation | Diagnostic warning emitted when branch refs are used, recommending SHA or tag pinning |
| Atomic replacement | Existing cache preserved on download failure — a failed or corrupted download cannot destroy a known-good cached version |
| Manifest validation | Downloaded archives must contain a valid `pack.yaml` before being committed to cache |
| Deterministic resolution | No network requests during `steergen run` — only cached content is used at generation time |
| HTTPS transport | All downloads use `https://github.com/` URLs with TLS verification |

### Recommendations for Users

1. **Pin to commit SHA:** Use full 40-character commit SHAs in `ref` fields for production configurations. This ensures content immutability and prevents silent updates.
   ```yaml
   rulesPacks:
     - source: "github:acme-corp/baseline-rules"
       ref: "abc123def456789012345678901234567890abcd"  # Pinned SHA
   ```

2. **Review before update:** Run `steergen update --rules` or `steergen update --templates` deliberately, then review changes in generated output before committing.

3. **Verify repository ownership:** Confirm the GitHub repository owner matches the expected organisation before configuring a pack source. Check for typosquatting variants.

4. **Use tags over branches:** When SHA pinning is impractical, prefer version tags (`ref: v1.2.3`) over branch names (`ref: main`). Tags are conventionally immutable (though not enforced by Git).

5. **Audit pack content:** After initial download, inspect the cached pack content at `~/.steergen/packs/` or `~/.steergen/rules/` before running generation.

### Residual Risk

- **No signature verification:** There is no GPG signature or Sigstore verification of pack content. This is a known limitation. Users must rely on GitHub's access controls and their own review processes.
- **Repository compromise:** If a pack source repository is compromised, pinned SHA refs remain safe (content is immutable), but branch or tag refs could serve malicious content. Mitigation: pinning recommendation and atomic cache preservation.
- **No content hash in configuration:** The configuration does not store a content hash alongside the ref, so there is no way to detect if a tag was force-pushed to different content. Recommendation: use SHA pinning for critical packs.

---

## 7. Summary of Implemented Mitigations

| Category | Mitigation | Component |
|----------|-----------|-----------|
| Template injection | Scriban sandboxed execution — no arbitrary code, no filesystem/network access | `TemplateResolver` |
| Rule injection | Typed YAML deserialization, full validation pipeline | `RulesPackLoader` |
| Path traversal | `../` detection, absolute path rejection, boundary validation | `PackDownloader` |
| Symlink escape | `FileAttributes` check, symlinks not followed | `TemplateResolver`, `RulesPackLoader` |
| Denial of service | 1 MB file size limit on templates and rule documents | `TemplateResolver`, `RulesPackLoader` |
| Supply chain | SHA pinning, pinning recommendations, atomic replacement, manifest validation | `PackDownloader` |
| Cache integrity | Atomic replacement — download to temp, validate, then swap | `PackDownloader` |
| Determinism | No network requests during generation; cached content only | `TemplateResolver`, `RulesPackLoader` |

---

## 8. Threat Model Summary

| Threat | Likelihood | Impact | Mitigation Effectiveness | Residual Risk |
|--------|-----------|--------|--------------------------|---------------|
| Arbitrary code execution via templates | Very Low | Critical | High (Scriban sandbox) | Scriban CVE |
| Path traversal file write | Low | High | High (multi-layer validation) | Platform-specific edge cases |
| Symlink-based data exfiltration | Low | Medium | High (attribute check) | Hard links, TOCTOU |
| Memory exhaustion via large files | Low | Medium | High (1 MB limit) | Archive-level bombs |
| Repository compromise / substitution | Medium | High | Medium (SHA pinning optional) | Unpinned refs vulnerable |
| Typosquatting | Low | High | Low (user responsibility) | No automated detection |
| Output poisoning (prompt injection in generated files) | Medium | Medium | Low (user review) | Requires manual inspection |

---

## 9. Future Considerations

The following enhancements could further reduce residual risk in future versions:

1. **Content hash pinning:** Store a SHA-256 hash of the pack content in configuration alongside the Git ref, enabling detection of force-pushed tags.
2. **Pack signature verification:** Support Sigstore or GPG signatures on pack manifests, allowing cryptographic verification of pack authorship.
3. **Archive size limits:** Enforce a maximum total archive size during download to mitigate decompression bombs.
4. **File count limits:** Enforce a maximum number of files per pack to prevent file-count-based DoS.
5. **Pack registry:** A curated registry of verified packs with namespace reservation to prevent typosquatting.
6. **Allowlist/blocklist:** Configuration-level allowlists for permitted pack sources, enabling organisational policy enforcement.
