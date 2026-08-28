# Feature: Platform foundation

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-27

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

## Pessimistic test matrix

| Case | Class | Given / When | Then |
|---|---|---|---|
| PF-001 | Success | HTTPS admin request has the configured key | Handler executes and the key is absent from response and logs |
| PF-002 | Failure | Key is absent, empty, incorrect, or configuration is empty | Same `401` Problem Details shape; no handler or database call |
| PF-003 | Failure | Non-Development admin request uses HTTP, with or without a valid key | `400 HTTPS required` before authentication or persistence |
| PF-004 | Success | Development admin request uses HTTP with a valid key | Request is permitted by the transport gate |
| PF-005 | Success | Pagination headers are omitted | Page 1, size 20, active records only |
| PF-006 | Success | Page/page-size are at valid minima/maxima and include-deleted is either Boolean value | Exact requested metadata and visibility |
| PF-007 | Failure | Any pagination header is malformed or outside its range | `400` lists every invalid header; query is not executed |
| PF-008 | Success | Requested page is beyond the collection | `200`, empty items, requested page, correct totals |
| PF-009 | Characterization | Valid extreme page causes skip arithmetic overflow | Capture current response, then decide a safe maximum page contract |
| PF-010 | Success | Merge patch omits a property or nulls a nullable property | Omitted value is retained; nullable value is cleared |
| PF-011 | Failure | Patch body is non-object, malformed JSON, or has an incompatible property type | Controlled `400` Problem Details; no mutation |
| PF-031 | Failure | Patch uses a media type other than `application/merge-patch+json` | `415 Unsupported Media Type`; no mutation |
| PF-012 | Characterization | Patch is `{}` or contains only unknown fields | Capture version/public-cache mutation before deciding no-op semantics |
| PF-013 | Failure | Mutable operation omits `If-Match` | `428`, unchanged version and state |
| PF-014 | Failure | `If-Match` is stale, malformed, weak, or otherwise not the exact current ETag | `412`, unchanged state |
| PF-015 | Success | `If-Match` exactly matches current version | Atomic mutation, version + 1, new ETag |
| PF-016 | Race | Two updates start from the same version and both pass the HTTP precheck | One commits; the other receives `412`; no lost update |
| PF-017 | Success | Active resource is deleted with current ETag | `204`, soft-deleted, version + 1, new ETag, hidden by default |
| PF-018 | Success | Already-deleted resource is deleted with any present ETag | Idempotent `204`; no version/timestamp change |
| PF-019 | Failure | Already-deleted resource is deleted without `If-Match` | `428`; no change |
| PF-020 | Failure | PostgreSQL unique/FK violation occurs after an application precheck | `409 Persistence conflict` with trace ID; transaction rolled back |
| PF-021 | Failure | Non-translated database/application exception occurs | `500` Problem Details with trace ID; no sensitive detail |
| PF-022 | Failure | Child/join replacement fails after root changes are tracked | Entire unit of work rolls back; original aggregate remains |
| PF-023 | Success | Allowed CORS origin performs actual/preflight request | Only configured origin receives expected allow headers/methods |
| PF-024 | Failure | Unconfigured origin performs actual/preflight request | No cross-origin access grant |
| PF-025 | Success | Management operation succeeds or returns a business error | Log has operation/resource/id/outcome/duration/trace, not key/body |
| PF-026 | Failure | Management handler throws, including concurrency | Failure is logged once; concurrency entity types are diagnostic-only |
| PF-027 | Failure | Invalid GUID route or unsupported HTTP method is requested | Framework `404`/`405`; no handler or persistence call |
| PF-028 | Success | Problem Details is returned by any application error family | Correct status/media type/title and nonempty request trace ID |
| PF-029 | Success | Relational schema is migrated | Presentation objects/history stay in `presentation`; FKs never cascade |
| PF-030 | Failure | Direct write attempts nonpositive version or restricted deletion | PostgreSQL constraint rejects it without corrupting related state |

## Test evidence

- PostgreSQL integration tests for schema isolation, soft-delete filters,
  restricted foreign keys, transactions, and concurrency.
- API integration tests for authentication, headers, pagination, merge patch,
  and Problem Details.
- Evidence (2026-08-24): the full solution builds without warnings; 12 HTTP and
  contract tests pass; the idempotent PostgreSQL migration script generates.

## Decisions and open questions

- Decision: one logical database may host both APIs, but schemas, principals,
  tables, migrations, and runtime access remain independent.
- Decision: no Presentation revision-history tables are required.
