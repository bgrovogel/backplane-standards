# 5. Validation and errors

> Everything we accept is checked, everything we refuse is explained safely, and every rule is proved by a test that fails if
> the rule is removed.

**Read time:** 20 min · **Best for:** adding a field, an endpoint or a failure path · **Builds on:**
[Principles](01-principles.md#fail-fast-and-parse-at-the-edge), [Architecture](02-architecture.md) ·
**Reference implementation:** `src/MortgageAnalytics.Api/Filters/ValidationFilter.cs`,
`src/MortgageAnalytics.Api/Errors/GlobalExceptionHandler.cs`

**On this page:** [What "validated" means](#what-validated-means) · [The layers](#the-layers) · [The rules of validation](#the-rules-of-validation) ·
[The error contract](#the-error-contract) · [Results and exceptions](#results-and-exceptions) · [One source for each limit](#one-source-for-each-limit) ·
[Proving a rule is real](#proving-a-rule-is-real) · [The frontend](#the-frontend) · [Validating the system itself](#validating-the-system-itself) ·
[Checklist for a new field](#checklist-for-a-new-field)

---

## What "validated" means

"We can validate everything" means three different things, and we do all three:

1. **Input is validated.** Nothing enters the system unchecked: requests, forms, queue messages, webhooks, uploaded files, configuration.
2. **Rules are validated.** Each rule is proved by a test that fails if the rule is deleted or weakened (boundaries, messages, mutation testing).
3. **The system is validated.** Its contract, its configuration, its infrastructure and its documentation are checked against what
   they claim to be, by machines, on every change.

---

## The layers

Validation is layered because each layer catches a different kind of mistake, and no layer trusts the one before it.

| Layer                      | Checks                                                                                 | Failure becomes                                                                                  | Tool                                                                   |
| -------------------------- | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------- |
| **Browser form**           | Helps the person: format, required, range, as they type                                | An inline message                                                                                | Angular forms; **never trusted**                                       |
| **API boundary**           | Every field of every request: presence, type, range, length, format, cross-field rules | **400** with field-by-field errors                                                               | FluentValidation, one validator per request, run by an endpoint filter |
| **Authorization**          | Who is asking, for which tenant, may they do this                                      | 401, 403 or (for another tenant's data) 404                                                      | Policies; tenant from the token                                        |
| **Domain**                 | Values and objects refuse to be invalid (a negative term cannot be built)              | A `DomainValidationException`, which means **a bug**, because the boundary should have caught it | Constructors and guard clauses                                         |
| **Persistence**            | Last line: NOT NULL, foreign keys, unique indexes, check constraints                   | A constraint error, mapped to 409 if expected                                                    | The database                                                           |
| **Configuration**          | Every option present and sensible, at startup                                          | The application refuses to start                                                                 | Options validation on start                                            |
| **Inbound from a partner** | Webhook signature, timestamp, replay, schema                                           | 400 or 401, never processed                                                                      | Signature check, idempotency store                                     |
| **Files**                  | Size, declared and actual type, name, scan                                             | 400 or 413                                                                                       | Checked before storage                                                 |

Two consequences:

- A domain guard firing is **a defect**, not a user error. The global handler turns it into a generic 500 and logs it, because
  its message is internal and the input should never have reached it.
- The browser check exists for the person's comfort. The server check exists for correctness and security. Both exist, and the
  server's wins.

---

## The rules of validation

1. **Validate at the boundary, once, completely.** The validator for a request checks _every_ field; the use case afterwards may assume it is valid.
2. **One validator per request type**, in `Application/Validation/`, one file each (the one-type-per-file rule). A request type without
   a validator fails an architecture test (Hyvva's `ValidatorTestPresenceRatchetTests` does this).
3. **An endpoint filter runs the validator**, so no endpoint can forget. One filter serves every endpoint (DRY).
4. **Allow-list, not block-list.** Define what is valid and reject the rest. Unknown JSON fields are rejected or ignored
   deliberately, never silently bound.
5. **Bound everything:** string length, collection size, number range, request body size (256 KB in `mortgage-analytics`), nesting
   depth, request rate. An unbounded input is a denial-of-service.
6. **Normalize, then validate, then store:** trim, apply Unicode normalization where it matters, lower-case an email for comparison. Store what you validated.
7. **Messages are safe and useful:** say what is wrong and how to fix it ("Rate must be between 0% and 30%"); never echo a secret, a
   token, a stack trace, internal names or another person's data.
8. **A message has a stable code** (`rate.out_of_range`) beside its English text, so the client can translate it and a test can
   assert on it without matching prose (`mortgage-analytics` does this since 2026-11-04: a refused request has `errors`, the English sentences, and `problems`, each complaint as `{ field, code, message, args }`, additive so no client breaks. A rule with a sentence of its own names its code and a plain-string template; the numbers it names travel unformatted; the kinds of rule that say nothing of their own share one code. A convention test, an approved list of every code and a client spec that holds the translations to it make a code with no translation fail the build).
9. **Cross-field rules** (end after start, down payment below price) are validator rules, and each names the field it belongs to.
10. **Money and dates use their own types** (`Money`, `DateOnly`), never strings or floating point, from the boundary inward.

---

## The error contract

Every error the API returns has one shape: **RFC 9457 Problem Details** (the standard `application/problem+json` document).

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "extraPayments.lumpSums[0].amount": ["Amount must be zero or more."] },
  "requestId": "9c1d7e2a4b0f4d3e8a52c6f1b7d90e11"
}
```

- **`errors`** keys are JSON paths matching the request body, in the body's own casing, so a form can attach each message to its field.
- **`requestId`** is on every error, equal to the `X-Request-ID` response header, so a person can quote it and we can find the log
  lines ([document 6](06-logging-observability.md)).
- **No internal detail.** A 500 says "An unexpected error occurred", nothing more; the cause is in the log.
- **Security headers are set on error responses too.** (An audit of `mortgage-analytics` found they were missing there; fixed in `abca34f`.)

Status codes, fixed for every application:

| Status | Meaning                                             | Notes                                                                        |
| ------ | --------------------------------------------------- | ---------------------------------------------------------------------------- |
| 400    | The request is malformed or invalid                 | With `errors` for validation                                                 |
| 401    | Not signed in, or the session ended                 | The client sends the person to sign in                                       |
| 403    | Signed in, not allowed                              | Same-tenant, wrong role                                                      |
| 404    | Not found **or not yours**                          | Cross-tenant access returns 404, never 403, so existence is not confirmed    |
| 409    | Conflict: duplicate, stale version, a limit reached | The 500-saved-calculations cap is a 409                                      |
| 413    | Body too large                                      | Checked from `Content-Length` before the body is read                        |
| 429    | Rate limited                                        | With `Retry-After`                                                           |
| 500    | Our fault                                           | Generic body; logged with the request id                                     |
| 503    | Overloaded or a dependency is down                  | With `Retry-After`; the work-at-once gate returns this after its short queue |

---

## Results and exceptions

Two tools, two jobs.

| Situation                                                   | Use                                                                        | Example                                                                   |
| ----------------------------------------------------------- | -------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| A **normal, expected** failure the caller can act on        | A `Result<T>` returned by the use case, with a typed error                 | "Saved calculation not found", "name already taken", "plan limit reached" |
| A **bug or an impossible state**                            | An exception                                                               | A domain guard fires; a null where the type says never                    |
| An **infrastructure failure** (timeout, 5xx from a partner) | An exception from the adapter, caught at the boundary, retried or reported | The mail server is down                                                   |

The endpoint maps a `Result` to a status code in **one shared function**, so no endpoint invents its own mapping (Hyvva's
`ResultMappingRatchetTests` fails when one does). Exceptions are not used for flow control, and an exception is logged **once**, at the
boundary that handles it, with context. Never log-and-rethrow at every layer.

(Using `Result<T>` as a standard is **proposed**; `mortgage-analytics` mostly uses exceptions and early returns today.)

---

## One source for each limit

Every limit ("rate at most 30%") is a business fact and has **one home**, because in `mortgage-analytics` the 30% limit is written
twice (a known weak spot in the owner's notes), which is the duplication [DRY](01-principles.md#dry) warns about.

The design:

1. The **Domain owns the limit** as a named constant (it is a business rule: `LoanLimits.MaxAnnualRate`).
2. The **validator references that constant**, never a literal.
3. The **OpenAPI schema carries it** (`minimum`, `maximum`, `maxLength`), generated from the validator, so the contract snapshot test (G-23)
   shows any change to a limit.
4. The **frontend reads the limit** from the generated types or a small limits endpoint, so a form hint cannot disagree with the server.
5. **One test pins each limit** by its value and message (the `GuardLimitTests` pattern: one row per guard).

---

## Proving a rule is real

A rule that nothing checks might not exist. For each rule, at least:

| Proof                                 | How                                                                                                |
| ------------------------------------- | -------------------------------------------------------------------------------------------------- |
| **Boundary tests**                    | The value just below, at, and just above each limit, asserting accept or reject and the message    |
| **Every message is pinned**           | A failing assertion names the rule; changing the text is a deliberate edit                         |
| **Mutation testing**                  | If changing `<=` to `<` in the validator survives, the boundary test is missing                    |
| **A validator exists and has a test** | A scan test fails otherwise                                                                        |
| **The contract shows it**             | The approved OpenAPI file changes when a limit changes                                             |
| **Property tests (optional)**         | For parsers and money: any generated valid input is accepted, any generated invalid input rejected |

---

## The frontend

- **Forms use the same limits** as the server (generated), and show **errors next to the field**, connected with `aria-describedby`,
  with an error summary that receives focus on submit. A message is not only colour.
- **Never block the server check** because the form "already validated".
- **A server `errors` map is shown** against the matching fields; a field the form does not have is shown in a summary.
- **A `requestId` is shown** in an unexpected-error message with a way to copy it.
- **Network failure, timeout and 5xx have their own message and a retry** (the stores retry with a delay; see `latest-request.ts` in the reference app).
- **Every message is translatable**, and a test fails when a language lacks one.

---

## Validating the system itself

| What                    | How                                                                                                                                                                                                                                                             |
| ----------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Configuration**       | Options classes with data-annotation or `IValidateOptions` checks and `ValidateOnStart()`; a missing required value stops startup with a message naming it. `mortgage-analytics` refuses to start without required settings, and warns when email is not set up |
| **The contract**        | The approved OpenAPI snapshot (G-23); the generated client builds against it (G-16)                                                                                                                                                                             |
| **The database schema** | The EF Core model matches the migrations (`HasPendingModelChanges`); migrations apply to an empty database and upgrade the previous release                                                                                                                     |
| **Stored data formats** | Saved JSON of today's shape still loads (stored-format tests)                                                                                                                                                                                                   |
| **Infrastructure**      | CDK assertions and cdk-nag on every stack                                                                                                                                                                                                                       |
| **The documentation**   | The docs check compares counts, links and file-and-line citations with the code                                                                                                                                                                                 |
| **Health**              | `/health` reports readiness (the database answers); a canary exercises a real request after a deploy                                                                                                                                                            |

---

## Checklist for a new field

Copy this into the change description and tick it.

- [ ] A failing validator test exists for each rule: required, type, range or length, format, cross-field.
- [ ] The limit is a named constant in the Domain, used by the validator, and appears in the OpenAPI schema.
- [ ] The domain type refuses an invalid value too.
- [ ] The error message is specific and safe, and has a code and a translation.
- [ ] The form shows the same limit and the error beside the field, with an accessible name.
- [ ] The database column has the right type, length and constraints, and a migration with a test.
- [ ] The contract snapshot was re-approved on purpose, and the client regenerated.
- [ ] Nothing about the field is logged if it is personal data ([document 6](06-logging-observability.md)).
- [ ] The mutation run on the validator has no surviving mutant for this field.
