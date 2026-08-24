# Feature: Skill management

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-24

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

## Test evidence

- Integration tests for category assignment, grouped ordering, uniqueness,
  pagination, deletion, restoration, and concurrency.
- Evidence (2026-08-24): automated HTTP tests cover category assignment and
  creation-ordered public grouping.

## Decisions and open questions

- Decision: no unused proficiency field is stored or exposed.
