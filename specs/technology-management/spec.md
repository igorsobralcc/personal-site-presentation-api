# Feature: Technology management

- Status: Approved
- Owner: Igor
- Last updated: 2026-08-24

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

- `technologies` has a partial unique index on `lower(name)` for active rows.
- Experience and project join tables reference it with restricted foreign keys.
- The normalized value is recalculated from `name` during every write.

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

## Test evidence

- Integration tests for normalization, uniqueness, reference protection,
  pagination, deletion, restoration, and concurrency.

## Decisions and open questions

- Decision: the catalog is shared only inside the Presentation schema.
