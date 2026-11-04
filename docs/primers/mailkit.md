# MailKit and MimeKit

> The libraries we use to send email: MimeKit builds the message (headers, text and HTML parts), MailKit talks to an SMTP server and delivers it.

**Read time:** 10 min · **Version:** `MailKit` 4.18.2, which brings `MimeKit` (`Directory.Packages.props`, which also pins MimeKit above the version MailKit asks for, because of a published advisory) · **Last verified:** 2026-11-04 against the repository's code; the official documentation was not re-read in this pass, so check a version-specific detail there before relying on it

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) · [How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) · [What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

**SMTP** (Simple Mail Transfer Protocol) is the plain-text conversation a program has with a mail server to hand it an email for delivery. **MimeKit** builds an email as a **MIME message**: the format that lets one email carry a subject, sender, recipients, a plain-text version, an HTML version and attachments. **MailKit** is the client that opens the connection, secures it, signs in and sends the message.

.NET's built-in `System.Net.Mail.SmtpClient` is marked obsolete by Microsoft, which recommends MailKit for new work.

## Why we use it

Sending a confirmation or reset email must work, be secure and not leak detail. MailKit supports modern TLS, authentication methods and asynchronous calls, and MimeKit gets the awkward parts of email right (encoding non-ASCII names, line lengths, the multipart structure that lets a mail client pick the HTML or text version).

Alternatives: a provider's API (SES, SendGrid, Postmark): fewer moving parts in production and better delivery tracking, and the plan for the serverless phase (SES). SMTP stays useful because **every** provider and every local mail catcher speaks it, so one transport works from a laptop to production.

## The big idea

> **Build the message with MimeKit, hand it to any SMTP server with MailKit, and hide both behind one small interface so the rest of the app never knows email exists.**

## How it works inside

1. **The message.** `MimeMessage` holds headers (From, To, Subject, Date) and a body tree. `BodyBuilder` with both `TextBody` and `HtmlBody` makes a `multipart/alternative` body: the mail client shows the best part it can display.
2. **Connecting.** `ConnectAsync(host, port, SecureSocketOptions.Auto)` opens a TCP connection. `Auto` picks the right security from the port and what the server offers: implicit TLS on port 465, **STARTTLS** (start in plain text, then upgrade the connection to TLS) when the server advertises it, no encryption only for a local catcher.
3. **Authenticating.** `AuthenticateAsync(user, password)` chooses a SASL mechanism the server supports (PLAIN or LOGIN over the now-encrypted connection, or others). We skip it when no username is set, as for a local catcher.
4. **Sending.** `SendAsync(message)` runs the SMTP commands (`MAIL FROM`, `RCPT TO`, `DATA`) and returns when the server has **accepted** the message for delivery. Accepted does not mean delivered: the server may still bounce it later, which the app never hears about.
5. **Disconnecting.** `DisconnectAsync(quit: true)` ends the session politely.

## How it fits with everything else

- The app depends on `IMailTransport`, one method, `SendAsync(EmailMessage)`. `SmtpMailTransport` is the MailKit implementation; `FileOutbox` is the other one, writing the message to a folder.
- `EmailSetup.AddEmail` picks the transport from configuration: with `Email:Smtp:Host` set it uses SMTP, otherwise the file outbox. That is **dependency injection** doing its job ([DI primer](dependency-injection.md)).
- `AccountEmailSender` implements Identity's `IEmailSender<IdentityUser>` (confirmation and reset links) and our own notices (a passkey was added, an account was deleted) ([Identity primer](identity-cookies-and-data-protection.md), [Fido2 primer](fido2-passkeys.md)).
- In Development, `GET /api/v1/dev/outbox` and the app's `/dev/mailbox` page show what was "sent", and the end-to-end tests read the confirmation link from there like a person would ([Playwright](playwright.md)).
- `EmailOptions` (bound with the options pattern) holds the sender address and the SMTP server, port, username and password; secrets come from the environment, never from the repository.

## An example from our code

`src/MortgageAnalytics.Api/Email/SmtpMailTransport.cs`:

```csharp
using var client = new SmtpClient();
await client.ConnectAsync(host, settings.Smtp.Port, SecureSocketOptions.Auto, cancellationToken);
if (!string.IsNullOrEmpty(settings.Smtp.Username))
    await client.AuthenticateAsync(settings.Smtp.Username, settings.Smtp.Password ?? "", cancellationToken);
await client.SendAsync(ToMimeMessage(message, settings.From), cancellationToken);
await client.DisconnectAsync(quit: true, cancellationToken);

public static MimeMessage ToMimeMessage(EmailMessage message, string from)
{
    var mime = new MimeMessage { Subject = message.Subject, Date = message.SentAt };
    mime.From.Add(MailboxAddress.Parse(from));
    mime.To.Add(MailboxAddress.Parse(message.To));
    mime.Body = new BodyBuilder { TextBody = message.Text, HtmlBody = message.Html }.ToMessageBody();
    return mime;
}
```

- A new `SmtpClient` per message, disposed by `using`: simple and safe; a message here is rare (a sign-up, a reset), so a pooled connection would be extra machinery.
- `ToMimeMessage` is `static` and public so a test can check the structure without a server: both parts present, the right addresses, the date the message says it was sent.
- The caller treats a failed send as a known case: the account is created before its email is sent, so the register filter turns a mail failure into a clear 503 ("your account was created, but we could not send the confirmation email").

## How to use it

- **Locally** with no mail server: leave `Email:Smtp:Host` empty and read the outbox on `/dev/mailbox`.
- **With a catcher** such as Mailpit (SMTP on port 1025, web page on 8025): set `Email:Smtp:Host=localhost` and the port; see the mail arrive in its web page.
- **In production** set host, port (587 for STARTTLS or 465), username and password through environment variables, and set `Email:From` to an address on a domain whose SPF, DKIM and DMARC records allow that server to send for it. Without them mail goes to spam.
- Outside Development the app refuses to start without `Email:Smtp:Host` and `Email:PublicUrl` (`Program.cs`), so a deployment cannot quietly send nothing or send links to the wrong address.
- To add an email: write the test of the `EmailMessage` it produces first, then the template; keep a plain-text part as well as HTML.

## The rules we follow

| Rule                                                                        | Enforced by                               |
| --------------------------------------------------------------------------- | ----------------------------------------- |
| Callers depend on `IMailTransport`, never on MailKit                        | Layer tests; code review                  |
| Every email has a plain-text and an HTML part                               | `ToMimeMessage`; a test                   |
| Secrets (SMTP password) come from the environment, never the repository     | Secret scan; deployment guide             |
| Nothing personal in logs: log the outcome, not the address or the body      | `LoggingConventionTests`                  |
| A mail outage must not undo what already happened, and must be said plainly | The register filter; a test               |
| The development outbox exists only in Development                           | `MapDevOutbox` called conditionally; test |

## What goes wrong

| Symptom                                        | Cause                                                                      | Fix                                                                   |
| ---------------------------------------------- | -------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| `SslHandshakeException` or a certificate error | The server's certificate is not trusted, or the port and TLS mode disagree | Use the provider's documented port; do not disable certificate checks |
| Authentication fails                           | Wrong credentials, or the provider needs an app password or API key        | Create the credential the provider documents for SMTP                 |
| Sent but never arrives                         | The sender's domain lacks SPF/DKIM/DMARC, or it is in spam                 | Set up the domain's records; send from an address on it               |
| The links in the email are wrong               | The link is built from the request host, behind a proxy                    | Set `Email:PublicUrl`; links use it instead of the request's host     |
| Works for one message, hangs under load        | A new connection per message with a slow server                            | Use a queue and a worker (planned for the serverless phase)           |
| A non-English name appears garbled             | A header was built as a plain string                                       | Use `MailboxAddress`/`MimeMessage`, which encode correctly            |

## Terms

| Term           | Meaning                                                                    |
| -------------- | -------------------------------------------------------------------------- |
| SMTP           | The protocol for handing an email to a mail server                         |
| MIME           | The format of an email with parts and attachments                          |
| STARTTLS       | Begin in plain text, then upgrade the connection to TLS                    |
| SASL           | The family of sign-in mechanisms SMTP uses                                 |
| SPF/DKIM/DMARC | DNS records that say which servers may send for a domain and sign the mail |
| Mail catcher   | A fake server (Mailpit) that accepts mail and shows it in a web page       |

## Further reading

- MailKit's documentation and the MimeKit documentation (the repository wiki and API docs).
- The provider's SMTP guide for your host; Google's and Microsoft's bulk-sender requirements for SPF, DKIM and DMARC.
- In this repo: `docs/12-deploying.md` (email settings) and `src/MortgageAnalytics.Api/Email/`.
