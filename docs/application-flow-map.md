# Presentation API application flow map

This document maps the behavior implemented in the Presentation API as of
2026-08-28. It is intentionally test-oriented: each flow identifies its
business outcome, decision points, state transitions, and exception paths.

## 1. Business model and lifecycle

The API owns the public portfolio/presentation shown by the personal site.
Content is published immediately; there is no draft, approval, or revision
workflow.

| Aggregate/resource | Business role | Public visibility | Important lifecycle rules |
|---|---|---|---|
| Profile | Identity, biography, availability, contact, and social links | Required root of the public presentation | Exactly one can ever be initialized; it cannot be deleted |
| Experience | Employment/consulting history | Every active experience is public, but location, highlights, and technologies are admin-only | Soft-deletable; referenced technologies must be active on create, patch, and restore |
| Project | Portfolio project | Only active featured projects are public | Soft-deletable; referenced technologies must be active on create, patch, and restore |
| Skill category | Public grouping for skills | Every active category is public | Cannot be deleted while an active skill references it |
| Skill | Capability within one category | Public only through its active category | Name is unique inside an active category; restore requires an active category |
| Technology | Reusable project/experience technology | Public only when attached to a featured project | Cannot be deleted while an active project or experience references it |

All managed records have a UUIDv7 identity, creation/update/public-update
timestamps, a positive version, and an optional soft-deletion timestamp.
Mutation is guarded by the version represented as a quoted integer ETag.

### Cross-resource business lifecycle

1. Initialize the profile. Without it, the public presentation is `404` even
   when other content exists.
2. Create skill categories, then create skills in those active categories.
3. Create technologies before attaching them to experiences or projects.
4. Create experiences and projects. They become public immediately; projects
   appear publicly only while featured.
5. To retire a category, first delete all of its active skills. To retire a
   technology, first remove it from or delete every active referencing project
   and experience.
6. A dependency may be deleted while it is referenced only by soft-deleted
   parents. Restoring such a parent is blocked until all of its technologies
   are active again.
7. A deleted name can be reused by a new category, skill, or technology. The
   original record then cannot be restored until the active name conflict is
   removed.

PostgreSQL mutations that add, replace, or restore technology references lock
the referenced technology rows until commit. Technology deletion takes the
same row lock before checking active parents. Concurrent operations therefore
serialize: either the aggregate commits and deletion returns `409`, or deletion
commits and the aggregate rejects the inactive reference.

## 2. Request pipeline shared flows

### 2.1 Administrative request gate

Every `/api/v1/admin/*` operation follows this flow before its handler runs:

1. In non-Development environments, reject a non-HTTPS request with `400`
   (`HTTPS required`). Development permits HTTP.
2. Read the configured `Admin:Key` and the supplied `X-Admin-Key`.
3. Hash and compare both values in constant time.
4. If the configured key is empty, the header is absent, or the value differs,
   return the same `401` Problem Details response.
5. If authorized, execute the operation and log method, derived resource type,
   route ID, outcome, duration, and trace ID. On an exception, log the failure
   and rethrow it to centralized exception handling.

Security rejections happen before the operation logging filter and before any
database access. Problem Details created by the application include `traceId`.

### 2.2 Input binding and media types

- JSON uses camel-case names and omits null response properties.
- `PATCH` declares `application/merge-patch+json` and requires a JSON object.
- A non-object merge patch returns `400` with a `document` validation error.
- Omitted patch fields retain their current values.
- Explicit `null` clears nullable fields and fails validation for required
  fields, required arrays, and required booleans.
- Unknown patch fields are ignored. An empty or unknown-only patch still runs
  the mutation path and increments the resource version.
- Framework routing/binding owns malformed JSON, incompatible JSON types,
  unsupported content types, invalid GUID route values, and unsupported HTTP
  methods. These should be characterized with tests because they do not pass
  through the feature validation helpers.

### 2.3 Pagination

Administrative list handlers parse headers before querying:

