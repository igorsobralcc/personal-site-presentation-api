# Contributing

## Required method: spec-driven development

Every feature, bug fix that changes behavior, and breaking refactor starts with
a version-controlled specification under `specs/<feature-name>/spec.md`.
The checked-in OpenAPI document is part of the specification for every HTTP
change.

### Workflow

1. **Specify** — copy `specs/_template/spec.md`, describe the outcome and mark
   the specification `Draft`.
2. **Review** — resolve open questions and mark it `Approved` before production
   implementation begins.
3. **Contract** — update `docs/openapi.json` (canonical; generate YAML/client with `python scripts/contract-artifacts.py`) first for HTTP changes. Define data,
   security, validation, errors, caching, and migration effects.
4. **Prove** — add integration tests mapped to the acceptance scenarios and
   initially failing for the missing behavior.
5. **Implement** — implement the smallest vertical slice that satisfies the
   specification.
6. **Verify** — run relevant integration tests, migration checks, contract
   checks, and the production build.
7. **Reconcile** — update and reapprove the specification when a decision
   changes; never silently make the implementation the new specification.
8. **Complete** — mark the specification `Implemented` and record test evidence.

### API specification requirements

An API feature specification must cover:

- Routes, methods, request and response schemas, and status codes
- Authentication and authorization rules
- Validation rules and Problem Details responses
- Publication and ordering behavior
- Database changes, migration and rollback considerations
- Caching, idempotency, and concurrency behavior when applicable
- Logging, health, privacy, and operational failure behavior
- Backward compatibility within `/api/v1`

EF Core entities are implementation details. Public behavior must be specified
through OpenAPI, acceptance scenarios, and observable HTTP results.

### Pull request gate

A feature is incomplete unless its pull request links its specification, the
OpenAPI contract matches the implemented endpoints, acceptance scenarios map to
automated tests, relevant checks pass, and migrations are included when needed.

## Required method: Conventional Commits

Development must be recorded as a sequence of small, atomic commits using the
[Conventional Commits](https://www.conventionalcommits.org/) format:

```text
<type>(<optional-scope>): <imperative summary>
```

Allowed types are `feat`, `fix`, `docs`, `refactor`, `test`, `build`, `ci`,
`chore`, `perf`, `style`, and `revert`. Use `!` and a `BREAKING CHANGE:` footer
for breaking changes.

Examples:

```text
docs(experiences): approve management endpoint spec
test(experiences): cover unpublished entries
feat(experiences): add protected create endpoint
refactor(persistence): extract experience mapping
```

Commits must be dispersed throughout development at meaningful, working
checkpoints. Do not wait until the end of a feature and place the entire change
in one commit. A normal sequence is specification, OpenAPI contract, failing
tests, implementation, migration, and focused refinement.

Each commit must:

- Represent one coherent reason for change
- Avoid unrelated formatting, cleanup, or feature work
- Keep the repository buildable whenever practical
- Keep a schema migration with the model change that requires it
- Include tests with the behavior they prove, or in an immediately preceding
  test commit during the red-green cycle
- Be independently understandable and safely revertible
- Never contain secrets, generated local state, or temporary debugging changes

Use a `revert:` commit to undo shared history. Do not rewrite published history
to conceal intermediate development.

### Automated enforcement

The CI `Commit policy` job validates every commit introduced by a pull request
or push with [`scripts/validate-commits.sh`](scripts/validate-commits.sh). Keep
pull request titles Conventional Commit-compatible when using squash merge.

Repository administrators must protect `main` and require the `Commit policy`,
`Secret scan`, `Build and test`, and `Build container` checks. CI configuration
cannot prevent an administrator from bypassing an unprotected branch.

GitHub Actions dependencies must be pinned to immutable 40-character commit
SHAs with a version comment. Never make production secrets available to pull
request jobs or use them as Docker build arguments.

## Pending-specification implementation — 2026-09-15

Run `dotnet tool restore`, then `./scripts/verify.ps1` with the governed SDK, Python 3.10+, Docker and a disposable PostgreSQL 18 connection supplied through `PRESENTATION_TEST_CONNECTION_STRING`. The chain checks source policy, validator fixtures, generated contract/client drift, read-only formatting, analyzer-clean Release build, real HTTP/schema/persistence tests, repository and changed-feature coverage, migration drift/idempotent SQL, publish and actual image smoke. Local `-SkipContainer`/`-SkipPostgres` explicitly leave required infrastructure verification incomplete.

Refresh exact test-method traceability with `python scripts/generate-acceptance-map.py` after reviewing changed behavioral assertions. Existing grouped-type exceptions are inventoried in `scripts/source-policy-baseline.json`; new violations fail and resolved exceptions must be removed. Generated code/migrations/entry points are exempt.

Empty/unknown-only merge patches preserve state after If-Match validation. Stored URLs are limited to 2048 characters, highlights are unique after trim ignoring case, and pagination offsets must fit Int32. Public GET validators support weak/list/wildcard matches; updatedAt remains the maximum current visible timestamp and can decrease. See the [adopted decision](specs/mutation-validation/decisions/DR-001-edge-semantics.md).

[Implementation evidence](docs/implementation-evidence.md) records the actual local boundary. [Operations and recovery prerequisites](docs/operations.md) list pending host/TLS/CORS, secrets, migrator grants, GitHub protections and restore exercise records. Deployment checks readiness as well as liveness and causes service interruption during stop/rename/start; container rollback cannot undo incompatible migrations. Source files and fixtures do not establish live enforcement.
