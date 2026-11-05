# AWS CDK in C\#

> The AWS Cloud Development Kit lets us describe cloud infrastructure (functions, queues, tables, permissions, alarms) as ordinary C# classes. Running the program produces CloudFormation templates, and CloudFormation creates the resources.

**Read time:** 12 min · **Version:** `Amazon.CDK.Lib` 2.275.0 on nuget.org (checked 2026-11-05); CDK Toolkit (the `cdk` command) installed with Node.js · **Status:** adopted in [the roadmap](../11-roadmap.md) (Phase 1), no CDK code is written in this repository yet · **Last verified:** 2026-11-05 against the AWS CDK v2 guide

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) · [How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) · [What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**Infrastructure as code** means the servers, queues and permissions an application needs are written down in files that are reviewed, tested and versioned like the application. The **AWS Cloud Development Kit (CDK)** is one way to do that. The AWS guide calls it "an open-source software development framework for defining cloud infrastructure in code and provisioning it through AWS CloudFormation". It has two parts:

- the **Construct Library**, a collection of pre-written classes called **constructs**, each of which stands for something in AWS (a bucket, a function, a queue);
- the **CDK Toolkit**, the `cdk` command line tool that runs your program and deploys the result.

C# is a fully supported language; the guide says it "is considered stable". C# apps need .NET 8.0 or later, and the main NuGet package is `Amazon.CDK.Lib`.

## Why we use it

The [strategy](../00-strategy.md) chose CDK in C# so that infrastructure sits beside the services in one language: the same compiler, the same test framework (NUnit), the same review. A capability such as email is shipped as a construct package that creates everything it needs, and an application adds one line to use it ([capability modules](../08-capability-modules.md)).

Alternatives: writing CloudFormation YAML by hand (verbose, no loops, no types, no tests of the logic that produces it); Terraform (a different language and state model, and the team is already on AWS and C#); clicking in the console (not repeatable, not reviewable). CDK keeps CloudFormation underneath, so a failed deploy rolls back the way CloudFormation always does.

## The big idea

> **Your program builds a tree of constructs. Running it ("synthesizing") turns the tree into CloudFormation templates. Deploying hands those templates to CloudFormation.**

The program itself never creates anything in AWS. That is why it can be tested on a laptop with no AWS account: the test looks at the templates.

## How it works inside

1. **App, stacks, constructs.** An **app** is the root of the tree. Inside it, **stacks** are the unit of deployment (one CloudFormation stack each). Inside stacks are constructs. Every construct is created with three arguments: its **scope** (its parent), an **id** (a name that must be unique inside the parent), and **props** (a bundle of settings).
2. **Three levels of construct.** The AWS guide names them:
   - **L1** constructs (`CfnBucket`) map one to one onto a CloudFormation resource and expose every property; no defaults, no help.
   - **L2** constructs (`Bucket`) are the curated ones: "intent-based" classes with sensible defaults and helper methods (`bucket.GrantRead(role)` writes the policy for you).
   - **L3** constructs, also called **patterns** (`ApplicationLoadBalancedFargateService`), assemble several resources for a whole job.
     Each L2 wraps an L1, so you can reach down when the high-level class lacks an option (an **escape hatch**).
3. **Synthesis.** `cdk synth` runs your program and writes a **cloud assembly** (the `cdk.out` folder by default) holding "a CloudFormation template for each stack" and a copy of any files or Docker images the app refers to. The names in the templates (**logical IDs**) are generated from each construct's path in the tree.
4. **Bootstrapping.** Before the first deploy into an AWS account and region, `cdk bootstrap` deploys a stack named `CDKToolkit` that holds an S3 bucket (for files such as function code), a container registry and IAM roles the toolkit uses. The guide: "Each environment that you want to use with the AWS CDK must first be bootstrapped."
5. **Diff and deploy.** `cdk diff` compares what the code would create with what is deployed. `cdk deploy` uploads the assets and asks CloudFormation to create or update the stacks.

```
   C# program --new App / Stack / constructs--> construct tree
        --cdk synth--> cdk.out/ (a CloudFormation template per stack + assets)
        --cdk diff--> what would change?      --cdk deploy--> CloudFormation changes AWS
```

The library itself is written in TypeScript. C# (and Java, Python, Go) call it through a bridge called **jsii**, which is why the guide lists Node.js among the things to install even for a C# app.

## How it fits with everything else

- **Tests** look at the synthesized templates: [CDK assertions and cdk-nag](cdk-assertions-and-cdk-nag.md) check that each stack has the encryption, retention, permissions and alarms it must.
- **Quality gates** G-35 (assertions), G-36 (cdk-nag) and G-43 (alarms exist) are in [document 3](../03-quality-gates.md#the-gates); the deploy rules are in [Release and deploy](../03-quality-gates.md#release-and-deploy).
- **Versioning** of construct packages comes from git tags with [MinVer](minver.md).
- A new application starts from the template, which carries an `infra/` folder ([document 2](../02-architecture.md)); see [`dotnet new` templates](dotnet-new-templates.md).

## An example from our code

There is no CDK code in this repository yet; infrastructure is written when the first capability is. So this is an example from the AWS guide, not from our code. It creates a bucket with versioning on, and shows the three-argument shape of every construct:

```csharp
var bucket = new Bucket(this, "amzn-s3-demo-bucket", new BucketProps {
    Versioned = true
});
```

- `this` is the scope (the stack), `"amzn-s3-demo-bucket"` is the id, and `BucketProps` is the props type; C# fills it with an object initializer.
- One line becomes an `AWS::S3::Bucket` resource in the template. Setting nothing else gives the L2 defaults.

## How to use it

```text
npm install -g aws-cdk                 # the CDK Toolkit (needs Node.js)
mkdir my-infra && cd my-infra
cdk init app --language csharp         # a project that references Amazon.CDK.Lib
dotnet build src                       # compile and run the tests, as for any C# project
cdk synth                              # write cdk.out/ ; no AWS account is needed for this
cdk diff                               # needs credentials: compares with what is deployed
```

`cdk bootstrap` and `cdk deploy` change an AWS account. In this platform **only the owner deploys**: an agent or a colleague prepares the change, runs `cdk synth` or `cdk diff`, and stops.

## The rules we follow

| Rule                                                                                  | Enforced by                                  |
| ------------------------------------------------------------------------------------- | -------------------------------------------- |
| Infrastructure is C#, in the application's `infra/` folder or a construct package     | Review; the template                         |
| Every stack has assertion tests (G-35) and passes cdk-nag (G-36)                      | The infrastructure tests, in CI              |
| `cdk diff` is read before every deploy, and replacements of data are called out       | [Release and deploy](../03-quality-gates.md) |
| Only the owner deploys; nobody else runs `cdk deploy` or `cdk bootstrap`              | The working rules; credentials               |
| NuGet versions of `Amazon.CDK.Lib` and `Constructs` are set once, in the central list | `Directory.Packages.props`                   |

## What goes wrong

| Symptom                                                  | Cause                                                                                         | Fix                                                                                             |
| -------------------------------------------------------- | --------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `cdk` cannot run the app, or says Node.js is missing     | The toolkit and the jsii bridge need Node.js                                                  | Install Node.js; run `cdk doctor` to see what the tool finds                                    |
| Deploy fails saying the environment is not bootstrapped  | `cdk bootstrap` was never run for that account and region                                     | The owner bootstraps once per environment                                                       |
| `cdk diff` shows a resource replaced after a rename      | Logical IDs come from the construct path, so renaming or moving a construct is a new resource | Keep ids stable; if a rename is needed, read the diff and plan the data move                    |
| A property you need is missing on an L2 construct        | The high-level class does not expose it yet                                                   | Use the escape hatch to the L1 (and write a test for it); raw overrides are not seen by cdk-nag |
| Two packages want different versions of the CDK library  | NuGet pins every dependency to one version                                                    | Use one version of `Amazon.CDK.Lib`; check the construct packages' ranges                       |
| The synth works, the deploy fails halfway and rolls back | CloudFormation rejected a resource (a name in use, a missing permission)                      | Read the failed event in the CloudFormation console; fix the construct; the rollback is clean   |

## Terms

| Term           | Meaning                                                                                       |
| -------------- | --------------------------------------------------------------------------------------------- |
| CloudFormation | The AWS service that creates resources from a JSON or YAML template and rolls back on failure |
| Construct      | A class that stands for part of the infrastructure; the unit of reuse                         |
| Stack          | The set of resources deployed together as one CloudFormation stack                            |
| App            | The root of the construct tree                                                                |
| Synthesis      | Running the program to produce the templates                                                  |
| Cloud assembly | The output folder of synthesis (`cdk.out`)                                                    |
| Bootstrap      | One-time preparation of an account and region for CDK deploys                                 |
| Logical ID     | A resource's name inside a template, generated from its construct path                        |
| Escape hatch   | Reaching from a high-level construct down to its L1 to set what it does not expose            |
| jsii           | The bridge that lets C#, Java, Python and Go call the CDK library written in TypeScript       |

## Further reading

- AWS CDK v2 Developer Guide: [What is the AWS CDK?](https://docs.aws.amazon.com/cdk/v2/guide/home.html), [Constructs](https://docs.aws.amazon.com/cdk/v2/guide/constructs.html), [Bootstrapping](https://docs.aws.amazon.com/cdk/v2/guide/bootstrapping.html), [Working with the AWS CDK in C#](https://docs.aws.amazon.com/cdk/v2/guide/work-with-cdk-csharp.html), [CDK CLI command reference](https://docs.aws.amazon.com/cdk/v2/guide/ref-cli-cmd.html).
- The package: [`Amazon.CDK.Lib` on nuget.org](https://www.nuget.org/packages/Amazon.CDK.Lib).
- [CDK Workshop](https://cdkworkshop.com/) for a hands-on first stack.