- `X-Page`: default `1`, valid range `1..Int32.MaxValue`.
- `X-Page-Size`: default `20`, valid range `1..100`.
- `X-Include-Deleted`: default `false`, accepts a Boolean string.
- Any invalid supplied header returns `400` Validation Problem Details. Multiple
  invalid headers are returned together.
- A page beyond the end returns `200`, an empty `items`, the requested page,
  the full `totalItems`, and calculated `totalPages`.
- An empty collection has `totalPages = 0`.

The expression `(page - 1) * pageSize` uses 32-bit arithmetic. An extreme valid
page number can overflow; this is an exception-flow candidate rather than a
defined business response.

### 2.4 Optimistic concurrency and soft deletion

For `PATCH`, `DELETE`, and restore:

1. Resolve the target in the state allowed by the operation.
2. If it is not found, return resource-specific `404`.
3. Require `If-Match`; absence returns `428`.
4. Require exact ordinal equality with `"<version>"`; mismatch, weak validators,
   or malformed values return `412`.
5. On success, increment version, update `updatedAt`, set the response ETag,
   and persist atomically.

Special delete behavior: after target lookup and presence-checking `If-Match`,
deleting an already-deleted resource returns `204` without validating the
header, mutating state, incrementing version, or emitting a fresh ETag.

Normal get/patch queries hide deleted records. List includes them only with
`X-Include-Deleted: true`. Restore deliberately queries deleted records.

### 2.5 Persistence and exception translation

| Exception/condition | HTTP behavior |
|---|---|
| EF concurrency failure | `412 Precondition Failed` |
| PostgreSQL unique or foreign-key violation wrapped in `DbUpdateException` | `409 Persistence conflict` |
| Any `DbUpdateException` during profile initialization | `409 Profile already exists` |
| Other database or application exception | Unhandled by the custom database handler; falls through to the platform `500` path |
| Readiness database exception/timeout | Converted to an unhealthy check and `503`, with no diagnostic detail in the response |
| Client/request cancellation | Propagates through the async operation; no business response is defined |

Database constraints backstop the singleton profile, active-name uniqueness,
foreign keys, maximum column lengths, and positive versions. Date ordering,
URL shape, list limits, and most non-empty rules exist only in application
validation. Foreign keys are restrictive; child/join replacement is explicit.

## 3. Profile flows

### GET `/api/v1/admin/profile`

- Active singleton exists: load ordered social links, return `200` and ETag.
- No active singleton: `404 Profile not found`.

### PUT `/api/v1/admin/profile`

1. Validate the complete aggregate.
2. Check for any profile, including a hypothetically deleted one.
3. Trim all text except social-link URLs; trim social-link labels.
4. Insert profile and links together.
5. Return `201`, location `/api/v1/admin/profile`, body, and ETag.

Business validation failures (`400`):

- `fullName` required, maximum 120.
- `headline` required, maximum 160.
- `biography` required, maximum 4,000.
- `shortSummary` maximum 500; `location` 160; `availability` 240;
  `currentFocus` 500.
- Optional email must be valid and at most 320.
- `socialLinks` is required and has at most 20 entries.
- Each link needs a nonblank label up to 40 and an absolute HTTP/HTTPS URL.
- Labels must be unique after trimming, case-insensitively.

Exception flows:

- Existing profile detected before insert: `409 Profile already exists`.
- Any EF update exception during insert, including a cause unrelated to the
  singleton: also `409 Profile already exists`.
- Social-link URLs have no application length check although the database
  maximum is 2,048; an oversized URL reaches persistence.

### PATCH `/api/v1/admin/profile`

- Requires an object, an active profile, and current `If-Match`.
- Builds a complete candidate from patched and retained fields, then applies
  the same validation as initialization.
- Supplying `socialLinks` physically replaces the ordered owned collection;
  omission preserves it; `null` is invalid; `[]` clears it.
- Every successful patch, including `{}` or an unknown-only document,
  increments version and `publicUpdatedAt`, so it invalidates the public ETag.
