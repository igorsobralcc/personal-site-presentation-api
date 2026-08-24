# Feature: Project management

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-24

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

## Test evidence

- Integration tests for CRUD, featured projection, media validation,
  technology replacement, ordering, pagination, deletion, restoration, and
  concurrency.
- Evidence (2026-08-24): automated HTTP tests cover featured filtering,
  accessible media validation, technology references, and public ordering.

## Decisions and open questions

- Decision: the unused long description field is removed.
