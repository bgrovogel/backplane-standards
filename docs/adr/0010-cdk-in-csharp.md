# 0010. Infrastructure as code in C# with AWS CDK

**Status:** Proposed · **Date:** 2026-11-03 · **Decided by:** to be accepted by the owner

## Context

Infrastructure is code we test and review like any other. Hyvva and the-hive write AWS CDK in C#; Livenhood and buybright write it in TypeScript. The platform's capabilities each ship a construct, and with a C# backend we want services and infrastructure in one language, one build and one test framework.

## Decision

We will write **AWS CDK in C#** (`Amazon.CDK.Lib`), publish constructs as ordinary NuGet packages, test them with CDK assertions in NUnit, and run **cdk-nag** and an IAM policy lint on every synth.

## Alternatives considered

| Option                         | Good                                       | Costs                                                                      | Why not                            |
| ------------------------------ | ------------------------------------------ | -------------------------------------------------------------------------- | ---------------------------------- |
| CDK in TypeScript              | The most examples and community constructs | A second language and test stack beside the backend                        | Splits the team's tooling          |
| Terraform                      | Multi-cloud, a large ecosystem             | A different language (HCL), a separate toolchain, no shared types          | The platform is AWS-only           |
| SAM / CloudFormation templates | Simple for small apps                      | Weak abstraction for constructs; no unit-testing story like CDK assertions | Capabilities need real abstraction |

## Consequences

- **Easier:** one language, shared types between a service and its construct, NUnit for infrastructure tests, the existing Hyvva and the-hive stacks and tests as a starting point.
- **Harder:** fewer public examples are in C#; construct libraries published as NuGet packages are .NET-only (fine, since the apps are .NET).
- **Revisit when:** a needed construct exists only as a TypeScript library with no C# equivalent.
