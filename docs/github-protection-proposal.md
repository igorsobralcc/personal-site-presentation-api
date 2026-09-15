# Approved and applied live GitHub protections

Status: explicitly approved by the owner, applied and independently reread on 2026-09-15. Payload filenames preserve the reviewed proposal.
Repository: igorsobralcc/personal-site-presentation-api. Date: 2026-09-15.

Initial read-only inspection found main unprotected, repository rulesets empty,
and production without reviewer or deployment-ref policy. The
[repository-policy specification](../specs/repository-policy/spec.md) assigns
confirmation of exact checks, bypass policy, reviewers and refs to an administrator.

The [main protection payload](github-main-protection.proposed.json) requires pull
requests and the four documented CI checks, requires the branch to be up to date,
blocks force pushes/deletion, requires resolved conversations, and applies checks
to administrators. It adds no mandatory human PR approvals, preserving a solo
repository workflow; automated checks remain mandatory.

The [production payload](github-production-protection.proposed.json) requires
manual approval from the verified owner account `igorsobralcc` (user ID 70531683).
It permits that owner to approve their own dispatched deployment and retains
GitHub's existing administrator environment-bypass behavior. The
[ref payload](github-production-main-ref.proposed.json) limits deployments to main
through a custom branch policy. No new runner, secret, deployment or publishing
action is included.

Following explicit approval, the main PUT, production environment PUT and main-only
deployment policy POST succeeded. Independent GETs verified all three settings;
the main-only deployment policy ID is 60060116. Actual failing-PR, protected
deployment and real container execution evidence remain required separately.
