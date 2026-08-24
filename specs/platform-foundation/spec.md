# Feature: Platform foundation

- Status: Approved
- Owner: Igor
- Last updated: 2026-08-24

## Outcome

Presentation features share one secure, observable HTTP and persistence baseline
without coupling their feature-specific behavior.

## In scope

- PostgreSQL persistence in the `presentation` schema of the shared logical
  database.
- A Presentation-only database principal and EF Core migration history.
- UUIDv7 identifiers, UTC timestamps, soft deletion, and optimistic concurrency.
- Administrative authentication, pagination, Problem Details, and JSON naming.
- Vertical slices with lightweight command/query handlers and direct EF Core
  access.

## Out of scope

- Blog tables, cross-schema foreign keys, shared migrations, MediatR, generic
  repositories, and distributed transactions.
- Administrative user accounts or OIDC in the first release.

## HTTP contract

- JSON properties use `camelCase`; database objects use `snake_case`.
- Protected operations require `X-Admin-Key` over HTTPS.
- Collection requests accept optional `X-Page` (default `1`), `X-Page-Size`
  (default `20`, maximum `100`), and `X-Include-Deleted` (default `false`)
  headers.
- Collection responses contain `items`, `page`, `pageSize`, `totalItems`, and
  `totalPages`.
- Mutable item responses emit an ETag derived from the integer `version`.
- `PATCH`, `DELETE`, and restore operations require `If-Match`; absence returns
  `428` and a stale value returns `412`.
- PATCH documents use `application/merge-patch+json`. Omitted properties remain
  unchanged, explicit `null` clears nullable properties, and supplied aggregate
  arrays replace those arrays completely.
- Errors use `application/problem+json` with a request trace identifier.

## Data and migrations

- Every independently managed record has a UUIDv7 `id`, immutable `created_at`,
  `updated_at`, nullable `deleted_at`, and positive integer `version`.
- EF Core includes a global soft-delete filter. Administrative queries opt into
  deleted rows only when requested.
- Every foreign key uses `RESTRICT` or `NO ACTION`; database cascades are
  forbidden.
- Aggregate handlers explicitly insert, update, or physically remove owned
  child and join rows in a transaction.
- Each successful mutation increments `version`. EF updates include the prior
  version in their predicate.
- The Presentation migration history remains in the `presentation` schema and
  never changes Blog-owned objects.

## Security and privacy

- Compare the configured admin key in constant time and never log it.
- Reject missing or invalid keys with the same `401` representation.
- CORS uses an explicit configured origin list.
- Public projections never expose deletion metadata, concurrency versions, or
  administrative-only fields.

## Failure and operational behavior

- Validation returns `400`; missing active resources return `404`; protected
  relationship conflicts and restore uniqueness conflicts return `409`.
- An already deleted resource makes DELETE return `204` when an `If-Match`
  header is present; no additional mutation occurs and its value is not checked.
- Transactions roll back the root and all child-table changes together.
- Logs identify operation, resource type, resource ID, outcome, duration, and
  trace ID without recording secrets or content bodies.

## Acceptance scenarios

### Scenario: Reject an unauthenticated management request

- Given a protected endpoint
- When the request omits or supplies an invalid `X-Admin-Key`
- Then it returns `401` Problem Details and performs no database operation

### Scenario: Prevent a lost update

- Given a resource has changed since a client read it
- When the client submits PATCH with the old ETag
- Then the API returns `412` and preserves the newer state

### Scenario: Page an administrative collection

- Given more records exist than the requested page size
- When valid pagination headers are supplied
- Then the response reports the requested page and complete pagination metadata

## Test evidence

- PostgreSQL integration tests for schema isolation, soft-delete filters,
  restricted foreign keys, transactions, and concurrency.
- API integration tests for authentication, headers, pagination, merge patch,
  and Problem Details.

## Decisions and open questions

- Decision: one logical database may host both APIs, but schemas, principals,
  tables, migrations, and runtime access remain independent.
- Decision: no Presentation revision-history tables are required.