- Returns `200`, updated body, and new ETag.
- Save-time concurrency is explicitly caught and translated to `412`.

## 4. Named resources: skill categories and technologies

The two resource types share the same list, create, get, patch, delete, and
restore algorithm. Their collection order is `createdAt ASC, id ASC`.

### List and get

- List returns `200` with pagination and optionally deleted records.
- Active item get returns `200` and ETag; missing or deleted returns
  `404 Resource not found`.

### Create

1. Require a nonblank name of at most 80 characters.
2. Trim and normalize it with invariant uppercase.
3. Reject an active normalized-name duplicate with `409 Name already exists`.
4. Insert and return `201`, item location, body, and ETag.

A deleted record does not reserve its name. A simultaneous duplicate that
passes the application precheck relies on the PostgreSQL partial unique index
and is centrally translated to `409 Persistence conflict`.

### Patch

- Requires object, active target, and current ETag.
- Omitted name retains its value; explicit null/blank/oversized name is `400`.
- A duplicate active normalized name excluding self is `409`.
- Success updates name/normalized name, version, and public timestamp, then
  returns `200` and ETag.
- `{}` and unknown-only patches are successful mutations.

### Delete category

- Missing target: `404`; missing/stale precondition: `428`/`412`.
- Already deleted with any present `If-Match`: idempotent `204`.
- Any active skill in the category: `409 Resource is in use`.
- Otherwise soft-delete, increment version/public timestamp, return `204` and
  ETag.

### Delete technology

- Same common failures as category deletion.
- Any active experience or active project reference: `409 Resource is in use`.
- References owned only by soft-deleted parents do not block deletion.
- Otherwise soft-delete and return `204` with the incremented ETag.

### Restore category or technology

- Missing target or target already active: `404 Resource not found`.
- Missing/stale precondition: `428`/`412`.
- An active resource with the same normalized name: `409 Name already exists`.
- Otherwise clear deletion, increment version/public timestamp, and return
  `204` with ETag.

## 5. Skill flows

Skills list in `createdAt ASC, id ASC` order.

### List/get/create

- List and get follow common pagination, soft-delete, `404`, and ETag behavior.
- Create requires a valid name and nonempty category UUID.
- Category must exist and be active, otherwise `400 categoryId`.
- The normalized name must be unique among active skills in that category,
  otherwise `409 Skill already exists in this category`.
- The same skill name may exist in different categories.
- Success returns `201`, location, body, and ETag.

### Patch

- Requires object, active skill, and current ETag.
- May rename the skill, move it to another active category, or both.
- Full candidate validation runs even for a partial patch.
- Missing/deleted destination category is `400`; duplicate name within the
  destination category is `409`.
- Success increments version/public timestamp and returns `200` with ETag.
- Empty/unknown-only patch still mutates metadata.

### Delete/restore

- Delete follows common soft-delete behavior; no child references block it.
- Restore returns `404` when missing/already active and `428`/`412` for its
  precondition failures.
- Restore is `409 Skill cannot be restored` when its category is deleted/missing
  or an active same-name skill now exists in that category.
- Otherwise restore returns `204` and the new ETag.

## 6. Experience flows

Admin and public ordering is `startDate DESC`, then current roles (null end
date) first for equal starts, then `endDate DESC`, then `id ASC`.

### Validation and normalization

- Company and role: required, maximum 160; optional location maximum 160.
- Start date: required. End date may be null but cannot precede start date.
- Summary: required, maximum 4,000.
- Highlights: required, at most 20, each nonblank and at most 500, unique
  case-insensitively. Stored values are trimmed, but uniqueness is checked
  before trimming, so `"value"` and `" value "` currently pass validation.
- Technology IDs: required, at most 40, nonempty UUIDs, unique, and every ID
  must reference an active technology. An empty array is valid.

### List/get/create

- List loads highlights and technology joins and returns the complete admin
  aggregate with pagination.
- Get active item returns the complete aggregate and ETag; deleted/missing is
  `404 Experience not found`.
