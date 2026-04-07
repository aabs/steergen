# How Steergen Works

This document explains the runtime flow of Steergen from a developer's point of view.
It is organized in three layers so you can start broad and drill down only as far as you need.

- High level: what the tool does end to end.
- Medium level: which components own each stage of the pipeline.
- Low level: the specific mechanics and implementation details that matter when modifying behavior.

## High Level

At a high level, `steergen run` does six things:

1. Resolve configuration and target selection.
2. Discover source steering documents from the configured roots.
3. Parse Markdown documents into an internal document and rule model.
4. Validate the full corpus and merge it into one resolved model.
5. Load a target layout, route each rule to exactly one destination, and build a write plan.
6. Hand each target a set of routed rules so the target can render and write its output files.

The important architectural point is that Steergen treats generation as a two-part problem:

- Routing decides where content belongs.
- Target components decide how content is rendered for a specific target ecosystem.

That split is what makes the dynamic layout engine work. The routing layer is target-aware enough to produce target-native file locations, but it does not know how to render Kiro or Speckit content. Rendering is still owned by the target component.

If you are orienting quickly, start in these files:

- [src/Steergen.Cli/Commands/RunCommand.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Cli/Commands/RunCommand.cs)
- [src/Steergen.Core/Parsing/SteeringMarkdownParser.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Parsing/SteeringMarkdownParser.cs)
- [src/Steergen.Core/Merge/SteeringResolver.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Merge/SteeringResolver.cs)
- [src/Steergen.Core/Generation/GenerationPipeline.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/GenerationPipeline.cs)
- [src/Steergen.Core/Generation/RouteResolver.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/RouteResolver.cs)
- [src/Steergen.Core/Targets/Kiro/KiroTargetComponent.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Targets/Kiro/KiroTargetComponent.cs)
- [src/Steergen.Core/Targets/Speckit/SpeckitTargetComponent.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Targets/Speckit/SpeckitTargetComponent.cs)

## Medium Level

### 1. CLI entry and configuration resolution

`RunCommand` is the operational entrypoint for generation.

Its responsibilities are:

- Resolve `--config`, `--global`, `--project`, `--output`, and `--target`.
- Load `steergen.config.yaml` when present.
- Determine the active roots.
- Determine which built-in targets should run.
- Build `TargetConfiguration` objects for those targets.
- Load Markdown files from the configured roots.
- Call `GenerationPipeline.RunAsync`.

Important implementation details:

- CLI arguments override config values for `globalRoot` and `projectRoot`.
- Explicit `--target` values override `registeredTargets` from config.
- Document discovery is recursive and only includes `*.md` files.
- The current working directory is the default base output path when `--output` is omitted.
- `outputPath` on target config is now treated as a legacy compatibility field; routed output location is driven by the layout plan and the selected output base.

The file that owns this is [src/Steergen.Cli/Commands/RunCommand.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Cli/Commands/RunCommand.cs).

### 2. Source document discovery

Steergen does not maintain an index or cache of source steering files. On each run it walks the configured roots and parses every Markdown file it finds.

The discovery logic lives inside `RunCommand.LoadDocuments` and is intentionally simple:

- If a root is null or missing, no documents are loaded from that root.
- Files are discovered with `Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)`.
- Paths are sorted ordinally before parsing to keep behavior deterministic.

This means a developer debugging missing output should check three things first:

- Was the correct root selected?
- Does the file end in `.md`?
- Is the file located under the expected root tree?

### 3. Parsing Markdown into the internal model

`SteeringMarkdownParser` turns a Markdown file into a `SteeringDocument`.

The parser has two major responsibilities:

- Parse YAML frontmatter into document metadata.
- Extract `:::rule ... :::` blocks into `SteeringRule` records.

The parser does not interpret arbitrary Markdown as rules. A document only contributes rules if it contains explicit fenced rule blocks. This is one of the most important practical details in the codebase.

Current parsing behavior:

- Frontmatter is optional.
- Invalid frontmatter does not crash parsing; it is ignored and the document still parses.
- Rules are detected by the regex `^:::rule\s+(.*?)\s*$`.
- Rule attributes are key-value pairs like `id="CORE-001"`.
- The rule body is every line until the terminating `:::`.

