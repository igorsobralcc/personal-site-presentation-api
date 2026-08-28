# Feature: Experience management

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-27

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
- PostgreSQL create, patch, and restore lock referenced technology rows through
  commit so a concurrent technology deletion cannot create an invalid active
  aggregate.

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

## Pessimistic test matrix

| Case | Class | Given / When | Then |
|---|---|---|---|
| EX-001 | Success | Minimum valid experience uses empty child arrays | `201`, location, ETag, empty arrays persisted/public summary visible |
| EX-002 | Success | Complete valid experience uses 20 highlights and 40 active technologies | Complete admin aggregate persists atomically at boundary limits |
| EX-003 | Failure | Required scalar is null/blank/maximum + 1 or optional location is oversized | `400` with all detected fields; no root/children |
| EX-004 | Failure | Start date missing or end date precedes start date | `400`; no root/children |
| EX-005 | Success | End equals start or is null | Accepted and ordered by defined chronology |
| EX-006 | Failure | Highlights are null, over limit, blank, oversized, or case-insensitive duplicates | `400 highlights`; no partial aggregate |
| EX-007 | Characterization | Highlights differ before trim but become equal after trim | Capture accepted duplicate-normalization behavior pending decision |
| EX-008 | Failure | Technology list is null/over limit/has empty or duplicate ID | `400 technologyIds`; no aggregate |
| EX-009 | Failure | Any technology ID is missing/deleted among otherwise valid IDs | `400 technologyIds`; no joins/root change |
| EX-010 | Success | List/get exercise paging and every chronology tie-breaker | Deterministic order; complete admin aggregate and ETag |
| EX-011 | Success | Patch changes one scalar and omits arrays | `200`; child rows/ordering retained; version increments |
| EX-012 | Success | Patch replaces or clears highlights/technologies | Exact new arrays persist; obsolete rows physically removed |
| EX-013 | Failure | Replacement validation/reference/precondition fails | Correct `400`/`428`/`412`; original root and both arrays remain |
| EX-014 | Race | Referenced technology is deleted between validation and join write | Controlled conflict/rollback; aggregate is not partially changed |
| EX-015 | Race | Two aggregate patches use the same current ETag | One complete candidate wins; loser `412`; children never mix |
| EX-016 | Success | Public scalar/date changes | Public representation, ordering if relevant, updatedAt, and ETag change |
| EX-017 | Success | Only location, highlights, or technologies change | Admin ETag changes; public representation and ETag remain stable |
| EX-018 | Success | Delete active experience with current ETag | `204`, soft-delete, removed publicly, children retained |
| EX-019 | Success | Delete already-deleted experience with any present ETag | Idempotent `204`; version/timestamps unchanged |
| EX-020 | Recovery | Deleted experience references only active technologies | Restore `204`, new ETag, returns to chronology-derived position |
| EX-021 | Failure | Any attached technology is deleted at restore | `409`, experience remains deleted with joins intact |
| EX-022 | Failure | Get/patch/restore/delete target or precondition is invalid | Correct operation-specific `404`/`428`/`412`; no change |
| EX-023 | Failure | Child/join replacement save fails | Transaction restores original root, highlights, joins, and version |
| EX-024 | Characterization | Empty or unknown-only patch is submitted | Version changes while public ETag remains stable; decide no-op contract |

## Test evidence

- Integration tests for CRUD, pagination headers, date ordering, validation,
  aggregate replacement, deletion, restoration, and concurrency.
- Evidence (2026-08-24): automated HTTP tests cover date ordering, date
  validation, hidden aggregate replacement, projection privacy, and ETags.

## Decisions and open questions

- Decision: highlights and technologies remain in admin storage and contracts
  but are omitted from the public composite response.
