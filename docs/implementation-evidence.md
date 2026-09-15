# Presentation API implementation evidence — 2026-09-15

Scope: owner's request to implement pending Presentation API specifications.
Environment: local Windows, .NET SDK 10.0.302, PostgreSQL 18 disposable cluster
on loopback port 55439. Base revision: 992b411e370190ee5c7891a73b91eba82e0ea319,
with existing owner specification edits preserved. Publication uses atomic commits on
`feature/presentation-specification-completion`, based on updated main
2ee1ea2 (only the upstream README character-count note removal differs from the
original verified base).
No production deployment or migration was performed. GitHub protections were subsequently applied after explicit owner approval, as recorded below.

## Evidence boundaries

| Scope | Reproducible evidence | Result |
| --- | --- | --- |
| Mutation/cache decisions | MutationEdgeTests, CacheEdgeTests, InitializationFailureTests | Local HTTP boundary tests |
| Real persistence | PostgreSqlIntegrationTests and PostgreSqlCompletionTests | Disposable PostgreSQL migration, constraints, rollback, races, URL/query and role checks |
| Full HTTP contract | ContractResponseHandler across fast/relational suites, ContractTests and GeneratedClientTests | Real response schema/status/media/header validation; document drift and client smoke |
| Source, links, traceability, workflow policy | scripts/validate-repository.py and scripts/test_validators.py | Positive and deliberate-negative fixtures |
| Deployment rollback source | scripts/test_deployment.py | Exact workflow shell exercised with a Docker substitute; no actual container/host guarantee |
| Formatting, analyzers, coverage, migrations, publish | scripts/verify.ps1 -SkipContainer | Local verification record below |
| Container runtime | CI Build container plus scripts/smoke-container.sh | Pending actual Docker execution; Docker unavailable locally |
| Live enforcement/production recovery | docs/operations.md | GitHub settings verified; protected run, hosting/TLS and recovery records pending |

## Current run

`scripts/verify.ps1 -SkipContainer` completed successfully: 158 .NET tests
passed, zero skipped, with all 12 PostgreSQL cases executed. Twelve Python
validator/deployment fixture tests passed. Repository line coverage is 97.11%;
all thirteen governed changed-feature boundaries reached at least 90%, including
async state machines and lambdas. Read-only formatting, analyzer-clean Release
build, model drift check, idempotent migration SQL and Release publish passed.
Actionlint 1.7.12 (checksum-verified Windows binary, shellcheck/pyflakes disabled)
and the Conventional Commit validator self-test passed. Postman JSON and
`git diff --check` passed.
Ignored `TestResults/` logs, TRX, Cobertura and idempotent SQL are reproducible local
artifacts, not permanently hosted CI run links. A passing local chain does not
establish that GitHub rules or environment protections are configured.

## Acceptance evidence

`scripts/acceptance-evidence.json` lists precise assertions/references and pending
dispositions for completion IDs. Existing feature traits are checked against the
authoritative matrices. Trait existence establishes a mapping; detailed named
tests and runtime schema assertions establish the observed local boundary.
Adapter-dependent cases require the PostgreSQL lane; fixture outcomes do not
claim production/provider behavior.

## Read-only GitHub inspection — 2026-09-15

GitHub API returned `Branch not protected` for main and an empty repository
ruleset list. The production environment exists with an empty protection-rule
list and no deployment branch policy. This was the initial inspection before the owner approved and applied the protections recorded below.

Recent CI runs on other revisions failed, including
[run 34891008982](https://github.com/igorsobralcc/personal-site-presentation-api/actions/runs/34891008982)
and [run 34891000987](https://github.com/igorsobralcc/personal-site-presentation-api/actions/runs/34891000987)
on 2026-09-14. Successful dependency-update runs are not application/container
verification. At the local verification checkpoint the new source had no live CI run. The
publication PR runs the required checks; its dated results are separate evidence.

The required-PostgreSQL negative fixture also passed: with `PRESENTATION_REQUIRE_POSTGRES=true` and no connection, a selected relational test failed immediately and zero tests skipped. This is an expected gate-failure fixture, separate from the 158-test successful configured run. The owner explicitly approved the [live protection payloads](github-protection-proposal.md). They were applied and independently reread on 2026-09-15: main requires all four checks with strict updates, administrator enforcement, and force-push/deletion disabled; production requires reviewer igorsobralcc (70531683), allows self-review, and uses only the main branch policy (60060116). Production retains existing administrator bypass behavior. Actual failing-PR and protected-deployment exercises remain pending; settings verification does not establish their execution.
