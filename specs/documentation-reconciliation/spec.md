# Feature: Documentation and decision reconciliation

- Status: Implemented
- Owner: Igor
- Last updated: 2026-09-15
- Review origin: owner requested this application-specific completion scope after the cross-repository audit.

## Outcome

Permanent documentation distinguishes approved intent, actual delivery, historical evidence, and outstanding requirements.

## Current implementation and scope

The audit found stale lifecycle states, evidence counts, and contract/feature assumptions. This specification owns reconciliation within this application only.

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

### PDR-001: Index every current owner

- Given feature, engineering, operations, and decision records coexist
- When the index is updated
- Then every completion topic has one authoritative owner, dependencies, status, and links; templates and historical reviews are not presented as delivered features
- Evidence: Index/link/status validation.
### PDR-002: Preserve and supersede history explicitly

- Given an earlier decision conflicts with a reviewed successor
- When documentation is revised
- Then the old decision remains discoverable and is marked historical/superseded with successor links and rationale; changes do not fabricate approval
- Evidence: Decision record review.
### PDR-003: Record evidence at the actual boundary

- Given tests run locally, in CI, or against infrastructure
- When completion evidence is written
- Then revision/date/environment/results/skips are recorded; in-memory results do not claim relational/provider/deployment guarantees
- Evidence: Evidence audit.
### PDR-004: Reconcile guides and runbooks

- Given implementation changes commands, configuration, routes, migrations, or release behavior
- When the change is completed
- Then README, CONTRIBUTING, flow maps, OpenAPI/Postman as applicable, evidence indexes, and runbooks agree with the approved implementation; links and examples are validated
- Evidence: Documentation validation.
### PDR-005: Apply honest lifecycle transitions

- Given required criteria or external evidence remain absent
- When a spec is proposed as Implemented
- Then partial delivery stays visible; material revisions are reapproved, unresolved choices remain explicit, and Implemented requires all mandatory evidence
- Evidence: Lifecycle checklist.

## Test plan and completion evidence

Each scenario names its required evidence level. Retain its stable ID in the executable test name, trait, or a precise evidence mapping. Evidence-only prerequisites must record the environment, revision, date, result, and a reproducible reference without secrets.

For changed executable feature code, enforce at least 90% line coverage using the narrowest defensible feature boundary; document the method. Preserve existing repository coverage gates and exclusions. Coverage does not replace rejection, concurrency, recovery, or contract assertions. Documentation-only changes need link, status, traceability, and formatting validation rather than artificial code tests.

Current baseline evidence is historical, not evidence that these new scenarios pass. Implementation must add exact test/run references and explicitly record any blocked external prerequisite.

## Documentation impact and related specifications

- [Specification index](../README.md)
- [Repository guide](../../README.md)
- [Scope decision](decisions/DR-001-completion-scope.md)

Update this specification, linked owners, the repository index, contract references, developer verification instructions, and operational runbooks when implementation lands. Record actual behavior and superseded decisions separately from planned behavior.

## Decisions and open questions

The owner accepted the completion topics and requested specifications, not every unresolved behavior choice or an implementation/deployment action. New choices remain proposed until approved.

None beyond the linked product specifications.

## Implementation revision — 2026-09-15

The owner subsequently requested implementation of the pending scopes. [adopted mutation/cache decision](../mutation-validation/decisions/DR-001-edge-semantics.md) and [implementation/verification DR-002](../documentation-reconciliation/decisions/DR-002-implementation-and-verification.md) govern this revision. Earlier authoring-only and undecided statements above are historical; they do not describe the new implementation scope.

[Current implementation evidence](../../docs/implementation-evidence.md) records local results and explicitly pending external evidence. [Acceptance dispositions](../../scripts/acceptance-evidence.json) and [exact executable mappings](../../scripts/feature-acceptance-map.json) retain stable IDs. This revision must not be marked Implemented while mandatory evidence remains pending.

## Verified completion — 2026-09-15

All scenarios owned by this revision have local evidence in [the implementation record](../../docs/implementation-evidence.md). The final chain passed 158 tests with no skips, including twelve PostgreSQL cases; all governed feature boundaries exceed 90% line coverage. External CI/container/settings/recovery scopes remain visibly owned by their separate Approved specifications.
