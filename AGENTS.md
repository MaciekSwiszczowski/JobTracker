# AGENTS.md

Guidance for AI agents and new contributors working in this repository.

## What this repo is

A tracker for the jobs the owner applies for. A Claude skill turns a pasted job offer into JSON and posts it to a
local service; the service stores it in MongoDB as a document (a typed core, an embedded timeline, and a free-form
`extra` part); a read-only Avalonia UI shows it and can close an application.
Its real purpose is **learning a document database (MongoDB) and building the data layer properly, test first.**

That purpose sets the first hard rule below: the owner writes the Domain, Application, Infrastructure and API
endpoint code by hand. An agent explains, reviews and suggests tests there; it does not write the code.

## Documentation — `docs/`

**Read these before starting.** They are the repository's own rules, not general advice.

| Document | What it is |
| --- | --- |
| [`docs/api.md`](docs/api.md) | The service's HTTP contract: endpoints, the document, the conventional `extra` keys. Examples in `docs/api/`. |
| [`docs/conventions/test-antipatterns.md`](docs/conventions/test-antipatterns.md) | How a test is written here: the three-line header, Shouldly, timeouts, fixtures, and the review checklist. |

## Layout

```
JobTracker.slnx
src/JobTracker.Contracts/               The wire format (records + JSON options). References nothing.
src/JobTracker.Domain/                  Entities, value objects, business rules. Plain C#. References nothing.
src/JobTracker.Application/             Use cases and ports (repository interfaces). References Domain only.
src/JobTracker.Infrastructure.MongoDb/  The data layer: implements the ports with MongoDB.Driver.
src/JobTracker.Api/                     The local service (minimal API, localhost:5080). Composition root.
src/JobTracker.UI/                      Avalonia app, MVVM. Read-only plus Close. Talks HTTP via Contracts only.
tests/JobTracker.TestSupport/           Shared test helpers (TestTimeouts). A library, not a test project.
tests/JobTracker.*.Tests/               One test project per production project.
.claude/skills/job-applications/        The skill that turns a pasted job offer into a POST to the service.
inbox/                                  Applications the skill queued while the service was down. Git-ignored.
docs/                                   The API contract and the test conventions        <- start here
```

## Build and run

Three configurations, and the difference between them is the point.

```powershell
# While working - as fast as possible. No analyzers, no NuGet audit.
dotnet build JobTracker.slnx

# The gate. Full analyzer set, warnings as errors. Must be clean before a change is reported done.
dotnet build JobTracker.slnx -c Debug_Analyzers

dotnet test --solution JobTracker.slnx       # needs Docker running for the MongoDB tests
docker compose up -d                         # a local MongoDB for the service
dotnet run --project src/JobTracker.Api      # the service, on http://localhost:5080
dotnet run --project src/JobTracker.UI       # the UI, reading from the service
dotnet run --project src/JobTracker.UI -- --sample   # the UI on built-in sample data, no service needed
```

Analyzer findings are repaired in `Debug_Analyzers`, never suppressed to make the build pass. A suppression
belongs in a `.csproj` or `Directory.Build.props` **with its reason written beside it**, and only for a genuine
false positive.

## Conventions

- .NET 11, C# with `ImplicitUsings` and `Nullable` enabled. Keep both on.
- Global usings live in each test project's `GlobalUsings.cs`.
- One type per file. Every type carries a `/// <summary>`. Comment the *why*, never narrate the next line.
- MongoDB types (`BsonDocument`, `[Bson…]` attributes, `IMongoCollection<T>`) never appear in Domain or
  Application.
- The wire format has three copies that must agree: the records in `JobTracker.Contracts`, the examples in
  `docs/api/`, and `docs/api.md`. Change them together; `JobTracker.Contracts.Tests` reads the examples and fails
  when they drift. The skill follows `docs/api.md`, so it needs no change unless the meaning does.
- Application never references Contracts. The API maps between the two, so the wire format and the use cases can
  change independently.
- The clock is an input: production code reads time through an injected `TimeProvider`.
  `BannedSymbols.txt` bans `DateTime.Now` and friends under `Debug_Analyzers`.
- Library layers use `ConfigureAwait(false)`; the UI project does not (its continuations must stay on the UI
  thread), which is why MA0004 is suppressed there and only there.
- Tests follow [`docs/conventions/test-antipatterns.md`](docs/conventions/test-antipatterns.md), including the
  three-line header on every test and Shouldly for every assertion.

## Hard rules

### The owner writes the domain and the data layer

Do not write production code in `src/JobTracker.Domain`, `src/JobTracker.Application`,
`src/JobTracker.Infrastructure.MongoDb`, or the endpoints of `src/JobTracker.Api`, unless explicitly asked for
that specific code. Explain, review, and propose the next test instead. The UI, the skill, the build/CI plumbing
and the API's host set-up may be generated; so may Contracts, when the owner has agreed the change to
`docs/api.md`.

### Definition of done — both gates, every time

No change is reported complete until both of these have actually been run and have actually passed:

1. **`dotnet build JobTracker.slnx -c Debug_Analyzers` reports 0 warnings and 0 errors**, and
   **`dotnet test --solution JobTracker.slnx` is green.** Not "the Debug build is clean" — that build runs no
   analyzers at all.
2. **A self-review has been run over the diff**, by a reviewer that did not write it, with
   `docs/conventions/test-antipatterns.md` and its checklist in hand when the diff touches `tests/`.

If a gate cannot be run, say so and say why — do not report the change as finished.

### Every commit is approved by a human

Never commit without an explicit approval for that commit. Stage what you changed, show the diff, and stop.
A `PreToolUse` hook in `.claude/settings.json` forces an approval prompt on any `git commit`.

A commit message is **one descriptive line**, under about 72 characters, saying what changed in plain words.
No body and no trailers.

### The architecture suite is a ratchet

The rules live in `tests/JobTracker.Architecture.Tests`.

> **An agent may ADD a rule there. Only a human may CHANGE or DELETE a rule that is already committed.**

If your work fails an architecture rule, fix the code. If you believe a rule is wrong, say so and stop.

### Fixtures never use the code under test to build their input

A stored document that a repository test reads back is inserted as a literal `BsonDocument` with the plain
driver, never written through the repository being tested; otherwise a bug in the write path produces both the
input and the expected result, and the test cannot fail.

## Gotchas

- **Docker must be running for the infrastructure tests.** They fail with a message naming Docker when it is
  not; they never skip.
- **The test MongoDB is a single-node replica set**, and so is `docker-compose.yml`. Transactions refuse to run on
  a standalone server, so do not "simplify" either to a plain `mongod`.
- **`BsonClassMap` and `BsonSerializer` registrations are process-wide static state.** Register once, at
  start-up, from one place; a registration made inside a test leaks into every other test in the run.
- **`inbox/` holds personal data** (real job offers, recruiter names). It is git-ignored; never commit it, never
  move it into `docs/` or a test.
- **The architecture suite reads the git working tree.** `GitWorkingTreeTests` fails while a path is staged and then
  changed again (`AM`, `AD`, `MM` and the like in `git status`) or a new source file is left untracked — `git add` the path, or
  `git restore --staged` it. Those rules skip on CI and off Windows, because the git helper runs every command in a
  Win32 job object.
- **The UI's sample mode is not a test double for the service.** `InMemoryJobTrackerClient` answers the way
  `docs/api.md` says the service does, and the UI tests rely on that; when the documented behavior changes, change
  it too.

## Working agreement

- Work in small changes, each one small enough to read in one sitting.
- Write the test first; add the code until it passes; refactor while green.
