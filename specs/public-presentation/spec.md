# Feature: Public presentation

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-27

## Outcome

Visitors can load the complete current portfolio through one ordered, cacheable
request without receiving deleted or administrative-only data.

## In scope

- `GET /api/v1/presentation`.
- Profile, social links, grouped skills, experiences, and featured projects.
- Conditional requests, deterministic ordering, and partial empty sections.

## Out of scope

- Blog articles, non-featured projects, deleted content, experience highlights,
  experience technologies, and management metadata.

## HTTP contract

- A successful response returns `200`, an ETag, and `Cache-Control`.
- A matching `If-None-Match` returns `304` without a body.
- The profile is required. If it has not been initialized, return `404` Problem
  Details rather than a response with `profile: null`.
- Only featured, non-deleted projects appear. All other non-deleted presentation
  content appears immediately after a successful management mutation.
- The response contains `profile`, `experiences`, `projects`, `skillCategories`,
  and `updatedAt`.

## Data and migrations

- This feature owns no tables; it projects Presentation-owned feature tables.
- Experiences order by `start_date DESC`, then `end_date DESC NULLS FIRST`, then
  UUID.
- Projects order by `created_at DESC`, then UUID.
- Skill categories, skills within a category, and social links order by
  `created_at ASC`, then UUID.
- `updatedAt` is the greatest visible aggregate update timestamp.

## Security and privacy

- The operation is anonymous.
- It exposes no admin key, ETag version integer, deletion timestamp, or internal
  normalized-name value.
- Contact email is intentionally public profile content.

## Failure and operational behavior

- Calculate the ETag from the visible representation, so hidden administrative
  changes do not unnecessarily invalidate public caches.
- A database failure returns `500` Problem Details and is logged with its trace
  ID.
- Empty optional collections are returned as empty arrays.

## Acceptance scenarios

### Scenario: Read the complete presentation

- Given a profile and presentation records exist
- When a visitor requests the composite endpoint
- Then it returns the current profile, ordered experiences, grouped skills, and
  featured projects only

### Scenario: Hide deleted content

- Given a record has been soft-deleted
- When the composite endpoint is requested
- Then the record and its administrative metadata are absent

### Scenario: Reuse a cached representation

- Given a client sends the current ETag in `If-None-Match`
- When no visible content has changed
- Then the API returns `304` without a response body

## Pessimistic test matrix

| Case | Class | Given / When | Then |
|---|---|---|---|
| PP-001 | Failure | No active profile exists, regardless of other data | `404` Problem Details with uninitialized-profile detail and trace ID |
| PP-002 | Success | Only profile exists | `200`; all optional collections are empty arrays; cache headers/ETag present |
| PP-003 | Success | Complete active dataset exists | Exact public fields only, with all ordering and grouping rules |
| PP-004 | Success | Featured and unfeatured projects coexist | Only featured active projects appear |
| PP-005 | Success | Every resource type has an active and deleted record | No deleted root/child/metadata leaks into response |
| PP-006 | Success | Admin-only experience fields are populated | Location, highlights, technology IDs, versions, and timestamps are absent |
| PP-007 | Success | Admin normalized names/deletion/version metadata exist | None appears anywhere in public JSON |
| PP-008 | Success | Experience dates tie in each ordering dimension | Start/current/end/ID tie-breakers are deterministic |
| PP-009 | Success | Projects/categories/skills/social links share timestamps | Creation-time then ID tie-breakers are deterministic |
| PP-010 | Success | Featured project technologies have name/ID ties | Technologies order by name then ID deterministically |
| PP-011 | Success | `If-None-Match` exactly matches current ETag | `304`, no representation body, revalidation headers retained as intended |
| PP-012 | Success | Validator is absent or does not match | `200` with full body and current strong ETag |
| PP-013 | Success | Visible field/category/skill/featured-project/technology changes | Body, aggregate updatedAt where defined, and ETag change |
| PP-014 | Success | Only hidden experience data or unrelated technology changes | Serialized response and ETag remain identical |
| PP-015 | Success | A record is deleted, restored, featured, or unfeatured | Projection changes immediately and deterministically |
| PP-016 | Characterization | `If-None-Match` is wildcard, weak, multiple, or comma-combined | Capture exact-match-only behavior before adopting full HTTP semantics |
| PP-017 | Characterization | Newest visible record is removed/unfeatured | Capture potentially decreasing aggregate `updatedAt`; ETag still matches body |
| PP-018 | Failure | Database query fails before or during projection | `500` Problem Details with trace ID; no sensitive database detail |
| PP-019 | Failure | Request is cancelled during database access/serialization | Work stops, no misleading successful/cacheable response |
| PP-020 | Success | Anonymous request includes no admin key | Same public result; no authentication challenge or secret dependency |

## Test evidence

- PostgreSQL-backed integration tests for projection fields, featured filtering,
  soft deletion, every ordering rule, missing profile, ETag, and `304`.
- Evidence (2026-08-24): automated HTTP tests cover projection fields,
  filtering, ordering, cache headers, stable visible-representation ETags, and
  `304`; generated and checked-in route sets match.

## Decisions and open questions

- Decision: Presentation changes are public immediately; no publication field
  exists.
- Decision: experience highlights and technologies remain administratively
  managed but are omitted from this public response.
