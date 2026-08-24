# Personal Site Presentation API

An ASP.NET Core and PostgreSQL API for a personal portfolio. It exposes one
cacheable public presentation and secured administration endpoints for profile,
experience, projects, skills, categories, and technologies, with optimistic
concurrency, soft deletion, health checks, tests, and container delivery.

> Repository description (350 characters maximum): the paragraph above is 305
> characters including spaces and punctuation.

Blog articles and series are intentionally owned by the separate Blog API.

## Architecture

The service is a modular monolith built with .NET 10 Minimal APIs, Entity
Framework Core 10, Npgsql, and PostgreSQL. It uses vertical slices so HTTP
contracts, validation, orchestration, and persistence behavior stay close to
each feature.

```text
HTTP client
    |
    +-- GET /api/v1/presentation -------- public composite query
    |
    +-- /api/v1/admin/* ----------------- admin key + operation logging
    |                                             |
    +-- /health/live and /health/ready            |
                                                  v
                                      PresentationDbContext
                                                  |
                                      PostgreSQL presentation schema
```

```text
src/PersonalSite.Presentation.Api/
  Common/       authentication, pagination, concurrency, errors, health, logging
  Data/         entities, EF mappings, migrations, development seed data
  Features/     endpoint slices and their request/response contracts
tests/          HTTP acceptance, OpenAPI contract, seed, and PostgreSQL tests
specs/          approved behavior and acceptance scenarios
docs/           OpenAPI contract and Postman collection
```

Handlers use `PresentationDbContext` directly. There is no MediatR, generic
repository, separate persistence model, or distributed transaction. The
checked-in [OpenAPI contract](docs/openapi.yaml) and implemented
[specifications](specs/README.md) are the source of truth for observable
behavior.

### Database boundary

The Presentation and Blog APIs may share one logical PostgreSQL database, but
they remain isolated:

```text
personal_site
  presentation schema  -> Presentation principal, tables, migrations, history
  blog schema          -> Blog principal, tables, migrations, history
```

There are no cross-schema foreign keys, shared tables, shared migrations, or
direct reads between APIs. The database is owned by `postgres`; migrations use
`presentation_migrator`, while normal application traffic uses the
least-privilege `presentation_app` role.

## Business rules

### Shared resource behavior

- Managed records use UUIDv7 identifiers, UTC timestamps, soft deletion, and a
  positive integer concurrency version.
- Database names are `snake_case`; JSON fields are `camelCase`.
- Deleted records are hidden by default. Admin collections can include them
  with `X-Include-Deleted: true`.
- Admin pagination uses `X-Page` (default `1`) and `X-Page-Size` (default `20`,
  maximum `100`).
- Mutations emit an ETag. `PATCH`, `DELETE`, and restore require `If-Match`;
  missing and stale preconditions return `428` and `412`, respectively.
- PATCH uses `application/merge-patch+json`: omitted fields are preserved,
  explicit `null` clears nullable fields, and supplied arrays replace the whole
  child collection.
- DELETE is soft and idempotent for an already deleted resource. Foreign keys
  use `RESTRICT` or `NO ACTION`; database cascades are forbidden.
- Validation returns Problem Details with a trace ID. Persistence uniqueness or
  relationship conflicts return `409`.

### Profile

- Exactly one profile can exist; the singleton is enforced in PostgreSQL.
- `PUT /api/v1/admin/profile` initializes it and returns `409` if it exists.
- Social links are ordered by creation and replaced atomically when supplied.
- Profile content, including the configured public email, becomes public
  immediately.

### Experiences

- Experiences are ordered by start date descending, current roles first when
  dates tie, then end date and ID.
- End date cannot precede start date. Highlights and technology IDs must be
  unique, and referenced technologies must be active.
- Highlights and experience technologies are admin-only in API version 1.

### Projects

- Only active featured projects appear publicly, newest first.
- Repository, live, and image URLs must be absolute HTTPS URLs.
- Image URL, alternative text, width, and height must be supplied together;
  dimensions must be positive.
- Technology IDs must be unique and reference active technologies.

### Skills, categories, and technologies

- Category and technology names are unique case-insensitively among active
  records. Skill names are unique within their active category.
- Public categories and skills retain immutable creation order.
- A deleted category or technology cannot satisfy a new relationship.

### Public presentation

`GET /api/v1/presentation` returns the profile, experiences, grouped skills, and
featured projects in one projection. Deleted data, non-featured projects,
concurrency/deletion metadata, and admin-only experience details are omitted.
A missing profile returns `404`.

Responses use `Cache-Control: public,max-age=60,must-revalidate`. The ETag is
derived from public update timestamps; a matching `If-None-Match` returns `304`.

## HTTP endpoints

