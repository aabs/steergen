# Governance and Compliance

This file captures the constitutional governance model. The constitution is the supreme authority for this project — it overrides any conflicting project conventions.

## Constitution Precedence

The constitution (`.specify/memory/constitution.md`) overrides conflicting project conventions. When in doubt, the constitution wins.

## Plan and Design Review Gates

- Plan reviews MUST pass constitution gates BEFORE design begins and AGAIN after design is complete.
- Task generation MUST include explicit tasks for:
  - Test-first / PBT-first implementation
  - Security analysis
  - Performance budget definition
  - Documentation updates

## Code Review and Release Gates

- Code review MUST block merge when constitutional obligations are unmet.
- Release review MUST block publish when constitutional obligations are unmet.
- Tagged release pipelines MUST build, run full tests, verify package metadata, and publish only after all checks pass.

## Constitutional Amendment Process

Amendments to the constitution require:

1. A documented proposal describing the change, rationale, and impact.
2. Approval from project maintainers.
3. Updates to affected templates and workflow guidance in the same change.
4. A version update to the constitution following semantic versioning:
   - MAJOR: Incompatible governance changes or principle removal/redefinition.
   - MINOR: New principle/section or materially expanded guidance.
   - PATCH: Clarifications, wording improvements, and non-semantic refinements.

## Rationale Reference

These are the reasons behind each constitutional principle. Use them to resolve ambiguity:

- **Security First**: This tool is distributed via NuGet — a single weak default can scale into ecosystem-level risk.
- **Correctness/Determinism**: Tooling reliability depends on predictable behavior in CI, local dev, and downstream integrations.
- **Test-First PBT**: PBT validates behavior classes and invariants more effectively than narrow examples, reducing latent defects in transformation tooling.
- **Performance**: Growth in users and corpus size is expected; regressions become major operational risks at scale.
- **Simplicity/Extensibility**: Ease of use is essential for adoption and reducing support burden.
- **No Plugin Loading**: Keeps supply-chain risk and runtime complexity lower than plugin systems while enabling rapid addition of new output platforms.
- **Documentation as Product Surface**: For CLI tooling, documentation is part of the API contract and directly impacts correctness of user workflows.
