# ASP.NET Core Identity, cookies and Data Protection

> The built-in .NET way to have user accounts: Identity stores users and checks passwords, a signed cookie remembers who is signed in, and Data Protection holds the keys that sign and encrypt that cookie and every emailed link.

**Read time:** 15 min · **Version:** `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.13 and `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.13 (`Directory.Packages.props`) · **Last verified:** 2026-11-04 against the repository's code; the official documentation was not re-read in this pass, so check a version-specific detail there before relying on it

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) · [How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) · [What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

Three pieces that are usually met together:

- **ASP.NET Core Identity** is a library for user accounts. It gives you a `UserManager` (create, find, change a password, delete), a `SignInManager` (check a password, start a session, lock out after too many failures), password hashing, and a set of ready-made HTTP endpoints (`register`, `login`, `confirmEmail`, `resetPassword`...). The users live in your database through Entity Framework Core.
- A **cookie** is a small value the browser sends back with every request. Here it holds a signed, encrypted ticket that says who is signed in. The browser cannot read or change it.
- **Data Protection** is the .NET library that signs and encrypts values with keys it manages for you. The session cookie, the codes in "confirm your email" and "reset your password" links, and our passkey challenges are all sealed by it.

## Why we use it

Passwords are the easiest thing to get wrong: weak hashing, timing leaks, no lockout, reset links that never expire. Identity is maintained by the framework's own team, hashes passwords with a slow salted algorithm, locks accounts out and rotates the hash when the algorithm improves. Writing that ourselves would be slower and less safe.

Alternatives: a hosted identity provider (Cognito, Auth0): the right choice when accounts are shared across many apps, and what the platform plans for the serverless phase (the roadmap's Cognito phase). Here a single app owns its accounts, so the built-in library is enough and needs no external service.

## The big idea

> **Identity decides who you are once, at sign-in. After that the browser carries a sealed ticket, and the server only has to open it.**

Everything else is detail about where the keys that seal the ticket are kept and when the ticket stops being believed.

## How it works inside

1. **Registering.** `POST /api/v1/account/register` creates an `IdentityUser` (id, email, password hash, a **security stamp**: a random value that changes whenever the password or similar security data changes). With `RequireConfirmedEmail = true` the account cannot sign in yet.
2. **The email.** Identity asks an `IEmailSender<IdentityUser>` to send a link. Its default sender sends nothing, so we replace it (`AccountEmailSender`). The link carries the user id and a **code**: a token made by Data Protection, bound to the user and the purpose ("confirm email"), valid for a limited time (Identity's default is one day).
3. **Confirming.** The link opens the app's own page, which calls the confirm endpoint with that code. Data Protection opens the code with the same keys; if it is genuine, unexpired and for this user and purpose, the email is marked confirmed.
4. **Signing in.** `POST login?useCookies=true` checks the password hash. On success the `SignInManager` builds a `ClaimsPrincipal` (the user id and email as **claims**), serializes it into an **authentication ticket**, protects the ticket with Data Protection and sets it in the cookie `mortgage-analytics.session`.
5. **Every later request.** The cookie authentication handler reads the cookie, asks Data Protection to unprotect it, and rebuilds the `ClaimsPrincipal` for `HttpContext.User`. `[Authorize]` and `.RequireAuthorization()` then only look at that principal. No database call is needed to know who you are.
6. **Staying valid.** The ticket expires after 14 days, and with `SlidingExpiration` the cookie is renewed when more than half of that has passed. The **security stamp validator** compares the stamp inside the ticket to the one in the database; if they differ (password changed, account deleted) the ticket is rejected. Identity checks this only every 30 minutes by default; we set the interval to zero so a password reset or account deletion ends other sessions on their next request.
7. **The keys.** Data Protection keeps a **key ring**: keys with an activation date and an expiry, newest used to protect, any not-yet-deleted one used to unprotect. By default each machine makes its own keys, so a second instance cannot open the first one's cookies. We persist the ring either in a folder (`PersistKeysToFileSystem`, a Docker volume) or in the shared database (`PersistKeysToDbContext`, the `DataProtectionKeys` table) so every instance, and every restart, uses the same keys.

```
   register -> email link (code sealed by Data Protection) -> confirm
   login (password ok) -> ticket sealed by Data Protection -> Set-Cookie
   each request: Cookie -> unprotect -> ClaimsPrincipal -> [Authorize] -> endpoint
                                   \-> security stamp still matches? else 401
   key ring in a folder or the database: lose it and every cookie and every emailed link stops working
```

## How it fits with everything else

- **Entity Framework Core** holds the users, roles and keys (`CalculationsDbContext` inherits Identity's tables; see the [EF Core primer](ef-core-and-testcontainers.md)). Migrations create them.
- **Dependency injection** gives endpoints `UserManager<IdentityUser>` and `SignInManager<IdentityUser>` ([DI primer](dependency-injection.md)).
- **Email** is sent through our transport ([MailKit primer](mailkit.md)); the dev outbox shows the links locally.
- **Passkeys** reuse the same Data Protection keys to seal their challenges ([Fido2 primer](fido2-passkeys.md)).
- **Rate limiting** guards the account endpoints so passwords cannot be guessed at speed.
- Tests build the whole app with `WebApplicationFactory` and a frozen clock ([WebApplicationFactory](webapplicationfactory.md), [testing time](testing-time.md)); anything that depends on time passing needs the clock advanced in a host of its own.

## An example from our code

`src/MortgageAnalytics.Api/Endpoints/AccountEndpoints.cs`, `AddAccounts`:

```csharp
IDataProtectionBuilder protection = services.AddDataProtection().SetApplicationName("MortgageAnalytics");
if (configuration.GetValue("DataProtection:Store", KeyStore.FileSystem) == KeyStore.Database)
    protection.PersistKeysToDbContext<CalculationsDbContext>();
else
    protection.PersistKeysToFileSystem(new DirectoryInfo(keys));

services.AddIdentityApiEndpoints<IdentityUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.Password.RequiredLength = 8;
    ...
}).AddEntityFrameworkStores<CalculationsDbContext>();

services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});
services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
```

- `SetApplicationName` makes the keys usable by any instance with the same name; without it the keys are tied to the content root path and break when the app moves.
- The `if` chooses where the key ring lives: a folder for one instance, the database when several instances share it.
- `HttpOnly` hides the cookie from JavaScript (a script injected into the page cannot steal the session); `SameSite=Strict` stops the browser sending it on requests started by other sites (a defence against cross-site request forgery).
- `ValidationInterval = Zero` is the deliberate "end other sessions at once" choice described above.
- `MapIdentityApi` adds Identity's endpoints; `ServeOnlyWhatTheAppUses` (same file) turns off the ones we do not use (bearer-token refresh, two-factor setup) and refuses to hand out bearer tokens, so the only way in is the cookie.

## How to use it

- Run locally with no setup: keys go to `App_Data/keys`, mail goes to the dev outbox (`/dev/mailbox` page). Delete `App_Data/keys` and every session and emailed link stops working, which is a good way to see the mechanism.
- Behind several instances set `DataProtection:Store=Database`.
- Over HTTPS in production set `Cookie:RequireSecure=true` so the cookie is always `Secure`.
- Get the signed-in user in an endpoint with `ClaimsPrincipal` and our `OwnerId()` extension; require sign-in with `.RequireAuthorization()`.

## The rules we follow

| Rule                                                                                    | Enforced by                                |
| --------------------------------------------------------------------------------------- | ------------------------------------------ |
| The session is a cookie only: `HttpOnly`, `SameSite=Strict`, `Secure` in production     | Config; integration tests read the headers |
| No bearer tokens are issued                                                             | The login filter; a test                   |
| Email must be confirmed before sign-in                                                  | `RequireConfirmedEmail`; an e2e test       |
| The key ring survives restarts and is shared by every instance                          | Config; deployment guide                   |
| Account endpoints are rate limited, and answer the same for known and unknown addresses | `AccountPrivacy`; tests                    |
| A password change or deletion ends other sessions at once                               | The zero validation interval; a test       |
| Passwords are checked by length (8+ with a digit), not by a list of character classes   | `IdentityOptions`; NIST SP 800-63B         |

## What goes wrong

| Symptom                                                 | Cause                                                                          | Fix                                                               |
| ------------------------------------------------------- | ------------------------------------------------------------------------------ | ----------------------------------------------------------------- |
| Everyone is signed out after a deploy                   | The key ring was in a container's own disk and was lost                        | Put keys on a volume or in the database                           |
| Signed in on one instance, signed out on the next       | Each instance has its own keys                                                 | `DataProtection:Store=Database`, same application name            |
| An emailed link says "invalid"                          | It expired, was used, or the keys changed; or the email client rewrote the URL | Ask for a new link; keep links short and avoid tracking redirects |
| The cookie is not sent over plain HTTP                  | `Secure` is set and the page is not HTTPS                                      | Use HTTPS; behind a proxy believe its forwarded headers           |
| A test cannot sign in twice or expire a session         | The test host's clock is frozen                                                | Advance the fake clock in a host of its own                       |
| Account deleted but its session still works for a while | The stamp is checked only every 30 minutes (Identity's default)                | Keep the interval at zero, as we do                               |

## Terms

| Term                  | Meaning                                                                                     |
| --------------------- | ------------------------------------------------------------------------------------------- |
| Claim                 | One fact about the user in the ticket, such as their id or email                            |
| Authentication ticket | The claims plus expiry, sealed in the cookie                                                |
| Security stamp        | A random value that changes when security data changes; a ticket with an old one is refused |
| Key ring              | The set of Data Protection keys, each with an activation and an expiry date                 |
| Protect / unprotect   | Sign and encrypt a value / open it, checking it is genuine                                  |
| CSRF                  | A forged request sent by another site using your browser's cookie; `SameSite` blocks most   |

## Further reading

- Microsoft Learn: "Introduction to Identity on ASP.NET Core" and "ASP.NET Core Data Protection overview" (key management, key storage providers).
- OWASP: Session Management Cheat Sheet; NIST SP 800-63B (password guidance).
- In this repo: `docs/04-backend.md` (security) and `docs/12-deploying.md` (the key volume and backups).