| Area | Routes |
| --- | --- |
| Public | `GET /api/v1/presentation` |
| Profile | `GET`, `PUT`, `PATCH /api/v1/admin/profile` |
| Experiences | collection CRUD, item CRUD, and restore under `/api/v1/admin/experiences` |
| Projects | collection CRUD, item CRUD, and restore under `/api/v1/admin/projects` |
| Skill categories | collection CRUD, item CRUD, and restore under `/api/v1/admin/skill-categories` |
| Skills | collection CRUD, item CRUD, and restore under `/api/v1/admin/skills` |
| Technologies | collection CRUD, item CRUD, and restore under `/api/v1/admin/technologies` |
| Operations | `GET /health/live`, `GET /health/ready`, Development OpenAPI at `/openapi/v1.json` |

Admin endpoints require `X-Admin-Key`. Outside Development they also require
HTTPS. Import the [Postman collection](docs/PersonalSite.Presentation.Api.postman_collection.json)
and set its `baseUrl` and `adminKey` variables. Its requests capture IDs and
ETags for subsequent mutations.

## Run locally

### Prerequisites

- .NET SDK 10
- PostgreSQL (the automated pipeline uses PostgreSQL 18)
- EF Core CLI: `dotnet tool install --global dotnet-ef --version 10.*`
- Optional: Postman and Docker

The examples use PowerShell and assume PostgreSQL is available on
`localhost:5432`. Replace every `<...>` placeholder locally and never commit
real credentials.

### 1. Restore the project

```powershell
git clone https://github.com/igorsobralcc/personal-site-presentation-api.git
Set-Location personal-site-presentation-api
dotnet restore
```

### 2. Create the database, roles, and grants

Open `psql` as the PostgreSQL owner:

```powershell
psql -U postgres -d postgres
```

Run the following SQL. `\connect` is a `psql` command, not SQL.

```sql
CREATE DATABASE personal_site OWNER postgres;

CREATE ROLE presentation_migrator
  LOGIN PASSWORD '<migrator-password>'
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;

CREATE ROLE presentation_app
  LOGIN PASSWORD '<app-password>'
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;

GRANT CONNECT ON DATABASE personal_site
  TO presentation_migrator, presentation_app;

\connect personal_site

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
CREATE SCHEMA IF NOT EXISTS presentation AUTHORIZATION postgres;

GRANT USAGE, CREATE ON SCHEMA presentation TO presentation_migrator;
GRANT USAGE ON SCHEMA presentation TO presentation_app;

ALTER DEFAULT PRIVILEGES FOR ROLE presentation_migrator
  IN SCHEMA presentation
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO presentation_app;

ALTER DEFAULT PRIVILEGES FOR ROLE presentation_migrator
  IN SCHEMA presentation
  GRANT USAGE, SELECT ON SEQUENCES TO presentation_app;
```

If the database or roles already exist, alter their passwords instead of
recreating them:

```sql
ALTER ROLE presentation_migrator PASSWORD '<migrator-password>';
ALTER ROLE presentation_app PASSWORD '<app-password>';
```

### 3. Apply migrations

Migrations use the elevated schema-scoped role; the application role must not
run them:

```powershell
dotnet ef database update `
  --project src/PersonalSite.Presentation.Api `
  --startup-project src/PersonalSite.Presentation.Api `
  --connection "Host=localhost;Port=5432;Database=personal_site;Username=presentation_migrator;Password=<migrator-password>"
```

After the first migration, explicitly grant access to all existing objects. The
default privileges above cover objects created by later migrations:

```sql
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA presentation
  TO presentation_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA presentation
  TO presentation_app;
```

### 4. Create the ignored local launch profile

`launchSettings.json` is deliberately absent from Git because it contains local
credentials. Create
`src/PersonalSite.Presentation.Api/Properties/launchSettings.json` with:

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://localhost:5074",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ConnectionStrings__Presentation": "Host=localhost;Port=5432;Database=personal_site;Username=presentation_app;Password=<app-password>",
        "Admin__Key": "<local-admin-key>",
        "Logging__EventLog__LogLevel__Default": "None",
        "SeedData__Enabled": "false"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7211;http://localhost:5074",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ConnectionStrings__Presentation": "Host=localhost;Port=5432;Database=personal_site;Username=presentation_app;Password=<app-password>",
        "Admin__Key": "<local-admin-key>",
        "Logging__EventLog__LogLevel__Default": "None",
        "SeedData__Enabled": "false"
      }
    }
  }
}
```

The Windows Event Log override prevents local requests from requiring elevated
Event Log permissions. For local HTTPS, trust the development certificate:

```powershell
dotnet dev-certs https --trust
```

### 5. Optionally load resume-derived development data

Seeding is Development-only, transactional, idempotent, and runs only when all
managed tables are empty. Because it applies migrations, temporarily use the
migrator connection and set `SeedData__Enabled` to `true` in the launch profile.
Run the application once, stop it, then restore the `presentation_app`
connection and `SeedData__Enabled=false`.

### 6. Run and verify

