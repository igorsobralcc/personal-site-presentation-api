# Feature: Automated code quality and acceptance gates

- Status: Approved
- Owner: Igor
- Last updated: 2026-09-15
- Review origin: owner requested this application-specific completion scope after the cross-repository audit.

## Outcome

Formatting, analyzers, builds, behavioral tests, and coverage are reproducible locally and enforced in CI.

## Current implementation and scope

Both API projects already enable .NET and Sonar analyzers, style checks, nullable types, and warnings as errors. CI builds and coverage gates exist; explicit format and full acceptance/contract enforcement remain to be completed.

The acceptance scenarios below define the delivery scope. Existing behavior is retained unless an explicit scenario or approved successor decision changes it.

## Out of scope

- Unrelated feature expansion, dependency upgrades, and broad source restructuring.
- Production mutation or external settings changes during specification authoring.
- Optional performance budgets, extra browser engines, and new hosting providers without an explicit decision.

## Content and workflow

Review unresolved choices, approve this revision, update affected contracts, add mapped failing behavior tests, implement, and record verification evidence. Do not mark this specification Implemented while required evidence is absent or skipped.

## HTTP and API contract

Preserve existing public contracts. Any HTTP change requires the owning OpenAPI contract to be updated before implementation.

## Data, migrations, and integrations

Use PersonalSite.Presentation.slnx and the existing test projects. Document disposable infrastructure prerequisites and generated migration/source exclusions. Planned dotnet format commands must be verified before being presented as implemented commands.

## Security and privacy

Preserve authorization and public-field boundaries. Never record credentials, connection strings, request bodies, private content, or full invalid payloads in evidence or logs.

## Failure and operational behavior

A failed check must produce a nonzero command result or the specified controlled API/UI failure. Rejected requests preserve business state; infrastructure-dependent guarantees require evidence against the actual adapter. CI file presence does not prove a live check passed or a repository setting is enforced.

## Acceptance scenarios

### PQG-001: Run one documented verification chain

- Given a clean checkout with supported .NET and disposable test infrastructure
- When the documented local and CI commands run
- Then restore, read-only dotnet format verification, analyzer-clean Release build, tests, coverage, relevant contract/migration checks, and publish/container checks run with failures propagated
- Evidence: Local/CI command evidence.
### PQG-002: Enforce organization and analyzer policy

- Given new or changed non-generated source and tests
- When static validation and review run
- Then feature organization, one named top-level type per matching file, braces, cancellation propagation, and documented narrow suppressions are checked; existing violations are inventoried without unrelated restructuring
- Evidence: Analyzer/configuration and source-policy fixtures.
### PQG-003: Map all required acceptance behavior

- Given a non-characterization criterion is required for completion
- When traceability validation runs
- Then an executable test or precise evidence mapping exists; unsupported and skipped cases have explicit dispositions and cannot claim adapter guarantees
- Evidence: Traceability positive/negative fixtures.
### PQG-004: Enforce coverage without hiding feature gaps

- Given a changed feature and existing global suite
- When coverage runs
- Then at least 90% changed-feature line coverage and the existing 90% repository line gate are enforced with documented generated-code exclusions
- Evidence: Coverage report and failing-threshold fixture.
### PQG-005: Require real infrastructure evidence

- Given behavior relies on database constraints, transactions, migrations, or production media adapters
- When required CI verification runs
- Then PostgreSQL cases run rather than silently skip; credentialed provider tests run in a protected documented lane or remain visibly incomplete; PRs receive no production secrets
- Evidence: CI configuration and integration run evidence.
### PQG-006: Reject defective automation

- Given a formatter/analyzer/test/contract check deliberately fails
- When the same task runs locally or in CI
- Then exit status is nonzero, publication is blocked, output identifies the failing check, and CI does not rewrite tracked files
- Evidence: Gate failure fixtures.

## Test plan and completion evidence

Each scenario names its required evidence level. Retain its stable ID in the executable test name, trait, or a precise evidence mapping. Evidence-only prerequisites must record the environment, revision, date, result, and a reproducible reference without secrets.

For changed executable feature code, enforce at least 90% line coverage using the narrowest defensible feature boundary; document the method. Preserve existing repository coverage gates and exclusions. Coverage does not replace rejection, concurrency, recovery, or contract assertions. Documentation-only changes need link, status, traceability, and formatting validation rather than artificial code tests.

Current baseline evidence is historical, not evidence that these new scenarios pass. Implementation must add exact test/run references and explicitly record any blocked external prerequisite.

## Documentation impact and related specifications

- [Repository workflow](../../CONTRIBUTING.md)
- [CI](../../.github/workflows/ci.yml)
- [Repository policies](../repository-policy/spec.md)

Update this specification, linked owners, the repository index, contract references, developer verification instructions, and operational runbooks when implementation lands. Record actual behavior and superseded decisions separately from planned behavior.

## Decisions and open questions

The owner accepted the completion topics and requested specifications, not every unresolved behavior choice or an implementation/deployment action. New choices remain proposed until approved.

Record supported SDK/tool versions and governed file scopes in repository configuration. Reuse current analyzers and test framework; no package upgrade is authorized by this specification.

## Implementation revision — 2026-09-15

The owner subsequently requested implementation of the pending scopes. [adopted mutation/cache decision](../mutation-validation/decisions/DR-001-edge-semantics.md) and [implementation/verification DR-002](../documentation-reconciliation/decisions/DR-002-implementation-and-verification.md) govern this revision. Earlier authoring-only and undecided statements above are historical; they do not describe the new implementation scope.

[Current implementation evidence](../../docs/implementation-evidence.md) records local results and explicitly pending external evidence. [Acceptance dispositions](../../scripts/acceptance-evidence.json) and [exact executable mappings](../../scripts/feature-acceptance-map.json) retain stable IDs. This revision must not be marked Implemented while mandatory evidence remains pending.