Parsed rule fields include:

- `Id`
- `Severity`
- `Category`
- `Domain`
- `Profile`
- `AppliesTo`
- `Tags`
- `Deprecated`
- `Supersedes`
- `PrimaryText`

The parser lives in [src/Steergen.Core/Parsing/SteeringMarkdownParser.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Parsing/SteeringMarkdownParser.cs).

### 4. Validation and merge into a resolved model

After parsing, `GenerationPipeline` runs the corpus through `SteeringValidator` before routing happens.

Validation covers:

- missing document IDs
- missing rule IDs
- invalid severities
- missing domains
- empty rule body text
- duplicate rule IDs across the corpus
- invalid `supersedes` references

If validation finds any errors, generation stops before any target output is written.

Once validation passes, `SteeringResolver` merges global and project documents into a `ResolvedSteeringModel`.

The resolved model is the canonical in-memory representation used by routing and target generation.

Key merge responsibilities:

- Track whether a rule came from global or project scope.
- Attach `InputFileStem` derived from the source filename.
- Filter by active profile.
- Build a source index keyed by document ID.

Two files own this stage:

- [src/Steergen.Core/Validation/SteeringValidator.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Validation/SteeringValidator.cs)
- [src/Steergen.Core/Merge/SteeringResolver.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Merge/SteeringResolver.cs)

### 5. Load layout and route rules to output paths

This is the core of the dynamic layout engine.

For each enabled target, `GenerationPipeline` does the following:

1. Load the built-in target layout YAML.
2. Deep-merge a user override YAML if one is configured.
3. Route every resolved rule through the target layout.
4. Build a deterministic write plan from the routing results.

The layout loader is [src/Steergen.Core/Configuration/LayoutOverrideLoader.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Configuration/LayoutOverrideLoader.cs).

It maps YAML into:

- `LayoutRootsDefinition`
- `RouteRuleDefinition`
- `FallbackRuleDefinition`
- `PurgePolicyDefinition`

Routing then happens in two layers:

- `RouteResolver` picks the best matching route for a single rule.
- `RoutePlanner` applies fallback behavior when no route matches.

The routing precedence is deterministic. A candidate route is selected by ordering on:

- explicit routes before non-explicit routes
- more specific match expressions before less specific ones
- lower configured order values
- declaration order in the YAML
- route ID as a final tie-breaker

If no route matches, `RoutePlanner` sends the rule to an `other.*` file colocated with the target's core anchor.

The main files here are:

- [src/Steergen.Core/Configuration/LayoutOverrideLoader.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Configuration/LayoutOverrideLoader.cs)
- [src/Steergen.Core/Generation/RouteResolver.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/RouteResolver.cs)
- [src/Steergen.Core/Generation/RoutePlanner.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/RoutePlanner.cs)

### 6. Build a write plan

The route planner produces one `RouteResolutionResult` per rule. `WritePlanBuilder` turns those rule-level decisions into file-level work.

Its responsibilities are:

- Ignore unresolved routes.
- Group all routed rules by destination path.
- Create one `WritePlanFile` per output file.
- Order files deterministically.
- Order content units deterministically by rule ID.

The important architectural idea is that the write plan is the boundary between routing and rendering.

Up to this point the system only knows:

- which target is being generated
- which rules belong in which file
- what the final destination path should be

It still does not know how the actual output text should look.

This logic lives in [src/Steergen.Core/Generation/WritePlanBuilder.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/WritePlanBuilder.cs).

### 7. Render and write target outputs

Each target component receives the resolved model, the target config, and a `WritePlan`.

The target component is responsible for:

- mapping rules into a target-specific render model
- choosing the appropriate Scriban template
- rendering the content
- writing the file to the planned path

The interface is [src/Steergen.Core/Targets/ITargetComponent.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Targets/ITargetComponent.cs).

The target contract is now plan-only. Routing chooses the destination file first, and targets implement `GenerateWithPlanAsync` to render content into those planned destinations.

The target then rebases the planned path through `PlannedOutputPathResolver` so absolute layout paths can be safely expressed relative to the selected output base.

That rebase logic is in [src/Steergen.Core/Generation/PlannedOutputPathResolver.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/PlannedOutputPathResolver.cs).

## Low Level

