# Deployment and recovery prerequisites

These are required operator checks. Repository source and local fixtures do not
prove that a public production deployment is ready.

## Promotion record

Record the date, immutable revision/image digest, named operator, environment,
result and reproducible run/settings references for each prerequisite. Keep
credentials, request bodies and connection strings out of the record.

| Requirement | Current disposition | Required evidence |
| --- | --- | --- |
| Long-term host and TLS termination | Pending owner selection | Host ownership, certificate/renewal check, HTTPS probe |
| Public origin and CORS | Pending actual origin | Allowed-origin and denied-origin HTTP checks |
| Runtime secrets | Pending environment verification | Presence/rotation record for PRESENTATION_CONNECTION_STRING and PRESENTATION_ADMIN_KEY |
| Migration sequence and grants | Disposable role checks only | Backup first; privileged migrator applies compatible SQL; runtime has schema usage and table DML, with no DDL/blog/history writes |
| Protected main | Configured and independently verified after owner approval (2026-09-15) | Ruleset export and failing-PR run for Commit policy, Secret scan, Build and test, Build container; document bypass policy |
| Protected production environment | Owner approval and main-only refs configured and independently verified (2026-09-15) | Required reviewers, main-only deployment refs, dedicated runner ownership and package-read credentials |
| Real image runtime and rollback | Pending Docker/host execution | CI smoke run plus disposable-host startup/readiness/interruption failure results |
| Data recovery | Pending operator/provider/objectives | Named recovery operator, RPO, RTO, encrypted isolated backup retention, restore procedure and dated exercise |

## Replacement and rollback

The deployment workflow accepts immutable `sha-<40-character commit>` tags on
main, injects secrets at runtime, and waits for both Docker liveness health and
database readiness. Startup, unhealthy/readiness timeout, and shell interruption
trigger restoration of the previous container. Deterministic fixtures exercise
the exact workflow shell, with a Docker substitute; actual host evidence remains
required.

Deployment stops and renames the previous container before starting the new one.
There is a service interruption during replacement and rollback. A prior image
must remain compatible with the current schema. Container rollback never undoes
a migration or restores lost data. Process/host termination that cannot deliver
a shell signal needs an operator to inspect the `-previous` container and restore
it manually. Avoid irreversible schema changes during the compatibility window.

## Recovery exercise

Before a schema-changing production release, the operator must record recovery
objectives and verify a restore of a recent backup into an isolated database.
Apply the recorded migration sequence with a separately privileged role; verify
schema/history isolation, row counts and critical HTTP flows with the runtime
role. Record recovery duration and data-loss window against RTO/RPO, then dispose
of the isolated environment according to the chosen retention policy. Provider
backup/restore commands and retention durations require the actual operator and
provider choices; this repository does not provision or mutate production.

## Local and CI verification

Use `scripts/verify.ps1` with Python 3.10+, the SDK in `global.json`, the governed
dotnet-ef tool, Docker and a disposable PostgreSQL 18 database. PostgreSQL tests
truncate application tables and create transaction-scoped role/schema fixtures;
never point them at shared or production data. CI uses only disposable credentials.
The CI test job requires PostgreSQL and runs formatting without rewriting files.
The separate container job loads and executes the actual image before publishing.

Local `-SkipContainer` and `-SkipPostgres` switches explicitly report partial
verification. Missing database configuration without `-SkipPostgres` fails before
checks begin. `PRESENTATION_REQUIRE_POSTGRES=true` prevents conditional PostgreSQL
facts from silently skipping in the required lane.

[Reviewed GitHub protections](github-protection-proposal.md) were explicitly approved, applied and independently verified on 2026-09-15. Actual protected execution and dedicated runner ownership evidence remain pending.
