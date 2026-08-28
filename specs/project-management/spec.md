# Feature: Project management

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-27

## Outcome

The site owner can manage portfolio projects and choose which projects appear
in the public composite presentation.

## In scope

- Protected paginated list, item retrieval, creation, merge-patch update,
  soft deletion, and restoration.
- Canonical technology associations, featured status, external destinations,
  and accessible dimensioned media.

## Out of scope

- Long project descriptions, uploads, image processing, project detail routes,
  and publication workflows.

## HTTP contract

- Collection: `GET` and `POST /api/v1/admin/projects`.
- Item: `GET`, `PATCH`, and `DELETE /api/v1/admin/projects/{id}`.
- Restore: `POST /api/v1/admin/projects/{id}/restore`.
- Fields are `name`, `summary`, nullable `repositoryUrl`, nullable `liveUrl`,
  `technologyIds[]`, `isFeatured`, and nullable `image`.
- When present, `image` requires `url`, `alt`, `width`, and `height` together.
- Admin output resolves selected technologies and includes resource metadata and
  `isDeleted`.

## Data and migrations

- `projects` and `project_technologies` use restricted foreign keys.
- Technology associations are replaced explicitly when supplied by PATCH.
- Projects order by immutable `created_at DESC`, then UUID.
- Media remains externally hosted; the API stores URL and accessibility/layout
  metadata only.
- PostgreSQL create, patch, and restore lock referenced technology rows through
  commit so a concurrent technology deletion cannot create an invalid active
  aggregate.

## Security and privacy

- Management operations require the admin key.
- Validate external URLs as absolute HTTPS URLs.

## Failure and operational behavior

- Reject incomplete image metadata, non-positive dimensions, duplicate
  technology IDs, and missing/deleted technologies.
- Restored featured projects immediately return to the public composite in
  creation-time order.

## Acceptance scenarios

### Scenario: Expose only featured projects

- Given active featured and non-featured projects exist
- When the public presentation is requested
- Then only featured projects appear while the admin list contains both

### Scenario: Require complete image metadata

- Given a request supplies an image URL without dimensions or alternative text
- When it is validated
- Then the API returns `400` and persists no change

## Pessimistic test matrix

| Case | Class | Given / When | Then |
|---|---|---|---|
| PJ-001 | Success | Minimum valid unfeatured project has empty technologies and no URLs/image | `201`, location, ETag; admin-visible and publicly absent |
| PJ-002 | Success | Complete featured project uses valid URLs, image, and 40 active technologies | Aggregate persists and complete public project appears |
| PJ-003 | Failure | Name/summary is null/blank/maximum + 1 | `400`; no root or joins |
| PJ-004 | Failure | Repository/live URL is relative, HTTP, malformed, or unsupported scheme | `400` for each invalid field; no mutation |
| PJ-005 | Characterization | Repository/live/image URL exceeds database length 2,048 | Capture persistence outcome; desired client error is `400` |
| PJ-006 | Failure | Technology list is null/over limit/empty-ID/duplicate or contains inactive ID | `400 technologyIds`; no partial aggregate |
| PJ-007 | Failure | `isFeatured` is null | `400`; no root or joins |
| PJ-008 | Success | Image is null or complete with positive dimensions and boundary alt | Accepted; null stores no partial image columns |
| PJ-009 | Failure | Image has missing URL/alt/dimension, non-HTTPS URL, blank/oversized alt, or nonpositive dimension | `400 image`; no mutation |
| PJ-010 | Success | List/get exercise paging, reverse creation order, unfeatured inclusion, and ETag | Correct complete admin representation |
| PJ-011 | Success | Patch changes scalar/URLs/featured/image and omits technology IDs | Existing joins retained; exact requested public change occurs |
| PJ-012 | Success | Patch replaces/clears technology IDs or clears image with null | Obsolete joins/image columns removed atomically |
| PJ-013 | Failure | Patch body/data/reference/target/precondition fails | Correct `400`/`404`/`428`/`412`; aggregate unchanged |
| PJ-014 | Race | Technology is deleted between active-reference check and join write | Controlled conflict and full rollback |
| PJ-015 | Race | Two aggregate patches use the same ETag | One complete candidate wins; loser `412`; no mixed joins/image |
| PJ-016 | Success | Project toggles false to true and true to false | It enters/leaves public projection and public ETag changes |
| PJ-017 | Success | Featured project's visible field or technology changes | Public body, updatedAt, and ETag change |
| PJ-018 | Success | Active project is deleted | `204`, soft-delete, public removal, joins retained |
| PJ-019 | Success | Already-deleted project is deleted with any present ETag | Idempotent `204`; metadata unchanged |
| PJ-020 | Recovery | Deleted project references only active technologies | Restore `204`; featured project returns publicly, unfeatured stays hidden |
| PJ-021 | Failure | Any attached technology is deleted at restore | `409`; project remains deleted with joins intact |
| PJ-022 | Failure | Root/join persistence fails during create/patch | No partial root, join, image, version, or public change |
| PJ-023 | Characterization | Empty or unknown-only patch is submitted | Capture version/public ETag change pending no-op decision |

## Test evidence

- Integration tests for CRUD, featured projection, media validation,
  technology replacement, ordering, pagination, deletion, restoration, and
  concurrency.
- Evidence (2026-08-24): automated HTTP tests cover featured filtering,
  accessible media validation, technology references, and public ordering.

## Decisions and open questions

- Decision: the unused long description field is removed.
