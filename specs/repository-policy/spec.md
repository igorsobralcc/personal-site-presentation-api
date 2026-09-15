# Feature: Repository policy and CI enforcement

- Status: Approved
- Owner: Igor
- Last updated: 2026-09-15
- Review origin: owner requested this application-specific completion scope after the cross-repository audit.

## Outcome

Repository and deployment protections enforce the checks documented for this application.

## Current implementation and scope

Workflows exist, but branch/environment settings and live Actions runs were not inspected. Source configuration and external enforcement are separate evidence boundaries.

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

No new data model is selected by this specification. Model changes must be specified before implementation.

## Security and privacy

Preserve authorization and public-field boundaries. Never record credentials, connection strings, request bodies, private content, or full invalid payloads in evidence or logs.

## Failure and operational behavior

A failed check must produce a nonzero command result or the specified controlled API/UI failure. Rejected requests preserve business state; infrastructure-dependent guarantees require evidence against the actual adapter. CI file presence does not prove a live check passed or a repository setting is enforced.

## Acceptance scenarios

### PRP-001: Require application checks before merge

- Given current workflows define mandatory checks
- When repository rules are configured and exercised
- Then required commit policy, secret scan, application quality/tests/coverage, contract/migration and container gates where applicable are identified by exact check names and enforced on protected branches
- Evidence: Ruleset/branch protection and failing-PR evidence.
### PRP-002: Preserve workflow hardening

- Given a proposed workflow change
- When workflow/static validation runs
- Then actions use immutable references, permissions remain least privilege, untrusted PRs receive no production secrets, cancellation/serialization are deliberate, and jobs fail on check errors
- Evidence: Workflow validation and permission review.
### PRP-003: Protect production promotion

- Given a deployment environment and allowed refs exist
- When promotion is attempted
- Then documented environment approvals/ref restrictions and least-privilege runner or hosting credentials apply; missing settings are explicit prerequisites
- Evidence: Environment/settings evidence.
### PRP-004: Separate implemented files from live enforcement

- Given workflow files pass local validation
- When completion is assessed
- Then dated run links prove required jobs executed and settings evidence proves enforcement; a passing local suite or file presence alone is insufficient
- Evidence: CI/settings evidence matrix.
### PRP-005: Maintain deterministic policy checks

- Given commit/traceability/generation or other important validators exist
- When positive and deliberate-negative fixtures execute
- Then valid inputs pass, invalid inputs fail nonzero, repeat runs are deterministic, and diagnostics disclose no secrets
- Evidence: Validator fixture tests.

## Test plan and completion evidence

Each scenario names its required evidence level. Retain its stable ID in the executable test name, trait, or a precise evidence mapping. Evidence-only prerequisites must record the environment, revision, date, result, and a reproducible reference without secrets.

For changed executable feature code, enforce at least 90% line coverage using the narrowest defensible feature boundary; document the method. Preserve existing repository coverage gates and exclusions. Coverage does not replace rejection, concurrency, recovery, or contract assertions. Documentation-only changes need link, status, traceability, and formatting validation rather than artificial code tests.

Current baseline evidence is historical, not evidence that these new scenarios pass. Implementation must add exact test/run references and explicitly record any blocked external prerequisite.

## Documentation impact and related specifications

- [Repository workflow](../../CONTRIBUTING.md)
- [CI workflow](../../.github/workflows/ci.yml)
- [Documentation owner](../documentation-reconciliation/spec.md)

Update this specification, linked owners, the repository index, contract references, developer verification instructions, and operational runbooks when implementation lands. Record actual behavior and superseded decisions separately from planned behavior.

## Decisions and open questions

The owner accepted the completion topics and requested specifications, not every unresolved behavior choice or an implementation/deployment action. New choices remain proposed until approved.

Repository administrators must confirm exact ruleset/required-check names, bypass policy, production approval/ref restrictions, and runner ownership. Do not change external settings during this specification update.

## Implementation revision — 2026-09-15

The owner subsequently requested implementation of the pending scopes. [adopted mutation/cache decision](../mutation-validation/decisions/DR-001-edge-semantics.md) and [implementation/verification DR-002](../documentation-reconciliation/decisions/DR-002-implementation-and-verification.md) govern this revision. Earlier authoring-only and undecided statements above are historical; they do not describe the new implementation scope.

[Current implementation evidence](../../docs/implementation-evidence.md) records local results and explicitly pending external evidence. [Acceptance dispositions](../../scripts/acceptance-evidence.json) and [exact executable mappings](../../scripts/feature-acceptance-map.json) retain stable IDs. This revision must not be marked Implemented while mandatory evidence remains pending.

## Approved external settings successor — 2026-09-15

The owner explicitly approved the concrete GitHub protection proposal. Main required checks and production owner approval/main-only refs were applied and independently reread successfully. This supersedes the earlier no-live-settings scope only for those reviewed settings; production deployment, publishing and migrations were not performed. See [approved settings and evidence](../../docs/github-protection-proposal.md). Actual protected-run and runner/recovery evidence remains pending.