- Create validates structure first and references second, explicitly inserts
  the root, highlights, and joins, then returns `201`, location, and ETag.
- Invalid/inactive technology references return `400 technologyIds`.

### Patch

- Requires object, active target, and current ETag.
- Builds and validates the full candidate aggregate.
- Supplying highlights or technology IDs replaces the full corresponding
  collection; omission preserves it; null is invalid; empty clears it.
- Any successful patch increments the admin version.
- `publicUpdatedAt` changes only when company, role, start date, end date, or
  summary is supplied. Location, highlights, and technology-only changes are
  deliberately invisible to the public representation and do not invalidate
  its ETag.
- Returns `200`, complete admin body, and new ETag.

### Delete/restore

- Delete follows common behavior and removes the experience from the public
  projection immediately.
- Restore requires the deleted target, current ETag, and every attached
  technology to be active. Inactive dependency returns `409 Experience
  references a deleted technology`.
- Successful restore returns `204` and the new ETag.

## 7. Project flows

Admin and public project order is `createdAt DESC, id ASC`. Public output
contains only active projects where `isFeatured = true`.

### Validation and normalization

- Name required, maximum 160; summary required, maximum 1,000.
- Optional repository and live URLs must be absolute HTTPS URLs.
- Technology IDs follow the experience rules (required, maximum 40, active,
  unique, valid; empty allowed).
- `isFeatured` is required.
- Image is optional as a whole. When present it requires an absolute HTTPS URL,
  nonblank alt text up to 500, and positive width and height.
- Repository, live, and image URLs have no application maximum-length check,
  although their database columns are limited to 2,048.

### List/get/create

- List/get return the complete admin aggregate, including unfeatured projects,
  with common pagination, soft-delete, `404`, and ETag behavior.
- Create validates data and active technology references, inserts root and
  joins, then returns `201`, location, body, and ETag.

### Patch

- Requires object, active target, and current ETag; validates the full candidate.
- Technology IDs replace the complete join collection only when supplied.
- `image: null` clears all image columns. A partial image object is invalid.
- Every successful patch increments `publicUpdatedAt` because every project
  field can affect its public visibility or representation. Empty/unknown-only
  patches therefore invalidate the public ETag.
- Success returns `200` and the complete updated aggregate with new ETag.

### Delete/restore

- Delete follows common behavior and removes a featured project publicly.
- Restore requires every attached technology to be active; otherwise `409
  Project references a deleted technology`.
- Success returns `204` with new ETag. An unfeatured restored project remains
  absent from the public presentation.

## 8. Public presentation flow

### GET `/api/v1/presentation`

1. Load the active profile and ordered social links. If absent, return `404
   Presentation not found` with detail that the profile is uninitialized.
2. Load all active experiences in business order.
3. Load active featured projects and their active technology data in project
   order; order each project's technologies by name then ID.
4. Load active categories in creation order and each category's active skills
   in creation order.
5. Project only public fields. In particular, omit admin metadata, deletion,
   versions, normalized names, experience location/highlights/technologies,
   and unfeatured projects.
6. Set aggregate `updatedAt` to the maximum `publicUpdatedAt` among visible
   profile, experiences, projects, categories, skills, and technologies used by
   visible projects.
7. Serialize the visible response, hash its bytes with SHA-256, and emit that
   lowercase hex digest as a strong ETag.
8. Emit `Cache-Control: public,max-age=60,must-revalidate`.
9. If any supplied `If-None-Match` value exactly equals the ETag, return `304`;
   otherwise return `200` JSON.

Consequences worth testing:

- Hidden experience field changes preserve the public ETag.
- Unused technology changes and changes to technologies used only by an
  experience or unfeatured project preserve it.
- Renaming a technology used by a featured project changes it.
- Feature/unfeature, delete/restore, category/skill changes, and public profile
  changes alter the representation and ETag.
- `If-None-Match: *`, weak ETags, and comma-combined validators are not
  explicitly implemented as HTTP conditional-request semantics; only exact
  supplied string values are matched.
