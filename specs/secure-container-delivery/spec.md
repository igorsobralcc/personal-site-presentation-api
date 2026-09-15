# Feature: Secure container delivery

- Status: Approved
- Owner: Igor Sobral
- Last updated: 2026-09-15

## Outcome

Every proposed change is checked for repository policy, committed secrets,
build correctness, PostgreSQL compatibility, and container buildability before
an image can be published. Approved images can be deployed without embedding
database credentials in source control, workflow files, image layers, or logs.

## In scope

- GitHub Actions validation for pull requests, `main` pushes, and manual runs.
- Conventional Commit validation for every commit introduced by a change.
- Secret scanning across Git history.
- Release build plus fast and PostgreSQL integration tests.
- A production, non-root OCI image with a liveness health check.
- Publishing immutable commit-tagged images and `latest` to GitHub Container
  Registry after all validation gates pass on `main`.
- Manual deployment to a persistent Docker host through a protected GitHub
  `production` environment and a labeled self-hosted Linux runner.
- Runtime injection of the Presentation connection string and admin key from
  GitHub environment secrets.

## Out of scope

- Provisioning the PostgreSQL server, deployment host, TLS termination, DNS,
  firewall, backup, or self-hosted GitHub runner.
- Provider-specific deployment definitions for Kubernetes or a cloud platform.
- Automatically applying production migrations; migrations continue to use the
  separately privileged Presentation migrator role.
- Changing the HTTP API or its OpenAPI contract.

## HTTP contract

No route, schema, header, status code, or OpenAPI change is required. The
container exposes HTTP port `8080` and uses `GET /health/live` for its container
health check.

## Data and migrations

No model or migration change is required. CI creates an ephemeral PostgreSQL 18
service and runs the checked-in Presentation migrations through the existing
integration test. The deployed application receives the least-privilege
runtime connection string and does not apply migrations outside Development.

## Security and privacy

- The production environment secrets are named
  `PRESENTATION_CONNECTION_STRING` and `PRESENTATION_ADMIN_KEY`.
- Secrets are provided only to the deployment step. Pull-request validation and
  image builds never receive production secrets.
- Docker receives secrets by environment-variable name so workflow commands do
  not interpolate or print their values.
- The connection string and admin key are runtime environment variables and are
  never Docker build arguments, image labels, or tracked files.
- Workflow permissions use least privilege. Only the image-publishing job can
  write packages; deployment can only read packages.
- Third-party actions are pinned to immutable commit SHAs with version comments.
- The runtime image executes as the non-root user supplied by the .NET image.
- The GitHub `production` environment should require approval and restrict
  deployment branches to `main`.

## Failure and operational behavior

- A malformed commit subject, detected secret, restore/build/test failure,
  PostgreSQL integration failure, or image-build failure blocks publication.
- Missing deployment secrets fail before the existing container is changed.
- A deployment pulls an immutable `sha-<commit>` tag by default, starts a
  replacement container, and verifies Docker health before removing the prior
  container.
- A failed replacement is removed and the prior healthy container remains
  available.
- Concurrent CI runs for the same ref are cancelled; production deployments are
  serialized.

## Acceptance scenarios

### Scenario: Reject a non-conforming commit

- Given a pull request introduces a commit that does not follow
  `<type>(<optional-scope>): <imperative summary>`
- When the CI workflow validates the pull-request commit range
- Then the commit-policy job fails and no image is published.

### Scenario: Verify application and database behavior

- Given a pull request targets `main`
- When CI runs
- Then Release restore/build/tests complete against an ephemeral PostgreSQL 18
  service and the production container image builds successfully.

### Scenario: Reject a committed secret

- Given Git history contains a value detected as a secret
- When the security job scans the full checkout
- Then CI fails without publishing an image.

### Scenario: Publish a verified image

- Given a conforming commit reaches `main`
- When all validation jobs pass
- Then GHCR receives `sha-<commit>` and `latest` tags for the same image and no
  production secret is available to the build.

### Scenario: Inject secrets only at runtime

- Given the `production` environment contains both required secrets and an
  approved self-hosted runner is online
- When an operator dispatches the deployment workflow for a published tag
- Then the new container receives `ConnectionStrings__Presentation` and
  `Admin__Key` at runtime and the values are absent from workflow commands and
  image history.

### Scenario: Preserve the running version after a failed replacement

