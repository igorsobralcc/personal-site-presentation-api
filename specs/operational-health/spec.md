# Feature: Operational health

- Status: Approved
- Owner: Igor
- Last updated: 2026-08-24

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

## Test evidence

- Integration tests for healthy responses and a substituted failing readiness
  check.

## Decisions and open questions

- Decision: health endpoints reveal no deployment topology.