- Removing the newest visible record can make aggregate `updatedAt` move
  backward because deleted/unfeatured records are excluded from the maximum.

Database failures in this flow are not translated by feature code. The
intended external outcome is platform `500` Problem Details with traceable
server logging.

## 9. Health flows

### GET `/health/live`

- Anonymous and database-independent.
- Always returns `200` with `status: Healthy` and an empty checks array while
  the process can handle HTTP.

### GET `/health/ready`

- Anonymous.
- Runs only checks tagged `ready`; currently `presentation_database`.
- Links request cancellation with a three-second timeout and calls
  `CanConnectAsync`.
- Connection succeeds: `200`, overall/check status `Healthy`.
- False, exception, or timeout: `503`, overall/check status `Unhealthy`.
- Response does not expose exception, host, credentials, or connection string.

## 10. Startup and development seed flows

1. Configure JSON, Problem Details, exception translation, OpenAPI, PostgreSQL
   context, readiness, and explicit-origin CORS.
2. Only when environment is Development and `SeedData:Enabled = true`, invoke
   seeding before the request pipeline starts.
3. Relational database: apply migrations and begin a serializable transaction.
   Nonrelational test database: ensure it is created and do not open a
   transaction.
4. Query every independently managed table with query filters disabled.
5. If any profile, experience, project, category, skill, or technology exists,
   commit/no-op and preserve the entire database without supplementation.
6. Otherwise build the complete linked seed graph, save once, and commit.
7. Migration, query, insert, or commit failure aborts startup. Relational
   transaction disposal rolls back uncommitted seed changes.
8. Outside Development, seeding is never invoked even if enabled. OpenAPI is
   also exposed only in Development.

## 11. Test inventory and gaps

The baseline on 2026-08-27 is 16 passing tests and one skipped opt-in
PostgreSQL test. Existing tests cover authentication, profile singleton/basic
patching, one stale ETag case, public ordering/filtering/cache reuse, basic
pagination/soft deletion, one technology reference conflict, idempotent delete,
hidden experience ETag stability, two validation examples, health, route-set
parity, seed completeness/idempotence/gating, and schema/FK shape when
PostgreSQL is available.

### Priority 0: business invariants and destructive lifecycle

- `P0-01` Category deletion is blocked by an active skill; succeeds after the
  skill is deleted; skill restore is blocked until category restore.
- `P0-02` Technology deletion is blocked independently by an active experience
  and by an active project.
- `P0-03` Technology deletion is allowed when all referencing parents are
  deleted; each parent restore is blocked until technology restore.
- `P0-04` Category/technology restore is blocked by a replacement active name
  and succeeds after the conflict is removed.
- `P0-05` Skill restore is blocked by both failure causes: deleted category and
  replacement same-name skill.
- `P0-06` Concurrent create races for category, skill, technology, and profile
  return a controlled `409` on PostgreSQL and leave one winner.
- `P0-07` Concurrent patch race after both requests pass the HTTP ETag check
  returns one success and one `412`, with no lost child/join updates.
- `P0-08` Aggregate replacement rolls back root and children together on a
  forced database failure.
- `P0-09` Public output never leaks every excluded admin field and hides every
  soft-deleted resource type.
- `P0-10` Profile absence dominates all other stored content and returns public
  `404`.

### Priority 1: complete endpoint decision coverage

- `P1-01` Parameterized CRUD lifecycle for categories and technologies:
  create/get/list/patch/delete/include-deleted/restore plus all ETags.
- `P1-02` Full skill lifecycle including moving categories and allowing the
  same name across different categories.
- `P1-03` Full experience lifecycle including child replacement, clearing,
  ordering, and active-reference checks on create/patch/restore.
- `P1-04` Full project lifecycle including feature toggling, image set/replace/
  clear, join replacement, and active-reference checks.
- `P1-05` Profile nullable-field clearing, `socialLinks: []`, null array,
  duplicate labels after trim/case normalization, and ordering after replace.
