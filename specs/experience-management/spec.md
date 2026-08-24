# Feature: Experience management

- Status: Approved
- Owner: Igor
- Last updated: 2026-08-24

## Outcome

The site owner can manage professional experience aggregates while visitors see
them in professional chronology rather than insertion order.

## In scope

- Protected paginated list, item retrieval, creation, partial update, soft
  deletion, and restoration.
- Owned highlights and associations with canonical technologies.

## Out of scope

- Manual sort positions, publication states, and public exposure of highlights
  or technologies in version 1.

## HTTP contract

- Collection: `GET` and `POST /api/v1/admin/experiences`.
- Item: `GET`, `PATCH`, and `DELETE /api/v1/admin/experiences/{id}`.
- Restore: `POST /api/v1/admin/experiences/{id}/restore`.
- Fields are `company`, `role`, nullable `location`, `startDate`, nullable
  `endDate`, `summary`, `highlights[]`, and `technologyIds[]`.
- Admin representations add `id`, timestamps, `version`, and `isDeleted`.
- Creating returns `201` with `Location`; patch returns `200`; delete and restore
  return `204` and an updated ETag where a current representation exists.

## Data and migrations

- `experiences` owns `experience_highlights` and
  `experience_technologies`; all foreign keys restrict deletion.
- Supplied aggregate arrays replace their current rows explicitly in one
  transaction.
- Ordering uses `start_date DESC`, `end_date DESC NULLS FIRST`, then UUID.
- Referenced technologies must be active.

## Security and privacy

- All management operations require the admin key.
- The public composite exposes only company, role, summary, and dates.

## Failure and operational behavior

- Reject `endDate` earlier than `startDate`, empty required text, duplicate
  highlights, duplicate technology IDs, and missing/deleted technologies.
- Missing related technologies return `400`; concurrent mutations follow the
  platform precondition rules.
- Restoration returns the experience to its date-derived public position.

## Acceptance scenarios

### Scenario: Order by professional date

- Given an older experience was created after a newer experience
- When experiences are read
- Then the newer `startDate` appears first

### Scenario: Replace aggregate children

- Given an experience has highlights and technologies
- When PATCH supplies replacement arrays with the current ETag
- Then the new arrays replace the old associations atomically

## Test evidence

- Integration tests for CRUD, pagination headers, date ordering, validation,
  aggregate replacement, deletion, restoration, and concurrency.

## Decisions and open questions

- Decision: highlights and technologies remain in admin storage and contracts
  but are omitted from the public composite response.
