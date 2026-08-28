# Feature: Skill management

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-27

## Outcome

The site owner can manage concise skills under canonical categories for grouped
public display.

## In scope

- Protected paginated list, item retrieval, creation, merge-patch update,
  soft deletion, and restoration.
- Assignment to one active skill category.

## Out of scope

- Proficiency levels, endorsements, manual ordering, and multiple categories
  per skill.

## HTTP contract

- Collection: `GET` and `POST /api/v1/admin/skills`.
- Item: `GET`, `PATCH`, and `DELETE /api/v1/admin/skills/{id}`.
- Restore: `POST /api/v1/admin/skills/{id}/restore`.
- Mutable fields are `name` and `categoryId`; admin output adds resource
  metadata and `isDeleted`.

## Data and migrations

- `skills` references `skill_categories` with restricted deletion.
- Active skill names are case-insensitively unique within an active category.
- Skills order by `created_at ASC`, then UUID within each category.

## Security and privacy

- All management operations require the admin key.

## Failure and operational behavior

- A missing or deleted category returns `400` Validation Problem Details.
- Duplicate active names within a category return `409`.
- Restoration returns `409` if the category is deleted or uniqueness is now
  violated.

## Acceptance scenarios

### Scenario: Assign a skill to a category

- Given an active category exists
- When a valid skill is created under it
- Then the skill appears inside that category in the public presentation

### Scenario: Reject a deleted category

- Given a category is deleted
- When a skill is created or moved to it
- Then the API returns `400` and persists no change

## Pessimistic test matrix

| Case | Class | Given / When | Then |
|---|---|---|---|
| SK-001 | Success | Valid skill is created in an active category | `201`, location, ETag; appears once in that public category |
| SK-002 | Failure | Name or category ID is null/empty/invalid | `400` with field errors; no row |
| SK-003 | Failure | Category ID is missing or soft-deleted | `400 categoryId`; no row/change |
| SK-004 | Failure | Same normalized name already exists in destination category | `409`; existing skill retained |
| SK-005 | Success | Same normalized name exists only in another category | Creation/move succeeds; each category has one skill |
| SK-006 | Race | Concurrent same-name creates in one category pass precheck | One winner and one controlled PostgreSQL `409` |
| SK-007 | Success | List/get exercise paging, creation order, ETag, and deleted visibility | Correct metadata and active-only item access |
| SK-008 | Success | Patch renames skill, moves category, or changes both | `200`, version/public timestamp update, public grouping moves atomically |
| SK-009 | Failure | Patch document/data/target/precondition is invalid | Correct `400`/`404`/`428`/`412`; original assignment remains |
| SK-010 | Failure | Destination category is deleted or contains conflicting name | `400` or `409`; no partial rename/move |
| SK-011 | Success | Active skill is deleted with current ETag | `204`, soft-delete, removed from public category |
| SK-012 | Success | Already-deleted skill is deleted with any present ETag | Idempotent `204`; metadata unchanged |
| SK-013 | Recovery | Deleted skill's category is active and name is free | Restore `204`, new ETag, original public position returns |
| SK-014 | Failure | Deleted skill's category is deleted/missing | `409 Skill cannot be restored`; remains deleted |
| SK-015 | Failure | Replacement same-name skill exists in original category | `409 Skill cannot be restored`; remains deleted |
| SK-016 | Failure | Restore target/state/precondition is invalid | Correct `404`/`428`/`412`; no change |
| SK-017 | Race | Category is deleted or duplicate created between restore checks/write | PostgreSQL restrict/unique failure is controlled; skill remains deleted |
| SK-018 | Characterization | Empty or unknown-only patch is submitted | Capture metadata/public ETag mutation pending no-op decision |

## Test evidence

- Integration tests for category assignment, grouped ordering, uniqueness,
  pagination, deletion, restoration, and concurrency.
- Evidence (2026-08-24): automated HTTP tests cover category assignment and
  creation-ordered public grouping.

## Decisions and open questions

- Decision: no unused proficiency field is stored or exposed.
