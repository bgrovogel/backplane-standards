# 13. buybright compared with `mortgage-analytics`

> What the older buybright app has that the mortgage app does not, sorted by whether it is worth bringing across. Read from the code on 2026-11-03 (`C:\Dev\buybright`, last commit 2026-05-01, 58 commits).

**Read time:** 10 min · **Best for:** deciding which buybright ideas to port · **Builds on:** [Strategy](00-strategy.md#what-we-found-in-the-five-existing-apps) · **How checked:** every feature below was searched for in the mortgage app's routes, `docs/00-features.md` and `client/src`; "none" means none was found

**On this page:** [The short answer](#the-short-answer) · [Calculators and tools buybright has](#calculators-and-tools-buybright-has) · [Content and checklists](#content-and-checklists) · [The product layer around the calculators](#the-product-layer-around-the-calculators) · [What the mortgage app already does better](#what-the-mortgage-app-already-does-better) · [Recommendation](#recommendation)

---

## The short answer

Yes, buybright has things the mortgage app lacks, in three groups:

1. **A few calculators and tools** for the _buying journey_ (down payment assistance, credit-score effects, student loans, debt payoff order, first-year costs, a reserves stress test, loan-estimate review).
2. **Process content** (a closing timeline, an inspection checklist, a pre-approval checklist, a roadmap).
3. **A multi-role product layer** (buyers, agents, admins, lenders, messaging, documents, Stripe subscriptions, an admin console).

The mortgage app is much deeper on the core mortgage maths and quality; buybright is wider across the buyer's journey and has the business layer. Group 3 is exactly the shared work the platform plan calls **capabilities**, not features to copy into the mortgage app.

---

## Calculators and tools buybright has

| buybright                                                              | What it does                                                                                                              | In the mortgage app                                                                                                 | Worth porting?                                                                                                                                                                                                          |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Down payment assistance finder** (`dpaPrograms.ts`, `DPAFinderPage`) | A curated list of programs by state (grants, forgivable and deferred loans), with income limits and requirements; filters | **None**                                                                                                            | **Yes, high value.** But the data goes stale: every program needs a source link and a "last checked" date, and the file's own comment says it is "not exhaustive"                                                       |
| **Credit score effect** (`creditImpact.ts`, `CreditScorePage`)         | Rate adjustment by score tier, payment and buying power at each tier, a list of improvement tips                          | The score is used only for FHA's minimum down payment                                                               | **Yes, with care.** The tier table is hard-coded and described as "based on Fannie Mae LLPA matrix"; the real adjustments depend on loan-to-value as well as score, so it would need sourcing from the published matrix |
| **Student loan plans** (`studentLoan.ts`, in `DebtRoadmapPage`)        | Compares standard, income-driven and public-service forgiveness payments and their effect on the debt-to-income ratio     | A student loan is only a monthly debt figure                                                                        | **Maybe.** Federal plan rules have been changing, so every rule would have to be re-verified before it is copied                                                                                                        |
| **Debt payoff roadmap** (`DebtRoadmapPage`)                            | An order in which to pay debts to improve debt-to-income fastest                                                          | None                                                                                                                | **Yes**, a good fit beside the affordability view                                                                                                                                                                       |
| **First-year true cost** (`firstYear.ts`, 23 lines)                    | Adds moving, a repairs buffer (a percentage of price) and furniture to twelve months of housing cost                      | None                                                                                                                | **Yes, small.** Easy and useful, with clearly labelled assumptions                                                                                                                                                      |
| **Reserves stress test** (`stressTest.ts`, `BudgetPage`)               | Simulates 24 months of cash reserves against default repair events (HVAC, roof, appliance, plumbing, medical)             | The mortgage app's "If things got tighter" stresses _rate and income_, not reserves and repairs                     | **Yes.** Different question, complements what exists                                                                                                                                                                    |
| **Loan Estimate review** (`LoanEstimatePage`)                          | You type the lender's numbers and see how they differ from an estimate                                                    | The Closing view takes the Loan Estimate's figures as inputs but does not compare them with an estimate of its own  | **Maybe.** Valuable as "is this lender's quote reasonable"; needs reference ranges that can be defended                                                                                                                 |
| **Negotiation tools** (`NegotiatePage`, 5 tabs)                        | Points, a 2-1 buydown, seller credits, an offer analyzer, lock versus float                                               | Points and temporary buydowns (more complete than buybright's) exist; no seller-credit comparison or offer analyzer | **Partly.** Credits and an offer analyzer are new; the rest the mortgage app already does better                                                                                                                        |
| **Rate lock vs float** (`rateLock.ts`)                                 | Payment and interest if rates move ±0.25% and ±0.5% before closing                                                        | A "what if the rate were different" slider covers the payment effect (±1.5 points in eighths)                       | **No.** Largely covered                                                                                                                                                                                                 |
| **Counter-offer tools** (`CounterOfferPage`)                           | Strategy helpers for a counter-offer                                                                                      | None                                                                                                                | **Maybe**, if the offer analyzer is built                                                                                                                                                                               |
| **Take-home pay** (`tax.ts`, 149 lines)                                | Federal brackets (2025), FICA, 401(k): gross to take-home                                                                 | Income is gross; an owner tax profile uses a marginal rate                                                          | **Maybe.** The mortgage app already carries 2026 tax figures; a take-home view would reuse them, not buybright's 2025 ones                                                                                              |
| **Approval probability** (`approvalProbability.ts`)                    | A 0 to 100 "probability" from credit, debt-to-income and down payment                                                     | None                                                                                                                | **No, not as a probability.** The weights (40, 30, 20...) are invented, and a precise percentage the data cannot support is misleading. A sourced readiness checklist would be honest                                   |

Features buybright has that the mortgage app **already covers**: bi-weekly payments, extra payments and pay-off tracking (the "a loan you already have" mode), refinance break-even, rent versus buy, property-tax rates by state, closing costs, PMI advice, affordability, comparing homes, scenario saving, CSV and print export.

---

## Content and checklists

| buybright                                                 | What it is                                                                    | In the mortgage app                               | Worth porting?                                                                        |
| --------------------------------------------------------- | ----------------------------------------------------------------------------- | ------------------------------------------------- | ------------------------------------------------------------------------------------- |
| **Closing timeline** (`ClosingTimelinePage`)              | A 30- or 45-day checklist with a wire-fraud warning, dated from an offer date | None                                              | **Yes, as Learn content.** Static text, high value, needs a careful read for accuracy |
| **Inspection checklist** (`InspectionPage`)               | What to ask and check at an inspection                                        | None                                              | **Maybe**, as a reading page                                                          |
| **Pre-approval checklist** (`PreapprovalPage`)            | A readiness checklist with documents to gather                                | None                                              | **Yes, as a sourced checklist** (without the invented probability)                    |
| **Home-buying roadmap** (`RoadmapPage`)                   | Stages from planning to closing                                               | None                                              | **Maybe**                                                                             |
| **Help** (`HelpPage`): FAQ, documents, property-tax guide | Reference                                                                     | A glossary and a "how it's calculated" page exist | **No**, covered                                                                       |

---

## The product layer around the calculators

This is a different _kind_ of thing: it makes buybright a business with several kinds of user, not a calculator.

| buybright                                                                                                                  | Evidence                                                                                          | Where it belongs                                                                                             |
| -------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| **Roles:** buyer, agent, admin, lender, with route guards                                                                  | 3 shells (`AppShell`, `AgentShell`, `AdminShell`); `user.role` checks in handlers                 | The login capability's roles and policies                                                                    |
| **Agent tools:** invitations with agent branding, buyer list, readiness score, activity timeline, notifications, analytics | `handlers/agent/`, `AgentProfiles`, `Invitations`, `BuyerActivities`, `AgentNotifications` tables | A product on its own, not a feature of a calculator                                                          |
| **Messaging** between buyer and agent                                                                                      | `handlers/agent/messages.ts`, `AgentMessages`                                                     | A notifications or messaging capability, if ever needed                                                      |
| **Document vault**                                                                                                         | `handlers/documents.ts`, `Documents`                                                              | The files capability                                                                                         |
| **Stripe subscriptions** (Free 5 buyers, Starter $19 for 25, Pro $49 for 100, Team $99 for 500)                            | `handlers/billing/`, `TIER_LIMITS` in `webhook.ts`                                                | The billing capability (also in the-hive and Hyvva)                                                          |
| **Lender rates and referrals**                                                                                             | `handlers/lender/`, `LenderProfiles`, `LenderReferrals`                                           | Product-specific                                                                                             |
| **Admin console:** users, agents, revenue (MRR and ARR), audit logs, health                                                | `AdminShell` pages; `AuditLogs`, `AnalyticsEvents`                                                | Audit-trail and admin capabilities                                                                           |
| **Audit log** that survives user deletion                                                                                  | `AuditLogs` with no foreign key to users                                                          | The audit capability (a good pattern already noted in [document 6](06-logging-observability.md#audit-trail)) |

The mortgage app has none of this: it has one kind of signed-in user, no agents, admins, billing or messaging. That is a deliberate scope choice, not an omission to fix.

---

## What the mortgage app already does better

For balance, and because it affects what _not_ to copy:

| Area                 | buybright                                                        | `mortgage-analytics`                                                                                                       |
| -------------------- | ---------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Money arithmetic     | Floating point in TypeScript (`mortgage.ts`), rounded at the end | Exact `decimal`, cent rounding in one place, schedules end at exactly $0.00                                                |
| PMI                  | A flat 0.55% below 20% down                                      | Rules for 78% and 80%, midpoint, request versus automatic, FHA MIP tables, VA funding fee                                  |
| Loan types           | Conventional-style                                               | Conventional, FHA with county limits, VA, ARMs with caps, temporary buydowns, recasting                                    |
| Taxes                | 2025 federal figures and one effective rate per state            | 2026 figures, state-specific rules (California, Florida, Michigan, Oklahoma, Texas units), SALT cap, sale and rental maths |
| Verification         | 77 API and 209 front-end unit tests plus e2e                     | About 4,320 tests, mutation testing, an independent Python oracle, contract snapshot, accessibility and reader-loss suites |
| Accounts and privacy | Cognito                                                          | Passkeys, sessions list, export and delete, privacy-safe logging                                                           |
| Languages            | English                                                          | English and Spanish                                                                                                        |

---

## Recommendation

1. **Do not copy buybright's code.** Its maths is less exact and in a different language; port the _ideas_ and rebuild them test-first in the mortgage app's domain, with sourced data.
2. If features are wanted, the best candidates, in order of value for effort, are: **down payment assistance finder** (needs sourced, dated data), **first-year true cost**, **reserves stress test**, **debt payoff roadmap**, a **closing timeline** as Learn content, and a **sourced pre-approval checklist**.
3. Treat **credit-score rate effects**, **student-loan plans** and **loan-estimate review** as "yes, after the source tables are found and verified".
4. **Do not port** the approval "probability", the lock-versus-float page, or the agent, admin, billing and messaging layers; the last group is the capability work in the platform plan, for a product that needs it.
5. Per the working convention for this repository, new features are offered as a menu and chosen by the owner; none of these is started until it is.
