# Presentation API application-flow specification index

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-28

## Purpose

This index maps every runtime application flow to its permanent product
specification and defines the pessimistic test posture used by their case
matrices. The detailed implementation map is
[`../docs/application-flow-map.md`](../docs/application-flow-map.md).

## Pessimistic test posture

For every operation, tests start from the assumption that any boundary or
dependency can fail. A flow is not considered covered by only its happy path.
Where applicable, its specification must include:

1. the smallest valid request and a representative complete valid request;
2. absent, null, blank, malformed, wrong-type, duplicate, and boundary inputs;
3. unauthenticated, incorrectly authenticated, and insecure transport calls;
4. missing, active, deleted, referenced, and conflicting resource states;
5. missing, malformed, stale, and current preconditions;
6. concurrent writes that pass application prechecks;
7. database constraint, connection, transaction, timeout, and cancellation
   failures;
8. response status, media type, headers, body, persisted state, public
   projection, cache effect, and logs;
9. proof that a rejected request causes no partial state change; and
10. PostgreSQL-backed verification whenever behavior depends on relational
    constraints, transactions, query translation, or concurrency tokens.

Tests may use the in-memory provider for fast HTTP contract checks, but it is
not evidence for unique indexes, restrictive foreign keys, transactions,
database concurrency, migrations, or PostgreSQL exception translation.

## Flow ownership

| Flow ID | Runtime application flow | Owning specification | Primary risk |
|---|---|---|---|
| AF-01 | Administrative request gate, binding, pagination, concurrency, soft deletion, persistence translation, CORS, and logging | [`platform-foundation/spec.md`](platform-foundation/spec.md) | Unauthorized access, lost updates, partial writes, misleading errors |
| AF-02 | Profile read, initialization, and aggregate patch | [`profile-management/spec.md`](profile-management/spec.md) | Singleton violation, public-data corruption, child replacement loss |
| AF-03 | Skill-category lifecycle | [`skill-category-management/spec.md`](skill-category-management/spec.md) | Orphaned grouping, name collision, invalid restoration |
| AF-04 | Technology lifecycle | [`technology-management/spec.md`](technology-management/spec.md) | Broken project/experience references |
| AF-05 | Skill lifecycle and category assignment | [`skill-management/spec.md`](skill-management/spec.md) | Invalid grouping, per-category duplicate |
| AF-06 | Experience aggregate lifecycle | [`experience-management/spec.md`](experience-management/spec.md) | Invalid chronology, partial aggregate replacement, hidden/public cache drift |
| AF-07 | Project aggregate lifecycle | [`project-management/spec.md`](project-management/spec.md) | Incorrect publication, inaccessible media, broken technology joins |
| AF-08 | Anonymous composite presentation and cache validation | [`public-presentation/spec.md`](public-presentation/spec.md) | Private metadata leakage, stale/unstable cache, incorrect ordering/filtering |
| AF-09 | Liveness and readiness | [`operational-health/spec.md`](operational-health/spec.md) | False readiness or infrastructure disclosure |
| AF-10 | Startup and Development seed | [`development-seed-data/spec.md`](development-seed-data/spec.md) | Production mutation, partial seed, overwritten content |

Secure container delivery is governed separately by
[`secure-container-delivery/spec.md`](secure-container-delivery/spec.md). It is
a delivery flow rather than an application runtime flow.

## Case classification

- **Success** proves the requested business state transition and its externally
  observable representation.
- **Failure** proves a rejected or degraded path, including unchanged state.
- **Race** proves behavior when two individually valid operations overlap.
- **Recovery** proves the system can return to a valid state after deletion or
  dependency failure.
- **Characterization** records a current edge behavior that requires an
  explicit product decision before it becomes a permanent assertion.

## Completion rule

A flow is test-complete only when every non-characterization case in its owning
specification has automated evidence and every characterization case has either
been accepted into the product contract or replaced by the decided behavior.

## Implementation evidence

- Every one of the 183 non-characterization case IDs is referenced by an
  automated test or theory through a `Spec` trait.
- The 14 characterization cases remain intentionally undecided and are not
  counted as missing automated behavior.
- Fast HTTP, validation, projection, health, seed, logging, and degraded
  dependency tests run without external infrastructure.
- Nine PostgreSQL tests verify migrations, constraints, exception translation,
  optimistic concurrency, aggregate rollback, seed rollback/concurrency, and
  technology-deletion races when `PRESENTATION_TEST_CONNECTION_STRING` points
  to a disposable database.
- Evidence (2026-08-28): without that opt-in connection, 109 tests pass and the
  nine PostgreSQL tests skip; the Release build has no warnings. CI-style
  Coverlet measurement reports 91.31% executable line coverage after excluding
  generated EF migration and OpenAPI source-generator files.
