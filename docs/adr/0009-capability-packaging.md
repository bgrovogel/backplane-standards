# 0009. How a capability's service code is packaged

**Status:** Open; **a spike in roadmap phase 2 decides** · **Date:** 2026-11-03 · **Decided by:** to be decided after the spike

## Context

A capability's Lambda code must reach the application's AWS account. The construct that deploys it needs a deployment artifact. The options differ in who builds it and how the version is carried ([Capability modules](../08-capability-modules.md#how-the-service-code-is-packaged)).

## Decision

Not yet decided. The proposal under test is **A: a prebuilt, versioned deployment artifact shipped inside the construct package** and referenced as a CDK asset, so an application builds nothing for a capability.

## Alternatives considered

| Option                                                             | Good                                                                  | Costs                                                                  | Notes    |
| ------------------------------------------------------------------ | --------------------------------------------------------------------- | ---------------------------------------------------------------------- | -------- |
| **A. Prebuilt artifact inside the construct package**              | No build step for the app; artifact version equals capability version | A build and publish step in the capability repository; larger packages | Proposed |
| B. The application compiles the capability's functions from source | Full source visible                                                   | Every application's build is slower and must know how to package it    |          |
| C. A container image in a registry                                 | Same artifact everywhere                                              | A registry to run and scan                                             |          |

## What the spike must show

Using the email capability as the test: (1) an application adds it in under ten lines; (2) the artifact is reproducible from the tagged source; (3) upgrading the capability changes only what the changelog says; (4) the deployed size and cold start are within budget; (5) no Lambda Layer is used.
The result is recorded here and the status changed.
