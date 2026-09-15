# Feature: Mutation and validation edge semantics

- Status: Implemented
- Owner: Igor
- Last updated: 2026-09-15
- Review origin: owner requested this application-specific completion scope after the cross-repository audit.

## Outcome

Previously undecided mutation/validation behavior becomes an explicit consistent contract with regression evidence.

## Current implementation and scope

Fourteen characterization cases remain outside the 183 non-characterization case mappings. This spec owns mutation, URL, normalization, error, and pagination choices; public cache cases remain with public-presentation.

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

### PMV-001: Resolve empty and unknown-only patches

- Given PF-012, PR-018, SC-017, TE-018, SK-018, EX-024, and PJ-023 are undecided
- When the revision is approved
- Then one explicit no-op/unknown-field policy is recorded and applied consistently; tests assert version, timestamps, children, public metadata, and ETag effects
- Evidence: Decision record plus HTTP tests.
### PMV-002: Handle oversized URLs deliberately

- Given PR-006 and PJ-005 reach the 2048-character database limit
- When the URL policy is approved and boundaries tested
- Then accepted client values fit storage; proposed over-limit 400 behavior is reconciled with OpenAPI and exact-limit/over-limit tests
- Evidence: HTTP plus PostgreSQL tests.
### PMV-003: Resolve normalized highlight duplicates

- Given EX-007 accepts values becoming equal after trim
- When duplicate policy is approved
- Then case/trim comparisons are explicit and tested alongside social labels; rejection or normalization leaves state consistent
- Evidence: Decision plus HTTP boundary matrix.
### PMV-004: Classify initialization failures accurately

- Given PR-019 encounters a non-singleton database exception
- When error mapping is approved and exercised
- Then only actual uniqueness conflicts receive conflict-specific responses; unexpected failures expose no misleading singleton diagnosis or sensitive database detail
- Evidence: Exception injection plus PostgreSQL translation.
### PMV-005: Prevent pagination arithmetic overflow

- Given PF-009 admits extreme page values
- When pagination policy is approved and exercised
- Then accepted pagination cannot overflow offset arithmetic; rejected values return keyed 400 and valid beyond-last pages preserve the documented response
- Evidence: Boundary HTTP plus PostgreSQL query tests.
### PMV-006: Close characterization evidence honestly

- Given all owned behavior choices have approved decisions
- When completion is assessed
- Then owning specs/OpenAPI are reconciled and characterization cases become normal mapped acceptance tests; unresolved choices keep this spec Draft
- Evidence: Traceability and decision review.

## Test plan and completion evidence

Each scenario names its required evidence level. Retain its stable ID in the executable test name, trait, or a precise evidence mapping. Evidence-only prerequisites must record the environment, revision, date, result, and a reproducible reference without secrets.

For changed executable feature code, enforce at least 90% line coverage using the narrowest defensible feature boundary; document the method. Preserve existing repository coverage gates and exclusions. Coverage does not replace rejection, concurrency, recovery, or contract assertions. Documentation-only changes need link, status, traceability, and formatting validation rather than artificial code tests.

Current baseline evidence is historical, not evidence that these new scenarios pass. Implementation must add exact test/run references and explicitly record any blocked external prerequisite.

## Documentation impact and related specifications

- [Platform](../platform-foundation/spec.md)
- [Profile](../profile-management/spec.md)
- [Projects](../project-management/spec.md)
- [Experiences](../experience-management/spec.md)
- [Public cache owner](../public-presentation/spec.md)
- [Flow evidence](../application-flow-index.md)

Update this specification, linked owners, the repository index, contract references, developer verification instructions, and operational runbooks when implementation lands. Record actual behavior and superseded decisions separately from planned behavior.

## Decisions and open questions

The owner accepted the completion topics and requested specifications, not every unresolved behavior choice or an implementation/deployment action. New choices remain proposed until approved.

Before approval choose: no-op and unknown-property policy; URL over-limit status; normalized highlight duplicate policy; singleton versus unexpected exception mapping; and safe pagination bound or arithmetic strategy. Desired 400 outcomes in older characterizations are proposals, not already approved behavior.

## Implementation revision — 2026-09-15

The owner subsequently requested implementation of the pending scopes. [adopted mutation/cache decision](../mutation-validation/decisions/DR-001-edge-semantics.md) and [implementation/verification DR-002](../documentation-reconciliation/decisions/DR-002-implementation-and-verification.md) govern this revision. Earlier authoring-only and undecided statements above are historical; they do not describe the new implementation scope.

[Current implementation evidence](../../docs/implementation-evidence.md) records local results and explicitly pending external evidence. [Acceptance dispositions](../../scripts/acceptance-evidence.json) and [exact executable mappings](../../scripts/feature-acceptance-map.json) retain stable IDs. This revision must not be marked Implemented while mandatory evidence remains pending.

The open mutation/cache choices above are resolved by the adopted decision: no-op empty/unknown-only patches; 2048-character URL limit; trimmed case-insensitive highlight uniqueness; singleton-index-specific diagnosis; validated Int32 pagination offsets; weak/list/wildcard conditional GET; maximum-current-visible updatedAt. No model or migration changes are needed.

## Verified completion — 2026-09-15

All scenarios owned by this revision have local evidence in [the implementation record](../../docs/implementation-evidence.md). The final chain passed 158 tests with no skips, including twelve PostgreSQL cases; all governed feature boundaries exceed 90% line coverage. External CI/container/settings/recovery scopes remain visibly owned by their separate Approved specifications.
