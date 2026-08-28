# Feature: Operational health

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-27

## Outcome

The hosting platform can distinguish a running process from an instance capable
of serving database-backed traffic.

## In scope

- Anonymous liveness and readiness endpoints.
- PostgreSQL readiness, bounded timeouts, and structured health logging.

## Out of scope

- Detailed infrastructure disclosure, metrics dashboards, and Blog API health.

## HTTP contract

- `GET /health/live` returns `200` while the process can handle requests.
- `GET /health/ready` returns `200` only when the Presentation database context
  can reach its schema; otherwise it returns `503`.
- Responses expose only status and named check outcomes, not connection strings,
  hosts, credentials, exceptions, or SQL.

## Data and migrations

- No tables or migrations.

## Security and privacy

- Endpoints are anonymous so infrastructure probes can call them.
- Diagnostic details remain in protected logs.

## Failure and operational behavior

- The readiness database check has a short configured timeout.
- A readiness failure does not cause liveness to fail.
- Repeated probe success is not logged at information level.

## Acceptance scenarios

### Scenario: Report a database outage

- Given the API process is running but PostgreSQL is unavailable
- When readiness and liveness are requested
- Then readiness returns `503` while liveness returns `200`

## Pessimistic test matrix

| Case | Class | Given / When | Then |
|---|---|---|---|
| OH-001 | Success | Process handles anonymous liveness request | `200`, Healthy, empty checks, no database call |
| OH-002 | Success | Database `CanConnectAsync` succeeds | Readiness `200`; named check and aggregate are Healthy |
| OH-003 | Failure | Database returns false | Readiness `503` Unhealthy; liveness remains `200` |
| OH-004 | Failure | Database throws connection/authentication/SQL exception | Readiness `503`; response omits exception/topology/credentials |
| OH-005 | Failure | Database check exceeds three seconds | Linked timeout cancels it; readiness becomes `503` promptly |
| OH-006 | Failure | Caller cancels readiness request | Work is cancelled without changing liveness or exposing diagnostics |
| OH-007 | Success | Admin key is absent or invalid | Both health endpoints remain anonymous and unchanged |
| OH-008 | Success | Multiple health checks include non-ready tags | Readiness reports only checks tagged `ready` |
| OH-009 | Failure | One of multiple ready checks is unhealthy | Aggregate is Unhealthy/`503`; individual names/statuses are accurate |
| OH-010 | Success | Health response is inspected | Stable JSON/media type; no connection string, host, stack, or SQL text |
| OH-011 | Success | Repeated healthy probes occur | No information-level success-log noise |

## Test evidence

- Integration tests for healthy responses and a substituted failing readiness
  check.
- Evidence (2026-08-24): automated HTTP tests cover anonymous liveness, named
  readiness output, and an isolated readiness failure while liveness remains
  healthy; the database check is bounded to three seconds.

## Decisions and open questions

- Decision: health endpoints reveal no deployment topology.
