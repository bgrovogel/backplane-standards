# OpenAPI and the contract snapshot

> OpenAPI is a standard way to describe an HTTP API in a file. We generate that file from the code and compare it with an approved copy, so the public contract can never change by accident.

**Read time:** 12 min · **Version:** Swashbuckle.AspNetCore 10.3.1 (`Directory.Packages.props`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**OpenAPI** (formerly Swagger) is a standard JSON or YAML format that lists an API's paths, what each accepts and returns, the shape of every request and response, enums, required fields and errors. **Swashbuckle** is the library that
produces this document from our ASP.NET Core code, and serves it at `/swagger/v1/swagger.json` with a browsable page.

A **contract snapshot test** (also called an **approval test**) stores the generated document in the repository as an **approved** file; each test run generates a fresh document, compares it with the approved one, and fails on any difference.

## Why we use it

The JSON an API sends is a **promise to every client**: the browser app, other capabilities, partners. Renaming a field or making one required breaks them silently, often only discovered in the browser. In `mortgage-analytics` the TypeScript models mirror the contract by hand, so
without this test a renamed field would first show up in a failing browser test, or in front of a person. With it, the change fails in seconds, the diff says exactly what changed, and the developer must approve it knowingly.

In the platform the same document is the **source** of each capability's typed client, so contract and client cannot drift ([Capability modules](../08-capability-modules.md#the-contract)).

## The big idea

> **The contract is a file in the repository. A change to it is a deliberate, reviewed edit, never a side effect.**

## How it works inside

1. **Generation.** Swashbuckle inspects the registered endpoints (routes, parameter and return types from `TypedResults`/metadata) and the C# types they use, and builds a document object: `paths`, `components.schemas`, and so on. It also reads our **XML doc comments** (that is part of
   why they are required) and uses them as descriptions.
2. **Serving.** The document is available at `/swagger/v1/swagger.json`. In the test it is fetched through [WebApplicationFactory](webapplicationfactory.md), so no server is needed.
3. **Normalizing.** `ContractOf` parses the JSON, **removes prose** (`description`, `summary`, `example`, except where a key is a property _named_ "description"), re-serializes it indented with `\n` line endings, so only changes of **shape** differ, not wording or line endings.
4. **Comparing.** The result is compared with `Snapshots/openapi.v1.approved.json`. If equal, any leftover "received" file is deleted. If different, the new document is written to a `.received.json` file beside the approved one and the assertion fails with the paths of both.
5. **Approving.** The developer diffs the two files (`git diff --no-index`, or the IDE), checks the change is intended, updates dependent code (the TypeScript models; in the platform, regenerates the client), and **copies the received file over the approved one**, committing it with the change.
6. **Compatibility.** In the platform an extra step classifies the diff: additions only is a _minor_ version; a removed or renamed field or a new required input is _major_ ([versioning](../08-capability-modules.md#versioning)). A tool compares the two OpenAPI documents to decide.

```
  code ---Swashbuckle---> generated document --normalize--> received.json
                                                              |  equal?   -> pass
  approved.json (in git) --------------------------------------+  different -> FAIL, received.json written beside it
                                                                     developer reviews the diff, then copies received over approved
```

## How it fits with everything else

- It runs as an **NUnit integration test** ([primer](webapplicationfactory.md)), gate **G-23**, in tier 2.
- The **limits** enforced by FluentValidation (`minimum`, `maximum`, `maxLength`) can appear in the schema, so a limit change shows in the diff ([FluentValidation](fluentvalidation.md), [one source for each limit](../05-validation-and-errors.md#one-source-for-each-limit)).
- **Error responses** are RFC 9457 problem details, described once.
- The **frontend client** is generated from it in the platform; today it is mirrored by hand.
- In the platform, **semantic versioning** of a capability is decided by this diff.

## An example from our code

`tests/MortgageAnalytics.Api.IntegrationTests/ContractSnapshotTests.cs`:

```csharp
[Test]
public async Task OpenApiDocument_MatchesTheApprovedContract()
{
    string received = ContractOf(await Client.GetStringAsync("/swagger/v1/swagger.json"));
    string approved = File.Exists(ApprovedPath) ? File.ReadAllText(ApprovedPath).ReplaceLineEndings("\n") : "";

    if (received == approved) { File.Delete(ReceivedPath); }
    else { File.WriteAllText(ReceivedPath, received); }

    Assert.That(received, Is.EqualTo(approved),
        $"The API contract changed. Compare {Path.GetFileName(ReceivedPath)} with {Path.GetFileName(ApprovedPath)} in {SnapshotFolder}. "
        + "If the change is intended, update the TypeScript models in client/src/app/core/models to match, then copy the received file over the approved one.");
}
```

- The test fetches the live document, normalizes it, and compares it with the approved file.
- The failure message tells the developer **exactly what to do**, which is the standard for every test of this kind.
- The approved file is `tests/MortgageAnalytics.Api.IntegrationTests/Snapshots/openapi.v1.approved.json`. It was re-approved on purpose, for example, when passkey registration gained an optional `{password}` body on 30 October 2026.

## How to use it

When the test fails after a deliberate change:

1. Open the two files side by side and read the diff.
2. Confirm the change is what you meant (an added optional field, not an accidentally renamed one).
3. Update the client models (or regenerate the client).
4. `cp received approved`, run the tests, commit the code, the models and the approved file **together**.

When it fails and you did _not_ change the API: something changed the shape indirectly (a renamed class, a changed type). Find it; do not approve it.

## The rules we follow

| Rule                                                                        | Enforced by                    |
| --------------------------------------------------------------------------- | ------------------------------ |
| The approved contract is committed and compared on every test run           | `ContractSnapshotTests` (G-23) |
| A contract change is committed together with the client change              | Review                         |
| Prose is stripped before comparing, so a wording fix does not need approval | `ContractOf`                   |
| Public types have XML summaries (they become the descriptions)              | Compiler (CS1591)              |
| A breaking change needs a major version and a migration guide               | Versioning rules (platform)    |

## What goes wrong

| Symptom                                                | Cause                                          | Fix                                                     |
| ------------------------------------------------------ | ---------------------------------------------- | ------------------------------------------------------- |
| The test fails on every machine after a package update | The library changed how it writes the document | Review the diff; if it is only formatting, approve once |
| The test passes locally and fails in CI                | Line endings or ordering                       | The normalizer uses `\n`; check sort order              |
| An accidental breaking change gets approved            | Nobody read the diff                           | A reviewer reads the approved file in the pull request  |
| The client and the contract disagree                   | The client is hand-written                     | Generate it                                             |
| The document includes types it should not              | A domain type leaked into a contract           | `ContractLayeringTests`                                 |

## Terms

| Term                    | Meaning                                                                           |
| ----------------------- | --------------------------------------------------------------------------------- |
| **OpenAPI**             | A standard description format for HTTP APIs                                       |
| **Swagger**             | The older name, and the browsable UI                                              |
| **Schema**              | The description of the shape of an object in the document                         |
| **Approval test**       | A test comparing output with an approved stored copy                              |
| **Breaking change**     | A change that makes existing clients stop working                                 |
| **Semantic versioning** | Version numbers whose parts say whether a change is a fix, an addition or a break |

## Further reading

- [OpenAPI Specification](https://spec.openapis.org/oas/latest.html)
- [Swashbuckle.AspNetCore](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- [Approval testing](https://approvaltests.com/)
