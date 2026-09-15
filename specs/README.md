# personal-site-presentation-api: specification index

Owner: Igor. Updated: 2026-09-15.

Current delivery: mutation/cache and documentation revisions are Implemented. Engineering, persistence/CI, container and repository-policy revisions remain Approved with explicit external evidence gaps. [The local verification record](../docs/implementation-evidence.md) reports 158 passing tests, no skips and 97.11% line coverage.

Specifications are permanent product documentation. New completion scopes and materially revised requirements are **Draft** for behavior review. Approval of the completion topics does not resolve open choices or mean implementation is complete. Existing baseline evidence is retained; **Implemented** means only the explicitly verified scope.

Use [_template/spec.md](_template/spec.md). The workflow is Draft â†’ Approved â†’ Implemented; superseded decisions remain discoverable with successor links. Required scenario evidence, contracts, migrations, documentation, and external prerequisites must be complete before the final transition.

## Application-owned specifications

| Specification | Status |
| --- | --- |
| [Platform foundation](platform-foundation/spec.md) | Implemented |
| [Public presentation and cache semantics](public-presentation/spec.md) | Implemented |
| [Profile management](profile-management/spec.md) | Implemented |
| [Experience management](experience-management/spec.md) | Implemented |
| [Project management](project-management/spec.md) | Implemented |
| [Skill category management](skill-category-management/spec.md) | Implemented |
| [Skill management](skill-management/spec.md) | Implemented |
| [Technology management](technology-management/spec.md) | Implemented |
| [Operational health](operational-health/spec.md) | Implemented |
| [Development seed data](development-seed-data/spec.md) | Implemented |
| [Secure container delivery and recovery verification](secure-container-delivery/spec.md) | Approved |
| [Automated code quality and acceptance gates](engineering-quality/spec.md) | Approved |
| [Mutation and validation edge semantics](mutation-validation/spec.md) | Implemented |
| [Persistence and full HTTP contract verification](persistence-contract-verification/spec.md) | Approved |
| [Documentation and decision reconciliation](documentation-reconciliation/spec.md) | Implemented |
| [Repository policy and CI enforcement](repository-policy/spec.md) | Approved |

## Dependency order

1. Review mutation/validation and public-cache choices; reapprove changed product owners and OpenAPI before implementation.
2. Complete full contract, PostgreSQL, role-isolation, and generated-client evidence.
3. Enforce explicit formatting/quality gates and reconcile acceptance mappings.
4. Complete container/recovery checks and external deployment/repository prerequisites.

[Application-flow evidence](application-flow-index.md) retains the 183 non-characterization mappings and fourteen outstanding characterizations. Implemented feature statuses refer to the existing baseline, not unresolved Draft successors.

## Decisions and evidence

[DR-001: application-owned completion scope](documentation-reconciliation/decisions/DR-001-completion-scope.md) records the owner's requested organization. Later consequential behavior choices need their own approved decision record; no implementation, publishing, production mutation, or external settings changes were performed by this specification update.

## Implementation successor — 2026-09-15

The owner requested implementation after the authoring revision above. Pending scopes are Approved under the [adopted implementation decision](documentation-reconciliation/decisions/DR-002-implementation-and-verification.md); unresolved product cache/mutation choices above are resolved by [edge DR-001](mutation-validation/decisions/DR-001-edge-semantics.md). Earlier authoring statements remain historical. [Current evidence](../docs/implementation-evidence.md) records local verification and visibly pending real CI/container/settings/recovery evidence.

Current delivery: mutation/cache and documentation successors are Implemented with local HTTP/PostgreSQL evidence. Engineering, persistence/CI, secure-container and repository-policy scopes remain Approved with explicit pending external prerequisites. Main required checks and production owner approval/main-only refs were explicitly approved, applied and independently verified on 2026-09-15. Actual protected execution evidence remains pending.