- `P1-06` Missing, deleted, and already-active target behavior for every get,
  patch, delete, and restore handler.
- `P1-07` Missing, stale, malformed, weak, and current `If-Match` for every
  mutation family; already-deleted DELETE with a stale but present header.
- `P1-08` Default pagination, all header boundaries, combined invalid headers,
  empty collection, beyond-last page, include-deleted ordering, and extreme
  page overflow.

### Priority 1: validation boundaries

- `P1-09` Required string: null, empty, whitespace, exact maximum, maximum + 1.
- `P1-10` Optional string and email boundaries, including empty/whitespace
  normalization decisions.
- `P1-11` URL matrix: relative, HTTP versus HTTPS, unsupported scheme, invalid,
  exact database maximum, and maximum + 1 for every URL field.
- `P1-12` Collection matrix: null, empty, exact maximum, maximum + 1, empty UUID,
  duplicate UUID, inactive UUID, and mixed valid/invalid references.
- `P1-13` Highlights and social labels: case-only duplicates and duplicates
  that become equal only after trimming.
- `P1-14` Experience dates: missing start, end before/equal/after start, current
  role, and ordering ties.
- `P1-15` Project image: null, each missing member, nonpositive dimensions,
  invalid URL, blank/oversized alt, valid boundary values.

### Priority 2: protocol, public cache, operations, and infrastructure

- `P2-01` PATCH with non-object JSON, malformed JSON, wrong property types,
  wrong/missing content type, unknown fields, and empty object.
- `P2-02` Production HTTP admin request returns `400` before authentication;
  HTTPS missing/wrong key produces indistinguishable `401` responses.
- `P2-03` Problem Details media type, status, title/detail policy, and trace ID
  for every application-generated error family and representative `500`.
- `P2-04` CORS permits only configured origins and expected headers/methods;
  disallowed origin receives no access-control grant.
- `P2-05` Public ETag changes for every visible mutation and stays stable for
  every hidden mutation; validate 304 body/headers.
- `P2-06` Characterize wildcard, weak, multiple, and comma-combined
  `If-None-Match` values against intended HTTP semantics.
- `P2-07` Public tie-breaking order for experiences, projects, categories,
  skills, social links, and project technologies.
- `P2-08` Health false/exception/timeout/cancellation paths and diagnostic
  non-disclosure.
- `P2-09` Seeding relational rollback, serializable concurrency, migration
  failure, partially populated table variants, deleted-only content, and
  disabled Development mode.
- `P2-10` PostgreSQL checks for every partial unique index, singleton/check
  constraint, restrictive FK, query filter, column maximum, and migration
  idempotence/schema isolation.
- `P2-11` Operation logs cover success and exception without key/body content;
  unauthenticated rejection produces no management-operation log.
- `P2-12` Development-only OpenAPI visibility, generated operation/schema parity
  (not only path parity), unsupported methods, invalid GUID routes, HTTPS
  redirection behavior, and request cancellation.

## 12. Behavior questions exposed by the map

These are implemented behaviors that should be accepted explicitly or changed
before tests permanently lock them in:

1. Should empty and unknown-only patches increment version and sometimes
   invalidate the public ETag?
2. Should duplicate highlights be compared after trimming, matching social-link
   label behavior?
3. Should all URL fields be validated against the 2,048-character database
   limit so oversized client input is a `400` rather than a persistence error?
4. Should profile creation translate every database update failure into
   `Profile already exists`, or only singleton/unique violations?
5. Should public conditional GET support standard wildcard, weak, and combined
   `If-None-Match` semantics?
6. Should a public aggregate's `updatedAt` remain monotonic when the most
   recently updated visible record is deleted or unfeatured?
7. Should active skills whose category is deleted ever be observable in admin
   lists? The guarded API lifecycle prevents creating this state, but direct
   database changes can produce it because the skill query filter checks only
   the skill's own deletion state.
8. Should the maximum accepted page be capped to prevent skip arithmetic
   overflow?
