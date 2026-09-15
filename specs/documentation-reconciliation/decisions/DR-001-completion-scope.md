# DR-001: Keep completion scope and evidence within this application

Status: Accepted
Date: 2026-09-15
Specification: [Documentation and decision reconciliation](../spec.md)

## Context

The owner reviewed application-separated completion topics and requested updates to existing specifications and new specifications for uncovered topics.

## Options considered

- A shared cross-repository document with loosely owned requirements.
- Application-owned specifications that reuse established feature and engineering owners.

## Decision

Maintain the completion backlog in this application's specs directory, update existing owners, and add a new owner only for uncovered scope.

## Rationale

Local ownership makes implementation, contracts, tests, lifecycle state, and external prerequisites traceable without a shared specification drifting from three codebases.

## Consequences

Existing implementation evidence is retained. Materially revised or new scopes are Draft for behavioral review; approval of topic titles does not settle open questions, claim completion, or authorize implementation or deployment. Historical decisions remain discoverable.

## Verification or follow-up

Validate indexes, links, stable acceptance IDs, and lifecycle/evidence statements. Record each later consequential adopted behavior in its own decision record before implementation.

## Successor — 2026-09-15

The authoring-only scope remains historical. The owner subsequently requested implementation; [DR-002](DR-002-implementation-and-verification.md) records the adopted successor scope and verification boundaries.
