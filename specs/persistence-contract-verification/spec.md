# Feature: Persistence and full HTTP contract verification

- Status: Approved
- Owner: Igor
- Last updated: 2026-09-15
- Review origin: owner requested this application-specific completion scope after the cross-repository audit.

## Outcome

Relational guarantees and complete contract compatibility are proven rather than inferred from in-memory tests or route parity.

## Current implementation and scope

Nine PostgreSQL tests exist and CI configures a disposable PostgreSQL service. Local audit ran 109 tests and skipped nine; ContractTests checks path parity only.

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

Retain existing migrations and test infrastructure. Add missing PostgreSQL/role assertions proportionally; generated/client artifacts and generator settings require explicit version-controlled ownership.

## Security and privacy

Preserve authorization and public-field boundaries. Never record credentials, connection strings, request bodies, private content, or full invalid payloads in evidence or logs.

## Failure and operational behavior

A failed check must produce a nonzero command result or the specified controlled API/UI failure. Rejected requests preserve business state; infrastructure-dependent guarantees require evidence against the actual adapter. CI file presence does not prove a live check passed or a repository setting is enforced.

## Acceptance scenarios

### PCV-001: Execute relational acceptance cases

- Given CI is validating a change
- When the disposable PostgreSQL suite runs
- Then all required relational cases execute without missing-connection skips; constraints, concurrency, rollback, seed races, and exception translation have persisted-state assertions
- Evidence: CI PostgreSQL test run.
### PCV-002: Verify actual role isolation

- Given separate runtime and migrator credentials
- When runtime access and migration checks run
- Then Presentation writes/history stay in presentation; runtime cannot access blog or perform DDL; admin CI credentials are not evidence of least privilege
- Evidence: Disposable-role privilege matrix.
### PCV-003: Compare full operation contracts

- Given generated metadata, checked-in OpenAPI, and runtime responses
- When contract verification runs
- Then methods, operation parameters, request/response schemas, status codes, media types, and relevant headers agree; deliberate mismatches fail
- Evidence: Positive/negative contract tests.
### PCV-004: Preserve scenario evidence quality

- Given the 183 mapped non-characterization IDs
- When the evidence map is reviewed
- Then each mapping identifies behavioral assertions; trait presence alone cannot claim the required failure/race/rollback result
- Evidence: Traceability and evidence audit.
### PCV-005: Generate compatible clients

- Given the reviewed canonical OpenAPI
- When client generation and smoke checks execute
- Then output is deterministic and public/admin consumers compile and handle actual responses; schema drift blocks verification
- Evidence: Generated-client smoke and drift check.

## Test plan and completion evidence

Each scenario names its required evidence level. Retain its stable ID in the executable test name, trait, or a precise evidence mapping. Evidence-only prerequisites must record the environment, revision, date, result, and a reproducible reference without secrets.

For changed executable feature code, enforce at least 90% line coverage using the narrowest defensible feature boundary; document the method. Preserve existing repository coverage gates and exclusions. Coverage does not replace rejection, concurrency, recovery, or contract assertions. Documentation-only changes need link, status, traceability, and formatting validation rather than artificial code tests.

Current baseline evidence is historical, not evidence that these new scenarios pass. Implementation must add exact test/run references and explicitly record any blocked external prerequisite.

## Documentation impact and related specifications

- [OpenAPI](../../docs/openapi.yaml)
- [Flow index](../application-flow-index.md)
- [Platform](../platform-foundation/spec.md)
- [Seed](../development-seed-data/spec.md)
- [Engineering gates](../engineering-quality/spec.md)

Update this specification, linked owners, the repository index, contract references, developer verification instructions, and operational runbooks when implementation lands. Record actual behavior and superseded decisions separately from planned behavior.

## Decisions and open questions

The owner accepted the completion topics and requested specifications, not every unresolved behavior choice or an implementation/deployment action. New choices remain proposed until approved.

None beyond the linked product specifications.

## Implementation revision — 2026-09-15

The owner subsequently requested implementation of the pending scopes. [adopted mutation/cache decision](../mutation-validation/decisions/DR-001-edge-semantics.md) and [implementation/verification DR-002](../documentation-reconciliation/decisions/DR-002-implementation-and-verification.md) govern this revision. Earlier authoring-only and undecided statements above are historical; they do not describe the new implementation scope.

[Current implementation evidence](../../docs/implementation-evidence.md) records local results and explicitly pending external evidence. [Acceptance dispositions](../../scripts/acceptance-evidence.json) and [exact executable mappings](../../scripts/feature-acceptance-map.json) retain stable IDs. This revision must not be marked Implemented while mandatory evidence remains pending.
