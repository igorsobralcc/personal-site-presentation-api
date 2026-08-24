# Personal Site Presentation API

ASP.NET Core API that owns the public presentation content for the personal
site. It provides one read-optimized public representation and protected CRUD
operations for profile, experience, projects, and skills.

## Scope

The MVP owns:

- A singleton profile with headline, biography, location, contact, and links
- Ordered experience entries
- Ordered skills grouped by category
- Ordered projects, including featured status and external links
- Publication state so unfinished content is never exposed publicly

Blog posts do not belong in this service. They will be owned by the separate
Blog API when that phase begins.

## Architecture

This API is a modular monolith using vertical slices. The domain is small, so
the design avoids distributed services, a generic repository layer, MediatR,
and other ceremony that would not currently protect a real boundary.

```text
src/
  PersonalSite.Presentation.Api/
    Common/          cross-cutting HTTP, auth, errors, observability
    Data/            EF Core context, mappings, migrations, seed data
    Features/
      Presentation/  public composite read model
      Profile/       admin singleton queries and update
      Experiences/   admin CRUD
      Projects/      admin CRUD
      Skills/        admin CRUD
tests/
  PersonalSite.Presentation.Api.Tests/
```

Each feature contains its endpoint mapping, request/response contracts,
validation, and handler. Endpoints depend directly on the EF Core context when
that is the simplest honest abstraction. Domain behavior is extracted only
when it is reused or has meaningful invariants.

## Development method

Every feature and behavior change uses spec-driven development. The feature
specification and, when applicable, the OpenAPI contract and database migration
are reviewed before production implementation begins. Development is committed
incrementally using Conventional Commits so each coherent change can be
reverted safely. See
[CONTRIBUTING.md](CONTRIBUTING.md).

## Planned stack

- .NET 10 and ASP.NET Core Minimal APIs
- Entity Framework Core with PostgreSQL
- Built-in OpenAPI generation and Scalar for local API exploration
- Built-in Problem Details for consistent errors
- xUnit plus `WebApplicationFactory` for integration tests
- Health checks and structured logging

## HTTP contract

The source-of-truth contract is [docs/openapi.yaml](docs/openapi.yaml).

### Public read

```http
GET /api/v1/presentation
```

The response is a page-shaped read model containing only published records. It
is ordered by the API and designed to render the first page without additional
requests.

### Protected management

```text
GET, PUT             /api/v1/admin/profile
GET, POST            /api/v1/admin/experiences
GET, PUT, DELETE     /api/v1/admin/experiences/{id}
GET, POST            /api/v1/admin/projects
GET, PUT, DELETE     /api/v1/admin/projects/{id}
GET, POST            /api/v1/admin/skills
GET, PUT, DELETE     /api/v1/admin/skills/{id}
```

Administrative routes require `X-Admin-Key` over HTTPS in the first release.
The key is a server-side secret and must not be embedded in the public React
bundle. This deliberately small authentication boundary can later be replaced
with OIDC without changing the public contract.

## API conventions

- JSON properties use `camelCase`.
- Dates use ISO 8601 calendar dates (`YYYY-MM-DD`); `endDate: null` means current.
- Resource identifiers are UUIDs represented as strings.
- Create returns `201 Created` with a `Location` header.
- Update returns `200 OK`; delete returns `204 No Content`.
- Validation uses `application/problem+json` with field errors.
- Missing resources return `404`; malformed requests return `400`.
- Public responses emit `ETag` and a short `Cache-Control` policy.
- The public DTO is separate from persistence entities and never exposes draft
  records or administrative metadata.
- CORS uses an explicit configured origin list; wildcard origins are not used.

## Data and change strategy

PostgreSQL is the system of record. Schema changes are made through reviewed EF
Core migrations. The API seeds useful local development content only in the
development environment. Production content is managed through protected CRUD
operations.

API additions should be backward compatible inside `/api/v1`. Breaking field
or behavior changes require `/api/v2`; database migrations alone do not imply
an HTTP API version change.

## Delivery order

1. Scaffold the solution and vertical-slice folders.
2. Add persistence models, mappings, migrations, and development seed data.
3. Implement protected CRUD endpoints and validation.
4. Implement the public composite projection, caching, and ETag behavior.
5. Generate and verify OpenAPI against the checked-in contract.
6. Add integration tests, health checks, and deployment configuration.