### End-to-end call chain

This is the actual runtime sequence a developer should hold in their head:

1. `RunCommand.RunAsync`
2. `LoadDocuments`
3. `SteeringMarkdownParser.Parse`
4. `GenerationPipeline.RunAsync`
5. `SteeringValidator.ValidateCorpus`
6. `SteeringResolver.Resolve`
7. `LayoutOverrideLoader.LoadAsync`
8. `RoutePlanner.Plan`
9. `WritePlanBuilder.Build`
10. `ITargetComponent.GenerateWithPlanAsync`
11. `EmbeddedTemplateProvider.GetTemplate`
12. `Scriban.Template.RenderAsync`
13. `File.WriteAllTextAsync`

That is the shortest accurate mental model of the system.

### Source document mechanics

### Discovery rules

Source documents come from two optional roots:

- `globalRoot`
- `projectRoot`

Those roots can come from:

- CLI arguments
- `steergen.config.yaml`

If both are absent, generation fails early.

Discovery is recursive and Markdown-only. Non-Markdown files are ignored completely.

### Parse rules that matter in practice

The parser is deliberately narrow.

It recognizes:

- YAML frontmatter at the top of the file
- `:::rule` blocks in the body

It does not recognize arbitrary headings, bullet lists, or prose as steering rules. If a file contains only plain Markdown with no `:::rule` blocks, it will parse as a document with zero rules.

That behavior matters because a target can appear to "run successfully" while generating no files if the corpus contains no parsed rules that survive validation and profile filtering.

### Metadata propagation

`SteeringResolver` enriches parsed rules with runtime metadata used later by routing.

Notable fields added during resolution:

- `InputFileStem`: derived from the source filename and used by layouts such as Kiro's `${inputFileStem}` destinations
- `SourceScope`: `Global` or `Project`, used by route scope matching

### Routing mechanics

### How a route matches

A route can match on:

- domain
- category
- severity
- profile
- tagsAny
- scope

Match semantics are intentionally simple:

- empty filter means "do not constrain on this field"
- `*` means wildcard match for that field
- otherwise the field is matched by ordinal-ignore-case equality

### How specificity works

Specificity is numeric and additive across fields.

- empty field contributes `0`
- wildcard field contributes `1`
- concrete field contributes `2`

This is how Steergen prefers a specific route over a catch-all route without introducing special-case behavior for catch-all routes.

### How destination paths are built

`RouteResolver.ResolveDestination` substitutes a small set of variables into the route's destination template:

- `${domain}`
- `${category}`
- `${severity}`
- `${profile}`
- `${ruleId}`
- `${inputFileStem}`

It then combines:

- destination directory
- destination file name
- destination extension

into a final path string.

The routing layer does not render content. It only computes the output path.

### Fallback mechanics

If no normal route matches, `RoutePlanner` looks for a core anchor route in the same scope and writes the rule to `other.*` in that anchor's directory.

If there is no core anchor, the result stays unresolved and the selection reason explains why.

### Write-plan mechanics

`WritePlanBuilder` groups many rule-level route resolutions into file-level work units.

Each `WritePlanFile` contains:

- `Path`
- `TruncateAtStart`
- `AppendUnits`

Each content unit carries a `RuleId` and an `OrderKey`. The actual rendered text is filled in later by the target component rather than by the planner.

This design is why the routing engine stays generic: it never needs to understand Kiro frontmatter or Speckit constitution structure.

### Generation mechanics per target

### Kiro

`KiroTargetComponent.GenerateWithPlanAsync` does the following for each planned file:

1. Look up the routed rules by `RuleId`.
2. Filter deprecated and inactive-profile rules.
3. Derive Kiro inclusion settings.
4. Build a `KiroDocumentModel`.
5. Render the `kiro/document` Scriban template.
6. Rebase the planned path under the selected output base.
7. Create the parent directory and write the Markdown file.

Kiro routing is document-shaped: one destination file usually corresponds to one source document or source-document-derived bucket.

### Speckit

`SpeckitTargetComponent.GenerateWithPlanAsync` does the following for each planned file:

1. Look up the routed rules by `RuleId`.
2. Rebase the planned path.
3. Infer whether the destination should be rendered as a constitution or a domain module.
4. Build either `SpeckitConstitutionModel` or `SpeckitModuleModel`.
5. Render the appropriate Scriban template.
6. Create the parent directory and write the file.

