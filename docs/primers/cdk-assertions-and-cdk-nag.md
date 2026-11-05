# CDK assertions and cdk-nag

> Two ways to test infrastructure without deploying it. **CDK assertions** check that a stack's CloudFormation template contains what we require. **cdk-nag** checks it against a published rule pack and reports what looks unsafe.

**Read time:** 12 min · **Version:** `Amazon.CDK.Lib` 2.275.0 (assertions are inside it, in `Amazon.CDK.Assertions`); `Cdklabs.CdkNag` 3.0.2, which needs `Amazon.CDK.Lib` 2.257.0 or later (nuget.org, checked 2026-11-05) · **Status:** adopted in [the roadmap](../11-roadmap.md) (Phase 1), no infrastructure tests are written in this repository yet · **Last verified:** 2026-11-05 against the AWS CDK API docs and the cdk-nag README

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) · [How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) · [What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

An infrastructure program ([AWS CDK in C#](aws-cdk-in-csharp.md)) cannot be tested by running it against AWS: that is slow, costs money and changes real accounts. But running it with `synth` produces CloudFormation templates, and a template is just data. So we test the data.

- **CDK assertions** is a module of the CDK library. Its `Template` class reads a stack's template and answers questions: how many functions are there, does this bucket have encryption, does this policy contain a wildcard.
- **cdk-nag** ("nag" as in nagging) is a separate package from the CDK Labs project. It "checks CDK applications, and CloudFormation templates, for best practices using rule packs", and was "inspired by cfn_nag". The packs it ships are AWS Solutions, HIPAA Security, NIST 800-53 (revisions 4 and 5), PCI DSS 3.2.1 and Serverless.

## Why we use it

The platform's gates name them: G-35 says each stack has the resources, encryption, retention and permissions it must; G-36 says no wildcard IAM actions, no public buckets, encryption on; G-43 says error-rate, latency and queue-age alarms exist for each deployed capability ([document 3](../03-quality-gates.md#the-gates)). Without tests, those would be promises in a document.

The two tools answer different questions. Assertions check **what we decided** (our retention is 30 days; this alarm exists). cdk-nag checks **what the industry has learned goes wrong** (a bucket without access logs, a role with `s3:*`), including things nobody thought to write a test for. Alternatives: reading `cdk diff` output by eye (still required, but a person misses things at 5 pm on a Friday); a cloud scanner after deploy (finds problems after they exist).

## The big idea

> **Synthesize the stack in memory, then ask questions of the template. Nothing is deployed, so the test runs in a second and needs no AWS account.**

## How it works inside

**Assertions.**

1. The test builds an `App` and the `Stack` under test, as the real program does.
2. `Template.FromStack(stack)` synthesizes it and keeps the template. (`Template.FromString` does the same for a template you already have as text.)
3. Questions are asked of the template. `ResourceCountIs("AWS::Lambda::Function", 2)` counts resources of a type. `HasResourceProperties(type, expected)` passes when some resource of that type has those properties. By default the match is a "deep partial" one: the resource may have more properties than the ones you list. `Match` helpers change that: `Match.ObjectLike` (partial), `Match.ObjectEquals` (exact), `Match.Absent` (the property must not exist), `Match.AnyValue`, `Match.ArrayWith` (a subset, in order), `Match.StringLikeRegexp`, `Match.Not`.
4. A `Capture` pulls a value out of a match so a later assertion can use it, for example the id of a bucket to check its policy.
5. `Annotations.FromStack(stack)` reads the errors and warnings that the code or an aspect attached to constructs (`HasError`, `HasWarning`).

**cdk-nag.** In version 3 it is a **plugin** to the CDK's own validation system. The README applies it with `Validations.of(app).addPlugins(new AwsSolutionsChecks(app))` (TypeScript). When the app is synthesized the pack walks every resource and checks its rules; a rule that fails is reported as an error with an id such as `AwsSolutions-IAM5`, and the report is written to the cloud assembly as `policy-validation-report.json`. Several packs can run on one app, and `verbose: true` adds an explanation of each triggered rule.

A finding can be **acknowledged** (what version 2 called a suppression) on a construct, a stack or an imported resource, with a written reason: `Validations.of(construct).acknowledge({ id, reason })`. In version 3 each finding is acknowledged separately; the README gives `AwsSolutions-IAM5[Action::s3:*]` and `AwsSolutions-IAM5[Resource::*]` as two. Acknowledging by prefix, to cover all findings of a rule at once, is "not yet supported".

## How it fits with everything else

- The tests are NUnit tests in the construct's own test project (`Brand.Email.Cdk.Tests` in the layout of [document 8](../08-capability-modules.md)), so they run in `check full` and in CI like any other.
- They guard the [security baseline](../07-security-baseline.md): encryption in transit and at rest, no wildcard actions, a reason for every acknowledged finding.
- Logging retention and alarms ([document 6](../06-logging-observability.md)) are asserted here, because a log group that is never expired costs money and keeps data we promised to delete.
- `cdk diff` is still read before every deploy ([Release and deploy](../03-quality-gates.md#release-and-deploy)); these tests make the diff shorter to read, not unnecessary.

## An example from our code

There is no infrastructure code in this repository yet, so there is no test of ours to show. This is a **sketch**, written from the library documentation and the C# naming of the module (`Amazon.CDK.Assertions`); it has not been compiled, and the first real stack must confirm it. It shows the three kinds of check the gates ask for:

```csharp
var template = Template.FromStack(stack);

// G-35: the stack has what it must.
template.ResourceCountIs("AWS::Lambda::Function", 1);
template.HasResourceProperties("AWS::Logs::LogGroup", new Dictionary<string, object>
{
    ["RetentionInDays"] = 30,
});

// G-43: an alarm exists for the function's errors.
template.ResourceCountIs("AWS::CloudWatch::Alarm", 1);
```

And for G-36, in the stack's test project, the app is synthesized with `new AwsSolutionsChecks(app)` added as a plugin and the test fails on any unacknowledged finding.

- The first block reads like the requirement it comes from; each line names a gate.
- A test that cannot fail is worth nothing, so the first version of each is written before the code it checks and seen to fail.

## How to use it

- Reference `Amazon.CDK.Lib` (assertions are in it) and `Cdklabs.CdkNag` from the construct's test project; the versions go in `Directory.Packages.props`.
- Write the assertion first, run it and see it fail, then change the stack until it passes. Use `Match.ObjectLike` unless the property must be exact.
- For a cdk-nag finding: **fix it** if you can. Acknowledge it only when the finding is wrong for this case, and write the reason and who owns the exception (document 7, "cdk-nag and policy lint run on every synth").
- In a Jest test run, the assertions module can leave temporary `cdk.out*` folders behind; the CDK docs give a clean-up helper. NUnit has no such issue, but delete stray `cdk.out*` folders if a test run is killed.

## The rules we follow

| Rule                                                                                         | Enforced by                     |
| -------------------------------------------------------------------------------------------- | ------------------------------- |
| Every stack has assertion tests for its encryption, retention, permissions and alarms        | G-35, G-43; review              |
| cdk-nag runs on every synth with at least the AWS Solutions pack, and its findings are fixed | G-36; the stack's test          |
| An acknowledged finding has a written reason and an owner                                    | Review; the acknowledgment call |
| A fix made by a raw override is acknowledged too, because cdk-nag does not see it            | Review                          |
| Assertions use partial matches unless exactness is the requirement                           | Review                          |

## What goes wrong

| Symptom                                                 | Cause                                                                                       | Fix                                                                               |
| ------------------------------------------------------- | ------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| An assertion passes although the property is wrong      | Partial matching: the resource has other properties and yours matched something else        | Use `Match.ObjectEquals` where exactness matters; assert `ResourceCountIs` too    |
| An assertion fails on a property you did set            | The template holds a CloudFormation reference or function (`Ref`, `Fn::GetAtt`), not a text | Assert the reference shape, or capture the id of the resource                     |
| cdk-nag stays silent about a fix you made               | The fix was a raw override, applied after synthesis                                         | Remediate, then acknowledge the rule (README, "Rules and Property Overrides")     |
| A finding id from version 2 no longer suppresses        | Version 3 needs each finding acknowledged on its own id, with the bracketed detail          | Read the finding text; acknowledge `Rule[Detail]`                                 |
| `Validations` or `addPlugins` is not found              | `Amazon.CDK.Lib` older than the version cdk-nag 3 needs (2.257.0)                           | Upgrade `Amazon.CDK.Lib`                                                          |
| Every run reports hundreds of findings on a new project | An old, un-nagged stack met a strict pack                                                   | Fix by theme (encryption, logging, IAM) and acknowledge the few that do not apply |

## Terms

| Term           | Meaning                                                                                        |
| -------------- | ---------------------------------------------------------------------------------------------- |
| Template       | The CloudFormation JSON a stack synthesizes to                                                 |
| Matcher        | An object (`Match.ObjectLike`, `Match.Absent`) that says how to compare, not just what to      |
| Capture        | A value taken out of a matched resource for use in a later assertion                           |
| Rule pack      | A named set of rules (AWS Solutions, HIPAA, NIST, PCI DSS, Serverless)                         |
| Aspect         | CDK code that visits every construct in the tree; how checks and annotations are attached      |
| Acknowledgment | A finding marked as understood and accepted, with a reason (version 2 called it a suppression) |
| Raw override   | Editing the generated CloudFormation of a construct directly, after the high-level API         |

## Further reading

- [`aws-cdk-lib.assertions` module](https://docs.aws.amazon.com/cdk/api/v2/docs/aws-cdk-lib.assertions-readme.html) in the CDK API reference (the examples are TypeScript; the .NET namespace is `Amazon.CDK.Assertions`).
- [cdk-nag on GitHub](https://github.com/cdklabs/cdk-nag): usage, acknowledging a rule, migrating from version 2; [the rules of each pack](https://github.com/cdklabs/cdk-nag/blob/main/RULES.md); [`Cdklabs.CdkNag` on nuget.org](https://www.nuget.org/packages/Cdklabs.CdkNag).
- AWS DevOps blog: [Manage application security and compliance with the AWS Cloud Development Kit and cdk-nag](https://aws.amazon.com/blogs/devops/manage-application-security-and-compliance-with-the-aws-cloud-development-kit-and-cdk-nag/).
