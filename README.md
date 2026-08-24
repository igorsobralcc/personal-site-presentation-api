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

## Postman collection

Import [`docs/PersonalSite.Presentation.Api.postman_collection.json`](docs/PersonalSite.Presentation.Api.postman_collection.json)
into Postman to exercise all public, administrative, and health endpoints. Set
the collection's `baseUrl` and `adminKey` variables before running protected
requests. Create and get requests capture resource IDs and ETags for subsequent
update, delete, and restore requests.

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

Development uses the `presentation_migrator` role because the seeder applies
EF Core migrations. Store its real local connection string outside Git:

```powershell
dotnet user-secrets set "ConnectionStrings:Presentation" "Host=localhost;Port=5432;Database=personal_site;Username=presentation_migrator;Password=<password>" --project src/PersonalSite.Presentation.Api
```

Normal runtime configuration uses the least-privilege `presentation_app` role.
Supply its production connection string through the
`ConnectionStrings__Presentation` environment variable or the deployment
platform's secret store.

## Secure CI and container delivery

[`CI`](.github/workflows/ci.yml) runs for pull requests and `main` pushes. It
validates every introduced Conventional Commit, scans Git history for secrets,
builds in Release mode, runs all tests against an ephemeral PostgreSQL 18
service, and builds the production container. A successful `main` run publishes
the same source revision to GHCR as `sha-<full-commit>` and `latest`.

The image runs as a non-root user, listens on container port `8080`, and exposes
a Docker health check through `/health/live`. Database and admin credentials are
runtime environment variables and are not Docker build arguments or image
layers.

### Configure the production secrets

First authenticate GitHub CLI and create the protected environment:

```powershell
gh auth login --hostname github.com
gh api --method PUT repos/igorsobralcc/personal-site-presentation-api/environments/production
```

Then add both environment secrets. These commands prompt for values and do not
put them in shell history:

```powershell
gh secret set PRESENTATION_CONNECTION_STRING --env production --repo igorsobralcc/personal-site-presentation-api
gh secret set PRESENTATION_ADMIN_KEY --env production --repo igorsobralcc/personal-site-presentation-api
```

`PRESENTATION_CONNECTION_STRING` must use the least-privilege
`presentation_app` login and a database host reachable from the deployment
container. Do not use `localhost` unless PostgreSQL runs inside that same
container. `PRESENTATION_ADMIN_KEY` should be a separate random value, for
example a 32-byte cryptographically random secret.

In the GitHub `production` environment, configure required reviewers and allow
deployments only from `main`. Register a persistent Linux self-hosted runner on
the Docker host with the label `presentation-production`. The runner must use
version `2.327.1` or newer and its service account must be allowed to manage the
Presentation container.

### Protect main and deploy

Configure a branch ruleset for `main` that requires pull requests, blocks force
pushes and deletions, and requires these status checks:

- `Commit policy`
- `Secret scan`
- `Build and test`
- `Build container`

After CI publishes an image, run the `Deploy production` workflow. Leaving
`image_tag` empty deploys the immutable image for the selected `main` commit;
enter another published `sha-<full-commit>` tag to deploy a specific revision.
The deploy workflow injects `ConnectionStrings__Presentation` and `Admin__Key`
only at container startup, waits for Docker health, and restores the previous
container if the replacement fails.

Production migrations are intentionally separate from application deployment.
Apply them with `presentation_migrator` before deploying a release that contains
new migrations. Place TLS termination and public routing in front of port
`8080`; the container itself serves HTTP.
