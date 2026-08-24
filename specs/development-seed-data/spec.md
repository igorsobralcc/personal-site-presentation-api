# Feature: Development seed data

- Status: Implemented
- Owner: Igor
- Last updated: 2026-08-24

## Outcome

Developers can populate an empty local Presentation schema with realistic
portfolio content derived from Igor Sobral's generated resume.

## In scope

- A profile with LinkedIn and GitHub social links.
- Availability text expressing openness to mid-level backend opportunities
  worldwide without presenting senior roles as a requirement.
- Five professional experiences and their resume highlights.
- Canonical technology and skill catalogs with ordered skill categories.
- Featured portfolio projects derived from the resume's described initiatives.
- Development-only, configuration-controlled, idempotent startup seeding.

## Out of scope

- Production content changes, resume PDF storage, education records, uploaded
  media, fabricated external project URLs, and replacing existing content.

## Seed behavior

- Seeding runs only when the environment is `Development` and
  `SeedData:Enabled` is `true`.
- The seeder applies Presentation migrations before inserting data.
- It inserts the complete dataset only when every independently managed table
  is empty. If any managed content exists, it changes nothing.
- All inserted records use UUIDv7 identifiers, UTC timestamps, active state,
  and version `1`.
- Resume month/year dates use the first day for start dates and the final day
  for completed end dates. The current consulting role has a null end date.
- Ordering follows the existing immutable creation-time rules.

## Security and privacy

- Seed content includes only information present in the supplied resume.
- The email, LinkedIn URL, GitHub URL, and role availability are intentionally
  public profile fields.
- No admin key, credentials, local file path, or source PDF content is logged.

## Failure and operational behavior

- Existing data always wins; the seeder never updates, deletes, or supplements
  a non-empty Presentation schema.
- A migration or insert failure prevents partial seed data through a database
  transaction and is surfaced during Development startup.
- Production startup never invokes the seeder.

## Acceptance scenarios

### Scenario: Seed an empty development database

- Given Development seeding is enabled and all managed tables are empty
- When the application starts
- Then migrations are applied and the complete resume-derived dataset is
  inserted once

### Scenario: Preserve existing content

- Given any managed Presentation content already exists
- When the seeder runs
- Then no seed record is inserted or existing record changed

### Scenario: Disable seeding outside Development

- Given the application runs outside Development
- When it starts regardless of seed configuration
- Then no seed operation is attempted

## Test evidence

- Automated tests for complete content, ordering, relationship wiring,
  idempotence, preservation of existing content, and environment/configuration
  gating.
- Previous evidence (2026-08-24): four automated seed scenarios passed, covering 1
  profile, 5 experiences, 29 highlights, 4 projects, 5 categories, 23 skills,
  21 technologies, all relationships, API validation, UUIDv7/version defaults,
  idempotence, existing-data preservation, public ordering, and Production
  exclusion.
- Updated evidence (2026-08-24): the seed validation test confirms ordered
  LinkedIn and GitHub links plus worldwide mid-level availability; the complete
  suite passes with 16 tests and one opt-in PostgreSQL test skipped.

## Decisions and open questions

- Decision: education is not seeded because version 1 has no education feature.
- Decision: projects are concise portfolio projections of initiatives explicitly
  described in the resume, without invented repository, live, or image URLs.
