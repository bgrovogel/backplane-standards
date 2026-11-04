# Architecture decision records

A short file per decision that is hard to reverse or that a reasonable person might question: what we decided, why, what else we considered, and what follows. Written so a newcomer, or the
owner in a year, learns **why** the code is shaped as it is. The rules for writing one are in the [documentation standard](../10-documentation-standard.md#decisions-adrs); the template is [0000](0000-template.md).

An accepted ADR is not edited; a later ADR **supersedes** it.

| #                                             | Decision                                                                                                                   | Status                                      | Date       |
| --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------- | ---------- |
| [0001](0001-stack.md)                         | The stack: .NET LTS, Angular, serverless on AWS                                                                            | Accepted (owner)                            | 2026-11-03 |
| [0002](0002-capability-modules.md)            | Capability modules are the unit of reuse                                                                                   | Proposed                                    | 2026-11-03 |
| [0003](0003-copy-per-app.md)                  | One copy per application by default                                                                                        | Proposed                                    | 2026-11-03 |
| [0004](0004-repositories.md)                  | Few repositories, monorepos for kits, a local-then-private feed                                                            | Proposed; owner to decide public or private | 2026-11-03 |
| [0005](0005-data-store.md)                    | PostgreSQL through EF Core by default; DynamoDB for small state                                                            | Proposed; owner to confirm                  | 2026-11-03 |
| [0006](0006-lambda-granularity.md)            | One Lambda function per feature area, each a Minimal API                                                                   | Proposed                                    | 2026-11-03 |
| [0007](0007-money.md)                         | Money is `decimal` in code, minor units only at the processor                                                              | Proposed                                    | 2026-11-03 |
| [0008](0008-standards-from-reference.md)      | The standards of `mortgage-analytics` are the baseline                                                                     | Accepted (owner)                            | 2026-11-03 |
| [0009](0009-capability-packaging.md)          | How a capability's service code is packaged                                                                                | Open (a spike decides)                      | 2026-11-03 |
| [0010](0010-cdk-in-csharp.md)                 | Infrastructure as code in C# with AWS CDK                                                                                  | Proposed                                    | 2026-11-03 |
| [0011](0011-mortgage-first.md)                | The standards are applied to `mortgage-analytics` first; no rewrites yet                                                   | Accepted (owner)                            | 2026-11-03 |
| [0012](0012-build-rules-as-an-msbuild-sdk.md) | The build rules reach an application as an MSBuild SDK package (a props file in a package cannot set the target framework) | Accepted for local use; the feed is open    | 2026-11-04 |
