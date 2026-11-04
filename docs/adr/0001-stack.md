# 0001. The stack: .NET LTS, Angular, serverless on AWS

**Status:** Accepted · **Date:** 2026-11-03 · **Decided by:** the owner

## Context

Five applications were built in three backend and three front-end generations: .NET 8, 9 and 10; Angular 19, 21 and 22; TypeScript backends with React and Next.js. Shared code cannot span that spread, and each app
re-implemented the same plumbing ([strategy](../00-strategy.md#what-we-found-in-the-five-existing-apps)). The owner asked whether to use TypeScript everywhere or TypeScript on the front end with C# on the back end, and decided
on the latter: **the latest .NET LTS for the backend and Angular for the front end**, serverless, keeping the quality standards of `mortgage-analytics` (Roslyn conventions tests, Stryker mutation testing and the rest).

Facts checked on 2026-11-03: .NET 10 is an LTS release (supported to November 2028); AWS Lambda has a managed `dotnet10` runtime since 8 January 2026 ([announcement](https://aws.amazon.com/about-aws/whats-new/2026/01/aws-lambda-dot-net-10/));
Amazon Cognito supports passkey sign-in on its Essentials tier and above ([API reference](https://docs.aws.amazon.com/cognito-user-identity-pools/latest/APIReference/API_WebAuthnConfigurationType.html)).

## Decision

We will build every backend in **C# on the newest .NET LTS** (today .NET 10), every front end in **Angular** (current major, standalone components and signals), and deploy to **AWS serverless** (Lambda, API Gateway, Cognito, SES, SQS, S3 and CloudFront).
When a new .NET LTS ships, the template and kits move first, then the applications.

## Alternatives considered

| Option                              | Good                                                            | Costs                                                                                                                    | Why not                                                       |
| ----------------------------------- | --------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------- |
| TypeScript everywhere               | One language: shared schemas, CDK and Lambdas in one package    | Rebuilds the Roslyn and Stryker.NET tooling; no `decimal`; three of five apps and the biggest codebases are C#           | The investment and the money-correctness favour C#            |
| C# backend, React front end         | React's ecosystem                                               | Three of the five existing front ends are Angular (the other two are React and Next.js); one UI kit serves one framework | Angular has three apps' worth of code and the owner's tooling |
| Containers (ECS/Fargate) not Lambda | No cold starts; one long-running process model like today's app | An always-on cost and more to operate                                                                                    | The owner chose serverless                                    |

## Consequences

- **Easier:** one language per layer, one UI kit, one set of standards and primers, `decimal` for money, the existing conventions tests and Stryker setup carry over.
- **Harder:** Veranda and buybright (TypeScript backends) are either left as they are or rewritten; two ecosystems (NuGet and npm) to publish to; the OpenAPI contract, not a shared language, keeps the two layers in step.
- **To do:** pick the typed-client generator ([roadmap](../11-roadmap.md)); prove native AOT or the managed runtime for cold-start in a spike ([ADR 0006](0006-lambda-granularity.md)).
- **Revisit when:** a requirement needs a runtime .NET cannot serve well on Lambda, or Angular stops being supported for our needs.
