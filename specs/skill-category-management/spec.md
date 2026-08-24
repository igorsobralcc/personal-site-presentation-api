# Feature: Skill-category management

- Status: Approved
- Owner: Igor
- Last updated: 2026-08-24

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

- `skill_categories` has a partial unique index on `lower(name)` for active
  rows.
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

## Test evidence

- Integration tests for uniqueness, ordering, reference conflicts, pagination,
  deletion, restoration, and concurrency.

## Decisions and open questions

- Decision: categories are managed resources rather than free-form skill text.
