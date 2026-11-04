# 7. Security baseline

> The rules every application and every capability follows, written down once, each with the thing that checks it. Most of them
> exist because one of our five apps learned them the hard way.

**Read time:** 20 min · **Best for:** designing or reviewing anything that touches identity, data, money, email or infrastructure ·
**Builds on:** [Principles](01-principles.md#secure-and-observable-by-default), [Validation and errors](05-validation-and-errors.md) ·
**Not legal advice:** the privacy policy and terms of each product still need a lawyer's reading.

**On this page:** [Threat thinking](#threat-thinking) · [Identity](#identity) · [Authorization](#authorization) · [Multi-tenancy](#multi-tenancy) ·
[Requests and responses](#requests-and-responses) · [Secrets](#secrets) · [Data protection and privacy](#data-protection-and-privacy) ·
[Payments](#payments) · [Email](#email) · [Webhooks and partners](#webhooks-and-partners) · [Infrastructure](#infrastructure) ·
[Supply chain](#supply-chain) · [Lessons we already paid for](#lessons-we-already-paid-for) · [Verifying security](#verifying-security) ·
[Incidents](#incidents) · [Review checklist for a capability](#review-checklist-for-a-capability)

---

## Threat thinking

Security is not a list of features; it is asking, for each piece of the system, **what could a malicious or careless person do here?**
We use **STRIDE**, a checklist of six kinds of threat:

| Letter | Threat                 | Plain question                                   | Typical defence                             |
| ------ | ---------------------- | ------------------------------------------------ | ------------------------------------------- |
| **S**  | Spoofing               | Can someone pretend to be someone else?          | Strong authentication, signed tokens        |
| **T**  | Tampering              | Can someone change data or code they should not? | Authorization, integrity checks, signatures |
| **R**  | Repudiation            | Can someone deny doing something?                | The audit trail                             |
| **I**  | Information disclosure | Can someone read what they should not?           | Authorization, encryption, no PII in logs   |
| **D**  | Denial of service      | Can someone make it unavailable or expensive?    | Limits, quotas, queues, budgets             |
| **E**  | Elevation of privilege | Can someone gain rights they were not given?     | Least privilege, deny by default            |

**Every capability has a short threat model** (one page, STRIDE per entry point) written _before_ it is built and reviewed before it
is released. The-hive's `Platform_STRIDE_Threat_Model_Booking.md` and `..._Static.md` are the format to follow. A capability without one is not done.

---

## Identity

1. **Do not write authentication from scratch for a new app.** Use **Amazon Cognito** through the login capability. Cognito supports
   passkey (WebAuthn) sign-in on its Essentials tier and above, so the passkey work done in `mortgage-analytics` becomes configuration
   in new apps. (Checked 2026-11-03 in the [API reference](https://docs.aws.amazon.com/cognito-user-identity-pools/latest/APIReference/API_WebAuthnConfigurationType.html);
   confirm current behaviour in a real pool before relying on passkeys combined with MFA, because a community post disagreed with the reference.)
2. **Separate pools for separate populations** where the risk differs: staff, tenant administrators and end customers (the-hive's three-pool design).
3. **MFA is mandatory for staff and administrators**, optional and encouraged for everyone else.
4. **Sessions:** short-lived tokens, refresh with rotation, a way for a person to **see and end their sessions** (built in `mortgage-analytics`), and a
   **server-side kill** (security stamp) so a password change ends other sessions.
5. **Passwords:** length over complexity, check against known-breached passwords, never limit length below 64, never email a password.
   Changing the password, adding a passkey or deleting the account **asks for the current password again** and a wrong guess counts toward lockout.
6. **Account enumeration is closed:** sign-up for an existing address answers the same as a new one (and emails the owner); a wrong password and an
   unknown user look identical, in the message and in the response time as far as practical.
7. **Lockout and rate limits** on every credential endpoint, keyed per account **and** per client address (IPv6 by /64, not the single address).
8. **Account recovery is the weakest link:** one-time codes are short-lived, single-use and never logged; the recovery email says what was requested and how to report it.

---

## Authorization

1. **Deny by default.** Every endpoint requires authorization unless it carries an explicit "public" marker. A test enumerates the endpoints and fails
   on an unmarked, unprotected one (Hyvva's admin-gate tests are the model).
2. **Authorize on the server, on every request.** Hiding a button is not security.
3. **Roles and permissions are named policies** in one place (`Api/Security/`), referenced by constant, never `if (user.Role == "admin")` in an endpoint.
4. **Check ownership, not only role.** "Can edit saved calculations" is not "can edit _this_ calculation"; the query filters by the owner,
   so another person's id simply finds nothing.
5. **Return 404, not 403, for another tenant's or owner's data**, so existence is not confirmed.
6. **Platform staff actions are audited**, narrow, and separate from tenant administration (Hyvva's staff-provisioning boundary).

---

## Multi-tenancy

A tenant is one customer's isolated world. A cross-tenant leak is the worst bug a multi-tenant product can have, so the defence is layered.

1. **The tenant id comes from the verified token, never from the body, the path or a header the caller controls.** (Both Livenhood and Hyvva list this
   as their first absolute rule.)
2. **Every entity except the tenant itself has a `TenantId`**, and every query is filtered by it through a single mechanism (a global query filter in EF Core).
3. **Database row-level security (RLS) as a second wall.** PostgreSQL enforces `tenant_id = current setting` even if the application forgets the
   filter; the connection sets the tenant at the start of each transaction (`SET LOCAL`). Deny by default: a table without a policy is a defect
   (Hyvva's `RlsDenyByDefaultRatchetTests`).
4. **Bypassing the filter is rare and reviewed**: every `IgnoreQueryFilters()` call has a test and a comment ("`IgnoreQueryFiltersTests`" in Hyvva).
5. **Tests with two tenants** assert that every list, search, export, report and background job returns only the caller's data (G-34).
6. **Tenant deletion purges everything**, proved by a test that lists every table (Hyvva's `TenantPurgeCoverageTests`).
7. **Per-tenant limits and budgets** (rate, storage, email volume), so one tenant cannot exhaust the platform.

---

## Requests and responses

1. **Validate everything at the boundary** ([document 5](05-validation-and-errors.md)).
2. **Security headers on every response, errors included:** `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`, a strict
   `Content-Security-Policy`, `Referrer-Policy`, `Permissions-Policy`, and frame protection. An e2e test reads them (exists as `security.spec.ts`).
3. **CORS: none** if the app and API share an origin; otherwise an explicit allow-list of origins and methods, never `*` with credentials.
4. **CSRF:** cookie-authenticated state changes need an anti-forgery token or `SameSite` and origin checks; bearer-token APIs do not.
5. **Cookies:** `HttpOnly`, `Secure`, `SameSite`, the narrowest path and lifetime. Production refuses to run with insecure cookies (`Cookie:RequireSecure`).
6. **Trust the proxy only as configured:** forwarded headers are accepted only from known proxies; **the application's own public URL is configuration**,
   never taken from the `Host` header (see the lesson below).
7. **Limit sizes and rates** (body size checked from `Content-Length` before reading, requests per minute, work at once).
8. **No stack traces, internal names or SQL in any response.**
9. **Output encoding:** Angular escapes by default; never use `bypassSecurityTrust*` or `innerHTML` with user content without sanitizing, and a lint rule flags it.
10. **SSRF:** a server that fetches a URL a user supplied validates the scheme and resolves the host against a deny-list of internal ranges.
11. **File uploads:** size limit, allow-listed type checked by content, random storage names, stored outside the web root (S3), served with a safe content type,
    scanned if the content is shared.

---

## Secrets

1. **No secret in the repository, ever**: not in `appsettings.json`, not in a test, not in an example (examples use obvious placeholders).
2. **Secrets live in AWS Secrets Manager or SSM SecureString**, read at runtime by the function that needs them, through its IAM role.
3. **A secret scan runs before every commit** (gitleaks, locally, G-13) and weekly across history.
4. **Rotate on a schedule and on suspicion**; every secret has a documented rotation step and an owner.
5. **Local development uses dummy values** in a git-ignored file, with a committed `.env.example`.
6. **Signing keys are not stored beside the data they protect** (`mortgage-analytics` stores its signing keys encrypted with a certificate kept apart from them, once a deployment sets one; a production start says so until then).

---

## Data protection and privacy

1. **Collect the least personal data that works.** Each stored personal field has a documented purpose.
2. **Encrypt in transit (TLS 1.2 or higher) and at rest** (managed keys by default; customer-managed where a contract requires). A CDK assertion checks it.
3. **Classify data:** public, internal, personal, sensitive (financial, identity documents). Sensitive data is encrypted in the column or kept out of the main database.
4. **A person can export their data and delete their account.** Both are standard capabilities (`mortgage-analytics` ships "Download your data" and
   "Delete account"); deletion removes everything, including backups on the documented schedule, and is tested.
5. **Retention is set** for logs, sessions, backups and soft-deleted records, and enforced by TTL or a scheduled job.
6. **No personal data in logs, metrics, traces or error trackers** ([document 6](06-logging-observability.md)).
7. **Backups are encrypted, kept off the machine, and restored in a drill** at least quarterly; an untested backup is not a backup.
8. **Legal documents** (privacy policy, terms, subprocessor list) exist before launch and are read by a person qualified to (a task for the owner, not for a tool).

---

## Payments

1. **Never store card data.** Stripe (or the chosen processor) hosts the card form; we store only Stripe's ids. This keeps us in the
   lightest PCI scope (self-assessment A).
2. **Money moves only through the processor's server-side API** with an **idempotency key** on every create, so a retry cannot charge twice.
3. **Amounts are decided on the server**, never trusted from the client.
4. **Never hold other people's funds.** Marketplaces use Stripe Connect so the processor, not us, is the money transmitter (a Livenhood absolute rule).
5. **Test the side effect:** a test asserts the processor was called with the right amount and key, not just that a row became `paid`.
6. **Reconcile** payments against the processor on a schedule and alarm on differences.

---

## Email

1. **SPF, DKIM and DMARC** are configured for the sending domain, and the capability's CDK test asserts it (the-hive's `ses-email-domain` test).
2. **Links in an email use the configured public URL**, never the request's `Host`.
3. **One-time links are single-use, short-lived and unguessable;** the token is never logged.
4. **Bounces and complaints are handled automatically:** a suppression list stops sending to an address that bounced hard or complained.
5. **Marketing mail has an unsubscribe link;** transactional mail says why it was sent.
6. **A mail outage never loses or breaks the action** that triggered it: the work succeeds, the mail is queued and retried (`mortgage-analytics`
   has a test that adding a passkey still works while the mail server is down).

---

## Webhooks and partners

1. **Verify the signature before reading the body as trusted**; reject stale timestamps; reject unknown event types explicitly.
2. **Idempotent processing:** record the event id, ignore a repeat (a kit component).
3. **Respond fast, work later:** acknowledge, queue, process, with a dead-letter queue and an alarm.
4. **Least privilege per partner credential,** one credential per environment.
5. **Timeouts, retries with backoff and jitter, and a circuit breaker** on every outbound call; a partner outage degrades the feature, not the app.

---

## Infrastructure

1. **Separate AWS accounts per environment** (local, dev, staging, production).
2. **IAM least privilege:** each function's role names only the actions and resources it needs. **No wildcard actions** (checked by a ratchet and cdk-nag).
3. **No public buckets;** CloudFront with origin access control in front of S3; S3 block-public-access on.
4. **Encryption on** for every store, queue and log group; retention set.
5. **Network:** functions that need a database reach it privately; nothing else is exposed.
6. **cdk-nag and policy lint** run on every synth (G-36); a finding must be fixed or suppressed with a written reason and an owner.
7. **A budget and billing alarm exists per account,** so a loop or an attack that costs money is noticed.
8. **WAF and rate limits** at the edge for public applications.
9. **Deploys are made only by the owner** and `cdk diff` is read first ([document 3](03-quality-gates.md#release-and-deploy)).

---

## Supply chain

1. **Lock every dependency** (lock files, `npm ci`, locked-mode restore) so a build is the same tomorrow.
2. **Scan for known vulnerabilities** when a manifest changes and weekly (G-40); high and critical block a release.
3. **Pin CI actions to a commit** (Hyvva does), and keep the pipeline's permissions read-only unless a step needs more.
4. **Few, trusted dependencies:** a new package needs a reason, a licence check and a primer ([document 10](10-documentation-standard.md)).
5. **Nothing from an untrusted download is executed;** downloaded files go in their own folder and are not run from there.

---

## Lessons we already paid for

Each of these was a real finding in our own code, fixed, and is now a standing rule.

| Lesson                                                                                                           | Where it was found                | The rule now                                                                    |
| ---------------------------------------------------------------------------------------------------------------- | --------------------------------- | ------------------------------------------------------------------------------- |
| Emailed links followed the request's `Host` header, so a forged header would send a victim to an attacker's site | `mortgage-analytics`, `abca34f`   | The public URL is configuration; startup warns when it is missing in production |
| The account attempt limit counted each IPv6 address, so an attacker with a /64 had billions of tries             | `mortgage-analytics`, `abca34f`   | Limits key on the /64 (`ComputeLimits.ClientKey`)                               |
| Error responses lacked the security headers                                                                      | `mortgage-analytics`, `abca34f`   | Headers are set in `OnStarting`, so every response carries them                 |
| Sign-up for an existing address revealed that the account exists                                                 | `mortgage-analytics`, 29 Oct 2026 | Same answer for new and existing; the owner is emailed                          |
| A passkey request with no `credential` was a 500                                                                 | `mortgage-analytics`, `abca34f`   | Malformed input is a 400 by the validation layer                                |
| A stolen session could add a passkey without the password                                                        | `mortgage-analytics`, `3efa454`   | Sensitive changes re-ask for the password; the owner is notified                |
| The body-size check ran after the body was read                                                                  | `mortgage-analytics` load review  | Checked from `Content-Length` in middleware before binding                      |
| A "completed" Stripe feature only flipped a status; the call never happened                                      | Hyvva, ISSUE-034, 161, 165, 166   | The side-effect rule in the definition of done                                  |
| An agent deployed one stack directly and triggered a real certificate request                                    | the-hive                          | Only the owner deploys                                                          |
| A test-only dependency dragged in vulnerable transitive packages                                                 | Hyvva, ISSUE-221                  | Scan transitive packages; pin leaves above the advisory                         |

Closed in `mortgage-analytics` on 2026-11-04 (hardening row A9 of its UI and UX backlog), so **copy the later code, not the earlier**: a 10-passkey cap checked again
when the ceremony finishes, SMTP that requires TLS, signing keys that can be stored encrypted, expired session rows removed by the app each day, and a per-recipient mail
cap. The keys stay unencrypted until a deployment sets a certificate.

---

## Verifying security

| Check                                     | Tool                                             | Tier        |
| ----------------------------------------- | ------------------------------------------------ | ----------- |
| Every endpoint protected; roles tested    | Endpoint-enumerating test                        | 2           |
| Tenant isolation, with two tenants        | Integration tests, RLS tests                     | 2           |
| Security headers, cookies, CORS           | Playwright and integration tests                 | 2           |
| Account enumeration, lockout, rate limits | Integration tests                                | 2           |
| Secrets in code                           | gitleaks                                         | 1, 3        |
| Known-vulnerable packages                 | `dotnet list package --vulnerable`, `npm audit`  | 3           |
| Insecure code patterns                    | Roslyn security analyzers, ESLint security rules | 2           |
| Infrastructure policy                     | CDK assertions, cdk-nag                          | 2           |
| A running application's common weaknesses | OWASP ZAP baseline, locally                      | 4           |
| Threat model per capability               | A written STRIDE review                          | design      |
| Manual review of risky changes            | A reviewer works through the checklist below     | each change |

---

## Incidents

1. **Contain first:** revoke the credential, disable the feature flag, block the route; then investigate.
2. **Preserve evidence:** logs, request ids, a snapshot; do not "clean up" before copying.
3. **Tell the people affected** as the law and the contract require; the privacy policy states who and how fast.
4. **Write a blameless note:** what happened, how it was found, what was missing, what changes. Every incident ends in a test, a gate or a
   runbook, never only in a warning.
5. **Keep a runbook for the likely ones:** leaked secret, compromised account, abusive tenant, data-exposure report, payment dispute spike.

---

## Review checklist for a capability

Before a capability is released, a reviewer can answer **yes** to each:

- [ ] A one-page STRIDE threat model exists and each threat has a defence or an accepted risk with an owner.
- [ ] Every endpoint has an authorization policy; unauthenticated ones are deliberately marked.
- [ ] The tenant comes from the token and every query is tenant-filtered; a two-tenant test exists.
- [ ] All input is validated, bounded and tested at its edges.
- [ ] No secret, personal data or request body is logged; the log templates were scanned.
- [ ] Every external call has a timeout, retry with backoff and a failure test.
- [ ] Every state-changing call is idempotent or safely repeatable.
- [ ] IAM permissions are the minimum, with no wildcard actions; storage is encrypted with retention set.
- [ ] A person's data can be exported and deleted through this capability, with a test.
- [ ] Alarms and a runbook exist; the alarms are asserted by a test.
- [ ] Dependencies are locked and scanned; no new package without a primer.
