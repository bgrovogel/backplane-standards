# EF Core and Testcontainers

> Entity Framework Core turns database rows into C# objects and back; Testcontainers starts a real PostgreSQL in Docker for a test. Together they let us test data access against the real thing
> without leaving a machine behind.

**Read time:** 15 min · **Version:** EF Core 10.0.13 (SQLite, PostgreSQL via Npgsql 10.0.3), Testcontainers.PostgreSql 4.16.0 (`Directory.Packages.props`) · **Last verified:** 2026-11-03

**On this page:** [What it is](#what-it-is) · [Why we use it](#why-we-use-it) · [The big idea](#the-big-idea) · [How it works inside](#how-it-works-inside) ·
[How it fits](#how-it-fits-with-everything-else) · [Example](#an-example-from-our-code) · [How to use it](#how-to-use-it) · [Rules](#the-rules-we-follow) ·
[What goes wrong](#what-goes-wrong) · [Terms](#terms) · [Further reading](#further-reading)

---

## What it is

- **EF Core** is an **object-relational mapper (ORM)**: you work with C# classes and LINQ queries; it generates SQL, runs it, and builds objects from the rows. A **`DbContext`** is the unit of work: it tracks the objects you loaded, notices changes, and saves them together.
- **Migrations** are generated C# files that describe each change to the database schema, so the database can be created and upgraded by running them in order.
- **Testcontainers** is a library that starts a **Docker container** (here PostgreSQL) from test code, waits until it is ready, hands the test a connection string, and removes it afterwards.
- **SQLite** is a small database in a single file or in memory, which EF Core can use for fast tests.

## Why we use it

Writing SQL and mapping code by hand is repetitive and error-prone; EF Core removes it and keeps the schema in version control through migrations. For tests, **fidelity versus speed** is the trade-off: SQLite in memory is very fast but is not PostgreSQL (different types,
case-sensitivity, JSON and constraint behaviour); a real PostgreSQL is faithful but slower and needs Docker. We use both: SQLite for the many everyday data tests, and PostgreSQL through Testcontainers for the few that must prove the production database.

## The big idea

> **Test data access against a real database engine, built by the real migrations, so the test proves what production will do.**

## How it works inside

1. **Model.** The `DbContext` declares `DbSet<T>` properties and configures entities (keys, indexes, lengths) in `OnModelCreating`. EF builds an internal **model** of the schema from them.
2. **Query.** A LINQ query (`context.Saved.Where(item => item.OwnerId == id).OrderByDescending(...)`) is an **expression tree**; the provider (SQLite or Npgsql) translates it to SQL, executes it, and materializes objects.
3. **Change tracking.** Objects loaded through the context are _tracked_. When you change a property, EF notices by comparing to a snapshot. `SaveChangesAsync()` generates `INSERT`, `UPDATE` or `DELETE` for the differences, in one transaction.
4. **Migrations.** `dotnet ef migrations add Name` compares the current model to the last **model snapshot** file and generates a migration class with `Up` (apply) and `Down` (revert). At run time, `Database.MigrateAsync()` applies the pending ones and records them in a history table. The model and the
   migrations can drift if someone changes the model and forgets to add a migration; our test `HasPendingModelChanges` fails in that case.
5. **Providers.** The same model works on several databases. We keep two contexts (`SqliteCalculationsDbContext`, `PostgresCalculationsDbContext`) each with its own migrations, chosen by configuration.
6. **Testcontainers.** A test fixture starts `PostgreSqlContainer`; the library calls the **Docker API** to pull the image, create and start the container with a random free port, polls until PostgreSQL accepts connections, and returns the connection string. On disposal it stops and deletes the container
   (a helper container called **Ryuk** removes strays if the test process is killed).
7. **Categories.** The Postgres tests are tagged `[Category("Postgres")]`, and CI runs `--filter "TestCategory!=Postgres"`, because they need Docker; they run on the developer's machine at tier 4.

```
  SQLite tests (fast, in CI):      test -> DbContext -> SQLite file/in-memory built by REAL migrations
  Postgres tests (faithful, local): test -> Testcontainers starts postgres:18-alpine in Docker -> DbContext (Npgsql) -> REAL Postgres migrations -> container removed
```

## How it fits with everything else

- Repositories are **adapters** implementing a **port** owned by the Application layer ([Architecture](../02-architecture.md)); the tests here prove the adapter.
- **WebApplicationFactory** gives each API host its own SQLite file built by the real migrations ([primer](webapplicationfactory.md)).
- A **port contract suite** runs the same tests against the in-memory fake and the EF repository ([testing strategy](../04-testing-strategy.md#detail-the-kinds-that-surprise-people)).
- **Stored-format tests** protect JSON saved in columns ([testing strategy](../04-testing-strategy.md#the-catalog)).
- In the serverless platform, the default store is PostgreSQL ([ADR 0005](../adr/0005-data-store.md)); **tenant isolation** adds row-level security on top ([security baseline](../07-security-baseline.md#multi-tenancy)).

## An example from our code

From the testing guide of the reference app (`docs/07-testing.md`), the infrastructure tests prove, on a real SQLite built by the real migrations:

- each owner sees only their own calculations; every input survives the round trip; newest first; search is case-insensitive and literal,
- the migrations match the model (`HasPendingModelChanges`): forget `dotnet ef migrations add` and this fails,
- deleting a user deletes their calculations, and no one else's, using the database's own cascade,
- today's saved JSON still loads (`StoredFormatTests`).

On PostgreSQL (`[Category("Postgres")]`, Testcontainers) the same migrations are applied, a round trip is made, and a case-insensitive search is checked, because PostgreSQL's text comparison differs from SQLite's. `HomeComparisonMigrationTests` migrates an empty
database to the previous migration, saves data the old way, migrates up, and checks the data moved correctly.

The registration that chooses the provider (`src/MortgageAnalytics.Infrastructure/DependencyInjection.cs`):

```csharp
services.AddDbContext<CalculationsDbContext, PostgresCalculationsDbContext>(options => options.UseNpgsql(connectionString));   // PostgreSQL
services.AddDbContext<CalculationsDbContext, SqliteCalculationsDbContext>(options => options.UseSqlite(connectionString));    // SQLite
services.AddScoped<ISavedCalculationRepository, SavedCalculationRepository>();
```

The context is **scoped** (one per request), and the repository interface is what the Application layer sees.

## How to use it

```
dotnet ef migrations add AddNotes --project src/X.Infrastructure --startup-project src/X.Api --context SqliteCalculationsDbContext
dotnet ef database update
dotnet test --filter "TestCategory=Postgres"       # needs Docker running
dotnet test --filter "TestCategory!=Postgres"      # what CI runs
```

A new column: write the failing repository test first; change the entity and its configuration; add the migration for **each** provider; run the model-matches-migrations test.

## The rules we follow

| Rule                                                                                              | Enforced by                                                         |
| ------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Migrations match the model                                                                        | `HasPendingModelChanges` test (G-24)                                |
| Every migration has a test that it applies to an empty database and upgrades the previous release | Infrastructure and Postgres tests                                   |
| Database changes are backward-compatible for one release (expand, then contract)                  | Review ([release rules](../03-quality-gates.md#release-and-deploy)) |
| Every query is tenant-filtered; bypassing the filter is rare and tested                           | Global query filter; `IgnoreQueryFiltersTests` pattern              |
| Persistence constraints back up validation (NOT NULL, unique, foreign keys)                       | Entity configuration; tests                                         |
| Never test on production data                                                                     | Policy                                                              |

## What goes wrong

| Symptom                                                             | Cause                                                | Fix                                                      |
| ------------------------------------------------------------------- | ---------------------------------------------------- | -------------------------------------------------------- |
| A test passes on SQLite and fails on PostgreSQL                     | Different case-sensitivity, types, or JSON functions | Add a Postgres-category test for that behaviour          |
| "Pending model changes" test fails                                  | The model changed without a migration                | `dotnet ef migrations add`                               |
| Testcontainers cannot start                                         | Docker is not running                                | Start Docker Desktop; skip with `TestCategory!=Postgres` |
| A slow or N+1 query                                                 | Lazy loading or a query inside a loop                | `Include`, project to a DTO, or one query                |
| Data from a previous test appears                                   | A shared database                                    | A database per host or fixture                           |
| Two requests overwrite each other                                   | No concurrency token                                 | A version column and a 409 on conflict                   |
| The migration drops a column in the same release as the code change | Not backward compatible                              | Expand now, contract next release                        |

## Terms

| Term                   | Meaning                                                                |
| ---------------------- | ---------------------------------------------------------------------- |
| **ORM**                | Object-relational mapper: a library that maps objects to database rows |
| **`DbContext`**        | EF Core's unit of work: tracks changes and saves them together         |
| **Migration**          | A generated, versioned description of a schema change                  |
| **Model snapshot**     | A file recording the model as of the last migration                    |
| **Provider**           | The part of EF Core that talks to one database engine                  |
| **Container**          | A lightweight isolated process environment run by Docker               |
| **Cascade delete**     | The database deleting dependent rows when their parent is deleted      |
| **Row-level security** | PostgreSQL rules limiting which rows a connection may see              |

## Further reading

- [EF Core documentation](https://learn.microsoft.com/ef/core/)
- [Migrations overview](https://learn.microsoft.com/ef/core/managing-schemas/migrations/)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [Testing EF Core applications](https://learn.microsoft.com/ef/core/testing/)