Speckit routing is bucket-shaped: multiple rules often collapse into one file such as `constitution.md` or `security.md`.

### Templates

Templates are embedded resources loaded by `EmbeddedTemplateProvider`.

Resource naming is:

`Steergen.Templates.Scriban.{targetId}.{templateName}.scriban`

That means when you add or rename a template, there are three things to keep aligned:

- the target component's `GetTemplate` call
- the embedded resource name
- the render model shape passed into Scriban

### Path rebasing and output semantics

The layout engine produces target-native paths that can be absolute or relative.

`PlannedOutputPathResolver` converts those plan paths into concrete output paths for the current run:

- relative plan paths are placed under the chosen output base
- absolute plan paths under `globalRoot` or `projectRoot` are stripped back to a relative path and then placed under the output base
- unknown absolute paths fall back to filename-only rebasing

This is the mechanism that allows layouts to express native paths like `${projectRoot}/.kiro/steering/...` while still supporting `--output` rebasing for tests and controlled generation runs.

### Validation and failure boundaries

There are three important failure boundaries in the code:

### 1. Corpus validation

If `SteeringValidator` finds errors, generation stops before routing.

### 2. Layout planning

If a target's layout fails to load or plan, `GenerationPipeline` records a warning diagnostic for that target.

### 3. Target generation

If a target throws while rendering or writing, the CLI surfaces a generation error.

The codebase also contains [src/Steergen.Core/Generation/WritePlanExecutor.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/WritePlanExecutor.cs), which models a more generic truncate-and-append executor, but the current built-in targets perform planned writing inside their own `GenerateWithPlanAsync` implementations rather than delegating to that executor directly.

That is useful to know when refactoring: the write-plan abstraction exists, but target-specific rendering is still coupled to file writing at the target layer.

### Where to work for common changes

If you want to change how files are discovered:

- [src/Steergen.Cli/Commands/RunCommand.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Cli/Commands/RunCommand.cs)

If you want to change the steering Markdown syntax:

- [src/Steergen.Core/Parsing/SteeringMarkdownParser.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Parsing/SteeringMarkdownParser.cs)

If you want to change merge or profile behavior:

- [src/Steergen.Core/Merge/SteeringResolver.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Merge/SteeringResolver.cs)

If you want to change route precedence or variable substitution:

- [src/Steergen.Core/Generation/RouteResolver.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/RouteResolver.cs)
- [src/Steergen.Core/Generation/RoutePlanner.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/RoutePlanner.cs)

If you want to change default target-native output locations:

- the target `default-layout.yaml` files under [src/Steergen.Core/Targets](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Targets)

If you want to change how a target renders content:

- the target component in [src/Steergen.Core/Targets](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Targets)
- the corresponding Scriban template under [src/Steergen.Templates/Scriban](/mnt/d/dev/aabs/steergen/src/Steergen.Templates/Scriban)

### Quick orientation checklist for new developers

If you need to become productive quickly, follow this sequence:

1. Read [src/Steergen.Cli/Commands/RunCommand.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Cli/Commands/RunCommand.cs) to understand runtime control flow.
2. Read [src/Steergen.Core/Parsing/SteeringMarkdownParser.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Parsing/SteeringMarkdownParser.cs) to understand what counts as a rule.
3. Read [src/Steergen.Core/Merge/SteeringResolver.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Merge/SteeringResolver.cs) to understand the resolved model.
4. Read [src/Steergen.Core/Generation/RouteResolver.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/RouteResolver.cs) and [src/Steergen.Core/Generation/RoutePlanner.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Generation/RoutePlanner.cs) to understand routing.
5. Read one target component, usually [src/Steergen.Core/Targets/Kiro/KiroTargetComponent.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Targets/Kiro/KiroTargetComponent.cs) or [src/Steergen.Core/Targets/Speckit/SpeckitTargetComponent.cs](/mnt/d/dev/aabs/steergen/src/Steergen.Core/Targets/Speckit/SpeckitTargetComponent.cs), to understand rendering.

Once those five pieces make sense, the rest of the codebase becomes much easier to navigate.