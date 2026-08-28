# Feature: Profile management

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-27

## Outcome

The site owner can initialize and partially update the single public profile and
its ordered social links.

## In scope

- Protected profile retrieval, initialization, and JSON Merge Patch update.
- Profile-owned social links updated as one aggregate.
- Singleton enforcement in the application and database.

## Out of scope

- Profile deletion, avatars, publication workflow, and revision history.

## HTTP contract

- `GET /api/v1/admin/profile` returns `200` with ETag or `404`.
- `PUT /api/v1/admin/profile` initializes the profile and returns `201`; it
  returns `409` when the singleton already exists.
- `PATCH /api/v1/admin/profile` requires `If-Match` and returns the updated
  aggregate with a new ETag.
- Fields are `fullName`, `headline`, `biography`, nullable `shortSummary`,
  nullable `location`, nullable `email`, nullable `availability`, nullable
  `currentFocus`, and `socialLinks[]` containing `label` and `url`.
- Supplying `socialLinks` replaces the complete ordered collection; omission
  leaves it unchanged.

## Data and migrations

- `profiles` uses a constant singleton key constrained by the database so a
  second row cannot exist.
- `profile_social_links` is owned by the profile, uses UUIDv7 identity and
  creation-time ordering, and has a restricted foreign key.
- Replacing social links physically removes obsolete owned rows explicitly
  inside the profile transaction.

## Security and privacy

- All operations require the admin key.
- Email is intentionally public through the composite response; no private
  contact field is stored by this feature.

## Failure and operational behavior

- Reject missing required strings, invalid email, invalid absolute URLs,
  duplicate social-link labels, and configured length/count violations with
  `400` Validation Problem Details.
- Concurrency follows the platform `428`/`412` rules.

## Acceptance scenarios

### Scenario: Enforce the singleton

- Given a profile already exists
- When initialization is requested again
- Then the API returns `409` and retains the existing profile

### Scenario: Patch one profile field

- Given the client has the current ETag
- When it patches only `currentFocus`
- Then all omitted fields and social links remain unchanged

## Pessimistic test matrix

| Case | Class | Given / When | Then |
|---|---|---|---|
| PR-001 | Success | No profile exists and the minimum valid aggregate is initialized | `201`, singleton location, version 1 ETag, trimmed persisted values |
| PR-002 | Success | Complete valid profile with 20 ordered social links is initialized | All fields round-trip and link order is preserved publicly/admin |
| PR-003 | Failure | Required text is null, empty, whitespace, or maximum + 1 | `400` per field; no profile or links inserted |
| PR-004 | Failure | Optional text/email is invalid or maximum + 1 | `400`; no profile or links inserted |
| PR-005 | Failure | Social links are null, exceed 20, have bad label/URL, or duplicate trimmed case-insensitive labels | `400 socialLinks`; no partial aggregate |
| PR-006 | Characterization | Social URL exceeds the database 2,048 limit | Capture current persistence outcome; desired client error is `400` |
| PR-007 | Failure | Active profile already exists | `409`; original profile and version unchanged |
| PR-008 | Race | Two valid initializations pass the precheck on PostgreSQL | One `201`, one controlled `409`, exactly one complete aggregate |
| PR-009 | Success | Profile GET follows successful initialization | `200`, current ETag, complete ordered aggregate |
| PR-010 | Failure | Profile GET/PATCH runs before initialization | `404`; PATCH does not prioritize a missing precondition over absence |
| PR-011 | Success | Patch changes one public scalar with current ETag | `200`, omitted fields/links retained, version and public ETag change |
| PR-012 | Success | Patch explicitly nulls every nullable profile field | `200`; fields clear and null properties are omitted from JSON |
| PR-013 | Success | Patch supplies a replacement social-link list or `[]` | Old rows are physically removed; new exact order or empty list persists |
| PR-014 | Failure | Patch sets social links to null or supplies an invalid replacement | `400`; root and original links remain unchanged |
| PR-015 | Failure | Patch lacks/currently misses ETag requirements | `428` when absent, `412` when stale; no state change |
| PR-016 | Race | Two profile patches use the same current ETag | One complete aggregate wins; the other is `412`; children never mix |
| PR-017 | Failure | Link replacement fails during persistence | Root fields, version, removed links, and new links all roll back |
| PR-018 | Characterization | Empty or unknown-only object is patched | Capture unconditional version/public ETag change pending no-op decision |
| PR-019 | Characterization | Profile initialization hits a non-singleton `DbUpdateException` | Capture misleading `Profile already exists` mapping pending refinement |

## Test evidence

- Integration tests for initialization, singleton enforcement, patch semantics,
  social-link replacement, validation, and concurrency.
- Evidence (2026-08-24): automated HTTP tests cover authentication, singleton
  initialization, merge-patch preservation, validation, and stale ETags.

## Decisions and open questions

- Decision: profile content becomes public immediately.
