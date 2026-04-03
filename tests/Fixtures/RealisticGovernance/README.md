# Realistic Governance Fixtures

This directory contains realistic governance fixture documents used in tests for the `steergen` pipeline.

## Structure

- `global/constitution.md` — A global governance constitution with foundational rules (CORE-xxx) covering test coverage, security, documentation, and dependency hygiene.
- `project/project-steering.md` — A project-level steering document that extends and overlays the global constitution with domain-specific guidance (API versioning, data validation, observability).

## Usage

These fixtures are consumed by integration and property-based tests to verify that the steering document transformation pipeline correctly parses frontmatter, extracts `:::rule` blocks, merges global and project-level rules, and renders output templates without data loss or corruption.
