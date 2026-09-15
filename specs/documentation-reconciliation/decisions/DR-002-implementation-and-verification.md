# DR-002: Implement the pending application completion scopes

- Status: Adopted under the owner's implementation request.
- Date: 2026-09-15
- Supersedes: DR-001's authoring-only scope for this new implementation request.

The owner requested implementation of the pending Presentation API specifications.
This authorizes local source, contract, test and workflow changes. It does not
select a production hosting provider, recovery objectives or change live settings.

Behavior defaults are recorded in
[mutation/cache DR-001](../../mutation-validation/decisions/DR-001-edge-semantics.md).
The SDK uses the installed 10.0.302 feature band with patch roll-forward. Existing
analyzers, test framework and package versions are retained; test analyzers now
use the same warnings-as-errors policy. Existing grouped source types are
inventoried in `scripts/source-policy-baseline.json`; new violations fail and
resolved entries must be removed. Formatting is brought to the configured
baseline without unrelated source restructuring.

`docs/openapi.json` becomes the canonical reviewed contract. A dependency-free
Python generator produces YAML and a C# client, and check mode detects drift.
Generated runtime OpenAPI verifies inferred methods/paths/request media before
applying reviewed filter, header and merge-patch metadata. Independent response
validation runs over actual fast and PostgreSQL HTTP responses, checking schema,
statuses, media, required headers and bodyless responses. The client intentionally
returns HttpResponseMessage so callers retain access to errors, headers and 304.
Callers own its disposal and configure administrator credentials on HttpClient.

Changed-feature coverage includes each governed class's async state machines and
lambdas, deduplicated by filename/line. Each boundary must reach 90%; the existing
repository 90% gate and generated exclusions remain. Required PostgreSQL checks
fail when configuration is absent. Container smoke checks and publication are
ordered behind the same source verification chain.

Production promotion is restricted to main and immutable commit tags. Previous
containers are removed only after liveness and database readiness pass; error and
signal rollback preserves the previous instance across stop/rename/start phases.
This strategy causes service interruption and cannot undo incompatible migrations.

Current source/test evidence and external prerequisites are recorded in
[implementation evidence](../../../docs/implementation-evidence.md) and
[operations](../../../docs/operations.md). No publishing, production migrations,
deployment, messaging, live settings changes or package upgrades are performed.

Request DTO field/type/array-item compatibility is checked against inferred JSON metadata before enrichment. Validation refines binder nullability/required fields and string formats. JSON numbers use strict numeric binding to match the documented dimension types. Framework bodyless errors receive traceable Problem Details through status-code pages; custom problems explicitly retain a type URI.

Additional documented validation boundaries now reject null social-link items, null/missing image dimensions and invalid JSON field types with keyed 400s. Merge-patch numeric binding is strict as well. Request binding failures do not become generic 500s; unexpected serializer/database failures keep their distinct server-error boundary. The canonical contract resource is explicitly copied into the Docker build context.

## Approved external settings successor — 2026-09-15

The owner explicitly approved the concrete GitHub protection proposal. Main required checks and production owner approval/main-only refs were applied and independently reread successfully. This supersedes the earlier no-live-settings scope only for those reviewed settings; production deployment, publishing and migrations were not performed. See [approved settings and evidence](../../../docs/github-protection-proposal.md). Actual protected-run and runner/recovery evidence remains pending.

## Delivery branch model — 2026-09-15

The owner selected the Gitflow skill for this delivery and explicitly named `main` as the PR target. The feature branch `feature/presentation-specification-completion` starts from updated `main`; delivery uses small Conventional Commits and a protected PR into `main`. This owner-selected target is the permitted delivery deviation from integration through `develop`. Existing branch names and release/version conventions are retained; this delivery creates no integration/release branch or release tag and performs no merge. The owner authorized preservation of local work and incorporation of upstream README commit 2ee1ea2 before branching.
