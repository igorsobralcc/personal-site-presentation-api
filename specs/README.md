# Feature specifications

Create one directory per independently deliverable API behavior. Current
implemented specifications are:

```text
specs/
  platform-foundation/spec.md
  public-presentation/spec.md
  profile-management/spec.md
  experience-management/spec.md
  project-management/spec.md
  skill-category-management/spec.md
  skill-management/spec.md
  technology-management/spec.md
  operational-health/spec.md
  development-seed-data/spec.md
  secure-container-delivery/spec.md
```

Use `_template/spec.md`. Specifications are permanent product documentation,
not temporary planning notes, and evolve through `Draft`, `Approved`, and
`Implemented` states.

Runtime application-flow coverage and pessimistic test conventions are indexed
in [`application-flow-index.md`](application-flow-index.md). Each runtime flow
is specified in the owning feature specification rather than duplicated in a
separate test-only contract.
