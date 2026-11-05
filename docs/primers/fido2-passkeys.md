# Fido2 and passkeys

> The library that checks the cryptography of passkeys (WebAuthn): we ask it for a challenge, the browser's device signs it, and the library verifies the signature, the website and the replay protection. It is how people sign in without a password.

**Read time:** 14 min · **Version:** `Fido2` 4.3.1 (`Directory.Packages.props`; the `Fido2NetLib` namespace) · **Last verified:** 2026-11-04 against the repository's code; the official documentation was not re-read in this pass, so check a version-specific detail there before relying on it

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) · [How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) · [What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

A **passkey** is a credential kept by a person's device (phone, laptop, security key, password manager) for one website. It replaces the password with a **key pair**: a private key that never leaves the device, and a public key the website stores. To sign in, the website sends a random **challenge**; the device asks the person to unlock it (fingerprint, face, PIN), signs the challenge with the private key, and the website checks the signature with the public key.

**WebAuthn** is the browser standard for this (`navigator.credentials.create()` and `.get()`); **FIDO2** is the family of specifications around it. **Fido2** (also called fido2-net-lib) is the .NET library that does the server half correctly: building the options the browser needs and verifying what comes back.

## Why we use it

A password can be phished, reused, guessed or leaked. A passkey is bound to the website's domain, so a fake site cannot ask for it, and the server stores only a public key, so a database leak gives an attacker nothing to sign in with. People find it easier too: one touch, nothing to type.

Verifying a WebAuthn response by hand means parsing CBOR and COSE structures, checking flags, the origin, the relying-party id hash, the signature algorithm and the signature counter. Every detail is a way to be insecure. The library does all of it and has been tested against the specification.

Alternatives: a hosted provider (Cognito's passkey support is planned for the serverless phase); or skipping passkeys and using passwords alone, which is simpler and weaker. Passkeys here are an **addition** to the password, so no one is locked out of an account that has none.

## The big idea

> **The server sends a one-time challenge; only the device that holds the private key can sign it; the library checks the signature, the website and that the challenge is fresh.**

The server keeps almost nothing: a public key and a counter per passkey.

## How it works inside

There are two ceremonies, each in two requests.

**Registering a passkey** (the person is already signed in, and confirms their password):

1. `registration-options`: the app calls `RequestNewCredential(...)` with the user (an id, their email as the name), the credentials they already have (so the same device is not registered twice) and the settings we want (`ResidentKey = Required` so the device can offer the passkey without being told the account name; user verification preferred; no attestation, because we do not care which maker built the device). The library returns **options** containing a random challenge.
2. The browser shows the options to the device with `navigator.credentials.create()`; the person unlocks it; the device makes a new key pair and returns an **attestation**: the public key, a credential id, and a signed statement of what was asked.
3. `POST /passkeys`: the app calls `MakeNewCredentialAsync(...)`. The library checks that the **challenge** matches the one it issued, the **origin** is our site, the **relying party id hash** is our domain, the flags say the person was present, and the format is valid. It returns the credential id, public key and counter, which we store.

**Signing in:**

1. `sign-in-options`: `GetAssertionOptions(...)` returns a fresh challenge. We do not name any account (`AllowedCredentials = []`): the device offers the passkeys it holds for this site.
2. `navigator.credentials.get()`: the person picks a passkey and unlocks it; the device signs the challenge and returns an **assertion** with the credential id, a **user handle** and the signature.
3. `POST /passkeys/sign-in`: we look up the stored passkey by credential id, then `MakeAssertionAsync(...)` checks the signature with the stored public key, the challenge, the origin, the domain and the **signature counter**: a counter that does not increase suggests a cloned key, and is refused. We also check the user handle matches the account the passkey was made for. Only then do we sign the person in with Identity's `SignInManager`, after the same checks a password sign-in has (confirmed email, not locked out).

**Where the challenge lives between the two requests.** The server must remember which challenge it issued. Keeping it in memory breaks with more than one instance. `PasskeyState` instead seals the options in a token with Data Protection ([the Identity primer](identity-cookies-and-data-protection.md)) that the browser carries back: it is encrypted and signed, valid for 5 minutes, bound to its purpose (register or sign-in) and to the user, and opened once. No instance needs to remember anything.

```
  server --options (challenge) + sealed state--> browser --> device (private key, unlock)
  server <--attestation/assertion + sealed state-- browser <-- device signs
  library verifies: challenge, origin, domain, flags, signature, counter  ->  store key / start session
```

## How it fits with everything else

- It sits **beside Identity**: the passkey proves who you are, then `SignInManager.SignInAsync` creates the same session cookie a password does.
- Data Protection seals the challenge state; the passkeys table is in `CalculationsDbContext` ([EF Core](ef-core-and-testcontainers.md)), accessed through `IPasskeyRepository`.
- The browser half is in the Angular client (`core/auth/`) and calls the WebAuthn API ([Angular primer](angular-for-us.md)).
- It is tested with a software authenticator (`tests/MortgageAnalytics.Api.IntegrationTests/Infrastructure/SoftwareAuthenticator.cs`) in the integration tests ([WebApplicationFactory](webapplicationfactory.md)), with the clock frozen ([testing time](testing-time.md)).
- A notice email is sent when a passkey is added ([MailKit primer](mailkit.md)), so a passkey that is not theirs is found out.

## An example from our code

`src/MortgageAnalytics.Api/Endpoints/PasskeyEndpoints.cs`, the library configuration and the sign-in check:

```csharp
private static Fido2 Fido2For(HttpContext http) =>
    new(new Fido2Configuration
    {
        RPID = request.Host.Host,
        RPName = configuration["Passkeys:ServerName"] ?? "Mortgage Analytics",
        Origins = new HashSet<string> { $"{request.Scheme}://{request.Host}" },
    });

VerifyAssertionResult result = await Fido2For(http).MakeAssertionAsync(new MakeAssertionParams
{
    AssertionResponse = assertion!,
    OriginalOptions = AssertionOptions.FromJson(optionsJson),
    StoredPublicKey = passkey.PublicKey,
    StoredSignatureCounter = passkey.SignCount,
    IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(args.UserHandle.AsSpan().SequenceEqual(passkey.UserHandle)),
}, cancellationToken);
await passkeys.RecordUseAsync(passkey.CredentialId, result.SignCount, clock.GetUtcNow(), cancellationToken);
```

- `RPID` (the **relying party id**) is the domain the passkey belongs to; `Origins` is the exact site the browser must report. Both come from the request, which behind a proxy is the believed public host. A passkey made on one domain does not work on another.
- `OriginalOptions` is the sealed state opened again: the library compares the answer to **the challenge it issued**.
- `StoredPublicKey` and `StoredSignatureCounter` are what we saved at registration; `RecordUseAsync` saves the new counter.
- The user-handle callback is the "is this passkey really that user's" check.
- Every verification failure becomes the same 401 "sign-in failed", so the endpoint does not reveal which part was wrong.

## How to use it

- Passkeys need a **secure origin**: HTTPS, or `http://localhost`. Over plain HTTP on another host the browser refuses.
- Locally, Chrome's DevTools has a **WebAuthn** panel with a virtual authenticator; use it to register and sign in without hardware.
- To change the name shown to people set `Passkeys:ServerName`; do not change the domain after people have registered, or their passkeys stop working.
- Add a feature to the ceremony by writing the integration test with the software authenticator first.

## The rules we follow

| Rule                                                                                  | Enforced by                             |
| ------------------------------------------------------------------------------------- | --------------------------------------- |
| The challenge comes from the server, is random, and is used once, within 5 minutes    | `PasskeyState`; tests                   |
| Origin and relying party id come from the request's believed host, not from the body  | `Fido2For`; proxy tests                 |
| The signature counter is stored and must not go back                                  | `RecordUseAsync`; tests                 |
| A failed verification answers 401 with one message                                    | The endpoint; tests                     |
| Registering needs a signed-in session and the password again                          | `PasswordConfirmation`; tests           |
| A passkey sign-in has the same standing as a password one (confirmed, not locked out) | The endpoint; tests                     |
| At most a fixed number of passkeys per account; a notice email on each addition       | `MaxPasskeysPerUser`; `IAccountNotices` |

## What goes wrong

| Symptom                                         | Cause                                                                        | Fix                                                                                     |
| ----------------------------------------------- | ---------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| "The device's answer could not be checked"      | Origin or domain mismatch (a proxy hides the real host, or the port differs) | Check the forwarded headers and the `Origins` the library builds                        |
| It works locally and not in production          | Not HTTPS, or the domain changed                                             | Serve over HTTPS on the final domain                                                    |
| Registration offered but never completes        | The sealed state expired (5 minutes) or was used already                     | Start again; keep the ceremony quick                                                    |
| Sign-in finds no passkey                        | The passkey was made on another domain, or was removed                       | Register again on this domain                                                           |
| Sign-in fails after a device is restored/cloned | The signature counter went back                                              | Remove the passkey and add it again                                                     |
| Tests cannot make a real signature              | Real authenticators need a person                                            | Use the software authenticator the integration tests use: it signs with a generated key |

## Terms

| Term                        | Meaning                                                                       |
| --------------------------- | ----------------------------------------------------------------------------- |
| Passkey                     | A discoverable WebAuthn credential, often synced between a person's devices   |
| Relying party (RP)          | The website that relies on the passkey; its id is its domain                  |
| Challenge                   | A random value the device must sign, so an old answer cannot be replayed      |
| Attestation / assertion     | The device's answer when registering / when signing in                        |
| Resident (discoverable) key | A credential the device can offer without the site naming the account         |
| User verification           | The unlock step (fingerprint, face, PIN)                                      |
| Signature counter           | A number that grows with each use; a drop can mean a cloned key               |
| User handle                 | An opaque id for the account, stored with the passkey and returned at sign-in |

## Further reading

- W3C: [Web Authentication: An API for accessing Public Key Credentials](https://www.w3.org/TR/webauthn-3/) (the standard); [webauthn.io](https://webauthn.io) and [passkeys.dev](https://passkeys.dev) for plain explanations.
- The Fido2 library's repository documentation ([fido2-net-lib](https://github.com/passwordless-lib/fido2-net-lib)) for the options you can set.
- In this repo: `docs/04-backend.md` (passkeys), `PasskeyEndpoints.cs` and `Security/PasskeyState.cs`.
