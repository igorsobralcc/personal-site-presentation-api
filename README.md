# Personal Site Presentation API

ASP.NET Core API that owns the current public presentation content for the
personal site. It provides one read-optimized public representation and
protected management operations for profile, experience, projects, skill
categories, skills, and a shared technology catalog.

Blog articles and article series belong to the separate Blog API.

## Architecture

The API is a modular monolith organized as vertical slices:

```text
src/
  PersonalSite.Presentation.Api/
    Common/          HTTP, auth, errors, observability
    Data/            EF Core context, mappings, migrations, seed data
    Features/
      Presentation/  public composite read model
      Profile/       singleton initialization and update
      Experiences/   aggregate management
      Projects/      aggregate management
      SkillCategories/
      Skills/
      Technologies/  shared Presentation catalog
tests/
  PersonalSite.Presentation.Api.Tests/
```

Each feature owns its endpoints, contracts, validation, lightweight command and
query handlers, and persistence mappings. Handlers use the EF Core context
directly. The design intentionally has no MediatR, generic repository, or
duplicated persistence/domain model.

## Development method

Every behavior change uses spec-driven development. Specifications under
[`specs`](specs/README.md) and the checked-in
[`docs/openapi.yaml`](docs/openapi.yaml) contract are reviewed before production
implementation. Development uses incremental Conventional Commits; see
[`CONTRIBUTING.md`](CONTRIBUTING.md).

## Planned stack

- .NET 10 and ASP.NET Core Minimal APIs
- Entity Framework Core with PostgreSQL
- Built-in OpenAPI generation and Scalar for local exploration
- Problem Details for consistent failures
- xUnit and `WebApplicationFactory` with PostgreSQL integration tests
- Health checks and structured logging

## Database boundary

The Presentation and Blog APIs share one logical PostgreSQL database while
remaining operationally independent:

```text
database
  presentation schema   Presentation API principal and migrations
  blog schema           Blog API principal and migrations
```

There are no cross-schema foreign keys, shared tables, shared migrations, or
direct cross-API database reads. Tables may be shared by features inside the
Presentation schema. All foreign keys use `RESTRICT` or `NO ACTION`; database
delete cascades are forbidden.

Presentation records use UUIDv7 identifiers, immutable UTC creation timestamps,
soft deletion, and explicit concurrency versions. Database identifiers use
`snake_case`; JSON uses `camelCase`. Presentation changes become public
immediately and do not have publication states or revision history.

## HTTP contract

### Public read

```http
GET /api/v1/presentation
```

The response contains the required profile, experiences ordered by professional
start date, skills grouped under creation-ordered categories, and featured
projects ordered newest first. Deleted records, non-featured projects,
administrative metadata, and experience highlights/technologies are omitted.
The operation supports ETag conditional requests and public caching. A missing
profile returns `404`.

### Protected management

Management routes use collection `GET`/`POST`, item `GET`/`PATCH`/`DELETE`, and
`POST /{id}/restore` conventions. Profile is a database-enforced singleton and
uses `GET`, initialization `PUT`, and update `PATCH`.

Administrative routes require `X-Admin-Key` over HTTPS. Collection pagination
uses the `X-Page`, `X-Page-Size`, and `X-Include-Deleted` request headers.
Deleted records are excluded by default and administrative representations
include `isDeleted`.

PATCH uses `application/merge-patch+json`. PATCH, DELETE, and restore require
`If-Match`; a missing precondition returns `428` and a stale ETag returns `412`.
DELETE is idempotent for an already deleted resource. Creates return `201` with
`Location`; successful deletes and restores return `204`.

## Delivery order

1. Approve the feature specifications and OpenAPI contract.
2. Scaffold the solution and vertical-slice folders.
3. Add schema-isolated persistence, mappings, migrations, and development seed
   data.
4. Implement authentication and protected management slices.
5. Implement the public composite projection, caching, and ETag behavior.
6. Verify generated OpenAPI, PostgreSQL integration tests, health checks, and
   the production build.

## Verification

Run the fast HTTP and contract suite with `dotnet test`. PostgreSQL persistence
verification is enabled by setting `PRESENTATION_TEST_CONNECTION_STRING` to a
disposable test database; the test applies Presentation migrations and verifies
schema isolation and restricted foreign keys. It is skipped when that variable
is absent.

## Development seed data

Development startup seeds an empty Presentation schema with resume-derived
profile, experience, skill, technology, and featured-project content. Seeding
is controlled by `SeedData:Enabled`, is disabled outside Development, and skips
the complete operation when any managed content already exists. Set
`SeedData__Enabled=false` to run Development without applying the seed.