- Given a healthy Presentation container is running
- When a replacement fails to become healthy
- Then the replacement is removed, the previous container remains available,
  and the deployment job fails.

## Test evidence

- `bash scripts/validate-commits.sh --self-test` passed valid and invalid
  Conventional Commit fixtures on 2026-08-24.
- Actionlint 1.7.12 passed both workflow files on 2026-08-24.
- `dotnet test --configuration Release --no-build --no-restore` passed all 17
  tests, including the PostgreSQL migration test against a disposable local
  PostgreSQL 18 database, on 2026-08-24.
- `dotnet publish` completed in Release on 2026-08-24.
- Structural validation confirmed all 14 action references use immutable SHAs,
  the final image selects `$APP_UID`, and the production connection secret is
  referenced only by the deployment workflow.
- Microsoft Container Registry returned `200` for both digest-pinned .NET 10
  Alpine base-image manifests on 2026-08-24.
- Docker Hub returned `200` for the digest-pinned PostgreSQL 18 Alpine CI
  service manifest on 2026-08-24.
- The CI `Build container` job is the authoritative Docker build verification;
  Docker is not installed in the local development environment.

## Decisions and open questions

- Decision: GHCR is the image registry and `GITHUB_TOKEN` authenticates publish
  and pull operations.
- Decision: the generic durable target is a self-hosted Linux runner labeled
  `presentation-production`; provider-specific deployment can replace this job.
- Decision: deployment is manual and protected by the GitHub `production`
  environment rather than automatic on every `main` push.
- Decision: branch protection must require the CI jobs; workflows alone cannot
  prevent an administrator from bypassing repository policy.
- Open question: choose the long-term hosting provider and TLS termination
  before exposing the container publicly.

## Completion review — 2026-09-15

Previous lifecycle state: Implemented. This revision is Draft; existing evidence does not establish completion of the added requirements. Earlier decisions remain historical where explicitly superseded below.

The original non-root image, pinned actions/images, runtime-only secrets, manual protected deployment, and separate migrator decisions remain unchanged. This Draft revision adds production verification evidence; it does not claim provisioning, TLS, backup, or live settings were completed.

### PCD-001: Verify runtime health prerequisites

- Given the actual built runtime image and configured database
- When disposable-host smoke checks run
- Then the non-root identity, application port, installed health-check executable, liveness, and bounded database readiness work in that image.
- Evidence: container build/runtime and dependency-failure checks. Liveness health alone does not establish database readiness.

### PCD-002: Promote only with deployment prerequisites

- Given a proposed public deployment
- When the release checklist is reviewed
- Then selected hosting/TLS, public origin/CORS, runtime secrets, separately privileged migration sequence, schema privilege grants, and [repository/environment protections](../repository-policy/spec.md) have dated secret-free verification records.
- Evidence: deployment checklist, role tests, and settings/run references. Long-term host and TLS remain open choices.

### PCD-003: Verify rollback and interruption honestly

- Given the previous compatible container is healthy
- When replacement fails startup or readiness
- Then the replacement is removed and the previous instance is restored; any service interruption and schema compatibility/rollback limitations are documented and tested.
- Evidence: disposable-host failure injection. The existing stop/rename/start strategy must not be described as uninterrupted deployment.

### PCD-004: Document data recovery prerequisites

- Given a schema-changing release or data loss incident
- When production readiness is assessed
- Then a named operator, backup/restore procedure, recovery objectives, and migration rollback limits are recorded and a recovery exercise reference is available before public production claims.
- Evidence: operational prerequisites checklist. Choose the objectives/provider separately; provisioning and production migrations remain outside automatic workflow scope.

## Implementation revision — 2026-09-15

The owner subsequently requested implementation of the pending scopes. [adopted mutation/cache decision](../mutation-validation/decisions/DR-001-edge-semantics.md) and [implementation/verification DR-002](../documentation-reconciliation/decisions/DR-002-implementation-and-verification.md) govern this revision. Earlier authoring-only and undecided statements above are historical; they do not describe the new implementation scope.

[Current implementation evidence](../../docs/implementation-evidence.md) records local results and explicitly pending external evidence. [Acceptance dispositions](../../scripts/acceptance-evidence.json) and [exact executable mappings](../../scripts/feature-acceptance-map.json) retain stable IDs. This revision must not be marked Implemented while mandatory evidence remains pending.
