# Feature: Technology management

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-27

## Outcome

The site owner can maintain one canonical technology catalog shared by
Presentation experiences and projects.

## In scope

- Protected paginated list, item retrieval, create, merge-patch update,
  soft-delete, and restore operations.
- Case-insensitive canonical uniqueness.

## Out of scope

- Technology logos, proficiency, public standalone technology endpoints, and
  Blog technology or tag data.

## HTTP contract

- Collection: `GET` and `POST /api/v1/admin/technologies`.
- Item: `GET`, `PATCH`, and `DELETE /api/v1/admin/technologies/{id}`.
- Restore: `POST /api/v1/admin/technologies/{id}/restore`.
- The mutable public field is `name`; normalized lookup text is internal.

## Data and migrations

- `technologies` has a partial unique index on `normalized_name` for active
  rows; normalization trims and applies invariant uppercase.
- Experience and project join tables reference it with restricted foreign keys.
- The normalized value is recalculated from `name` during every write.
- PostgreSQL deletion locks the technology row before checking active project
  and experience references. Aggregate writes use the same lock ordering.

## Security and privacy

- All operations require the admin key.

## Failure and operational behavior

- Duplicate active names return `409`.
- Deletion returns `409` while an active experience or project references the
  technology; no association is cascaded or silently removed.
- Restoration returns `409` on an active-name conflict.

## Acceptance scenarios

### Scenario: Prevent spelling variants

- Given `.NET` already exists
- When an administrator attempts to create a case-equivalent name
- Then the API returns `409` and retains the canonical record

### Scenario: Protect a referenced technology

- Given an active project references a technology
- When deletion is requested
- Then the API returns `409` and changes neither record

## Pessimistic test matrix

| Case | Class | Given / When | Then |
|---|---|---|---|
| TE-001 | Success | Valid trimmed technology name is created | `201`, location, version 1 ETag, canonical normalized name |
| TE-002 | Failure | Name is null/empty/whitespace/maximum + 1 | `400`; no row |
| TE-003 | Failure | Active case/whitespace-normalized duplicate is created or patched | `409`; canonical record retained |
| TE-004 | Race | Concurrent same-name creates pass precheck | One winner, one controlled PostgreSQL `409` |
| TE-005 | Success | List/get exercise defaults, boundaries, ordering, and deleted visibility | Correct page metadata; active get emits current ETag |
| TE-006 | Success | Valid rename is patched with current ETag | `200`, normalized name/version/public timestamp updated |
| TE-007 | Failure | Patch body/name/precondition or target is invalid | Correct `400`/`404`/`428`/`412`; no change |
| TE-008 | Failure | Active experience is the only reference when DELETE runs | `409 Resource is in use`; relationship remains |
| TE-009 | Failure | Active project is the only reference when DELETE runs | `409 Resource is in use`; relationship remains |
| TE-010 | Failure | Both active parent types reference the technology | One `409`; neither association is altered |
| TE-011 | Success | No active parent references the technology | `204`, soft-delete, version + 1, new ETag |
| TE-012 | Success | Only soft-deleted parents reference the technology | Delete succeeds; join rows remain for possible recovery |
| TE-013 | Success | Already-deleted technology is deleted with any present ETag | Idempotent `204`; metadata unchanged |
| TE-014 | Recovery | Deleted technology has no active-name conflict | Restore `204`, new ETag, dependent restores can proceed |
| TE-015 | Failure | Restore target/state/precondition is invalid or name now conflicts | Correct `404`/`428`/`412`/`409`; remains deleted |
| TE-016 | Success | Featured project uses a renamed technology | Public project name and ETag change |
| TE-017 | Success | Technology is unused or used only by experience/unfeatured project | Rename does not alter the public representation ETag |
| TE-018 | Characterization | Empty or unknown-only patch is submitted | Capture public-cache impact pending no-op decision |

## Test evidence

- Integration tests for normalization, uniqueness, reference protection,
  pagination, deletion, restoration, and concurrency.
- Evidence (2026-08-24): automated HTTP tests cover case-insensitive
  uniqueness, reference protection, pagination, idempotent deletion, and ETags.

## Decisions and open questions

- Decision: the catalog is shared only inside the Presentation schema.
