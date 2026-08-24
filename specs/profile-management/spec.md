# Feature: Profile management

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-24

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

## Test evidence

- Integration tests for initialization, singleton enforcement, patch semantics,
  social-link replacement, validation, and concurrency.
- Evidence (2026-08-24): automated HTTP tests cover authentication, singleton
  initialization, merge-patch preservation, validation, and stale ETags.

## Decisions and open questions

- Decision: profile content becomes public immediately.
