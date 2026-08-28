# Feature: Skill-category management

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-27

## Outcome

The site owner can maintain consistent, ordered categories used to group public
skills.

## In scope

- Protected paginated list, item retrieval, create, patch, soft-delete, and
  restore operations.
- Case-insensitive uniqueness and reference protection.

## Out of scope

- Manual ordering, nested categories, and cascading skill changes.

## HTTP contract

- Collection: `GET` and `POST /api/v1/admin/skill-categories`.
- Item: `GET`, `PATCH`, and `DELETE /api/v1/admin/skill-categories/{id}`.
- Restore: `POST /api/v1/admin/skill-categories/{id}/restore`.
- The mutable field is `name`; admin output adds resource metadata and
  `isDeleted`.

## Data and migrations

- `skill_categories` has a partial unique index on `normalized_name` for active
  rows; normalization trims and applies invariant uppercase.
- Categories order by immutable `created_at ASC`, then UUID.
- Foreign keys use restricted deletion.

## Security and privacy

- All operations require the admin key.

## Failure and operational behavior

- Duplicate active names return `409`.
- Deletion returns `409` while any active skill references the category.
- Restoration returns `409` when its name now conflicts with an active category.

## Acceptance scenarios

### Scenario: Protect a referenced category

- Given active skills reference a category
- When deletion is requested
- Then the API returns `409` and changes nothing

### Scenario: Restore a category

- Given a deleted category has no active-name conflict
- When restore is requested with its current ETag
- Then it becomes public in its original creation-time order

## Pessimistic test matrix

| Case | Class | Given / When | Then |
|---|---|---|---|
| SC-001 | Success | Valid trimmed name is created | `201`, location, version 1 ETag, normalized internal value |
| SC-002 | Failure | Name is null/empty/whitespace/maximum + 1 | `400`; no row |
| SC-003 | Success | Name length is exactly 80 | `201` and exact value after trimming rules |
| SC-004 | Failure | Active case/whitespace-normalized duplicate is created or patched | `409`; both records unchanged |
| SC-005 | Race | Concurrent same-name creates pass precheck | One winner and one controlled PostgreSQL `409` |
| SC-006 | Success | List uses defaults, boundaries, paging, and include-deleted | Deterministic `createdAt ASC, id ASC` with correct totals |
| SC-007 | Failure | Item is missing or deleted on GET/PATCH | `404`; deleted item is visible only in include-deleted list |
| SC-008 | Success | Name is patched with current ETag | `200`, normalized name/version/public timestamp updated |
| SC-009 | Failure | Patch document/name/precondition is invalid | `400`, `428`, or `412` as appropriate; no change |
| SC-010 | Failure | Active skill references category at delete time | `409 Resource is in use`; category and skill unchanged |
| SC-011 | Success | No active skill references category | `204`, soft-delete, new ETag, category and its skills absent publicly |
| SC-012 | Success | All referencing skills are deleted first | Category deletion succeeds without physically deleting skills |
| SC-013 | Success | Already-deleted category receives DELETE with a present stale ETag | Idempotent `204`; metadata unchanged |
| SC-014 | Recovery | Deleted category has no name conflict and is restored | `204`, new ETag, original ordering/public grouping returns |
| SC-015 | Failure | Restore target is missing/already active, ETag missing/stale, or active name conflicts | `404`, `428`, `412`, or `409`; deleted state retained |
| SC-016 | Recovery | Category is restored before its deleted skills | Category returns empty; later valid skill restores repopulate it |
| SC-017 | Characterization | Empty or unknown-only patch is submitted | Capture metadata/public ETag mutation pending no-op decision |

## Test evidence

- Integration tests for uniqueness, ordering, reference conflicts, pagination,
  deletion, restoration, and concurrency.
- Evidence (2026-08-24): automated HTTP tests cover assignment, paging,
  soft-deletion visibility, and protected references.

## Decisions and open questions

- Decision: categories are managed resources rather than free-form skill text.