```powershell
dotnet run --launch-profile https --project src/PersonalSite.Presentation.Api
```

In another terminal:

```powershell
Invoke-RestMethod https://localhost:7211/health/live
Invoke-RestMethod https://localhost:7211/health/ready
Invoke-RestMethod https://localhost:7211/api/v1/presentation
```

Liveness reports whether the process can serve requests. Readiness performs a
database connection check with a three-second timeout and returns `503` when
PostgreSQL is unavailable without exposing credentials or topology.

### 7. Run tests

```powershell
dotnet build --configuration Release --no-restore --warnaserror
dotnet test --configuration Release --no-build --no-restore
```

The PostgreSQL schema-isolation test is opt-in and must target a disposable
database because it applies migrations:

```powershell
$env:PRESENTATION_TEST_CONNECTION_STRING = "Host=localhost;Port=5432;Database=presentation_test;Username=postgres;Password=<test-password>;SSL Mode=Disable"
dotnet test --configuration Release
Remove-Item Env:PRESENTATION_TEST_CONNECTION_STRING
```

## Production safety

### Application and database controls

- Production `appsettings.json` contains a blank connection string and admin
  key. GitHub environment secrets provide `ConnectionStrings__Presentation`
  and `Admin__Key` only when the container starts.
- The runtime role has data access but no schema-creation privilege. Migrations
  are a separate release step using `presentation_migrator`.
- Admin keys are compared in constant time and never logged. Production admin
  requests require HTTPS, and CORS allows only configured origins.
- Optimistic concurrency prevents lost updates; transactions keep aggregate
  root and child updates atomic; restricted foreign keys prevent accidental
  cascade deletion.
- Structured management logs contain operation, resource type/ID, result,
  duration, and trace ID—not request bodies or secrets.
- Separate liveness and readiness checks support safe routing and deployment.

### CI/CD and supply-chain controls

The [CI workflow](.github/workflows/ci.yml) runs on pull requests and `main`:

- Conventional Commit policy validation and validator self-tests.
- `actionlint` downloaded at a pinned version and verified by SHA-256.
- Full-history Gitleaks scanning.
- Release build with warnings as errors and tests against ephemeral PostgreSQL.
- Container build only after policy, secret scan, and tests pass.
- GitHub Actions and PostgreSQL/build images pinned to immutable digests or
  commit SHAs.

Successful `main` builds publish immutable `sha-<commit>` and convenience
`latest` tags to GHCR. The multi-stage Alpine image runs as a non-root user,
disables .NET diagnostics, exposes only port `8080`, and has a liveness-based
Docker health check. Secrets are never Docker build arguments or image layers.

The manually triggered [production deployment](.github/workflows/deploy.yml)
uses a protected GitHub environment and a dedicated self-hosted runner. It
validates required secrets, deploys the selected immutable image, waits for
health, and automatically restores the previous container on failure. TLS must
terminate at a reverse proxy in front of container port `8080`.

Configure production secrets without placing values in shell history:

```powershell
gh secret set PRESENTATION_CONNECTION_STRING --env production --repo igorsobralcc/personal-site-presentation-api
gh secret set PRESENTATION_ADMIN_KEY --env production --repo igorsobralcc/personal-site-presentation-api
```

Protect `main` with pull requests, blocked force-push/deletion, and the required
`Commit policy`, `Secret scan`, `Build and test`, and `Build container` checks.

## Key trade-offs

| Decision | Benefit | Trade-off / alternative |
| --- | --- | --- |
| Modular monolith + vertical slices | Simple deployment and feature locality | Microservices isolate scaling/failures better but add networking and operational cost. |
| Direct EF Core access | Less abstraction and clearer queries | Repositories can isolate persistence but often duplicate EF capabilities. |
| Static admin key | Small, practical first-release attack surface | OIDC gives identity, rotation, and granular roles but needs an identity provider and more flows. |
| Soft deletion | Recovery and stable references | Hard deletion reduces retained data but is harder to undo and audit. |
| Optimistic concurrency with ETags | Prevents silent lost updates without locks | Pessimistic locks simplify some contention cases but reduce throughput and complicate HTTP usage. |
| One public composite endpoint | One cacheable page-load request | Separate resources offer finer caching but require more round trips and client orchestration. |
| Shared database, isolated schemas | Lower infrastructure cost with clear ownership | Separate databases improve fault isolation but increase provisioning and operational overhead. |
| Separate migrations and deployment | Runtime uses least privilege and rollout is explicit | Startup migrations are simpler locally but give the application dangerous production DDL rights. |

## Development workflow

Behavior changes are spec-driven: update an approved file under `specs/`, update
`docs/openapi.yaml` before HTTP implementation, add acceptance tests, implement,
and verify. Commits must follow Conventional Commits and remain small, coherent,
buildable, secret-free, and independently revertible. See
[CONTRIBUTING.md](CONTRIBUTING.md).
