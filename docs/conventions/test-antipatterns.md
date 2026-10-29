> **Read this before writing or reviewing any test in this repository**, and before adding a fake, a fixture, or
> a wait under `tests/`. It is the C#-and-this-project half of the generic `test-anti-patterns` skill: that skill
> knows what a test smell looks like in many languages, this file knows what one looks like *here*.
>
> **Provenance.** Adapted from `docs/conventions/test-antipatterns.md` in the Scripts (GitMetrics) repository, which
> was itself adapted from the WinUiFileManager one, where nearly every rule was written because that suite had
> already broken it. The rules are kept; the git and WinUI examples are replaced with MongoDB and MVVM ones. Where a
> rule has no example here yet it says so. When you write the first test that demonstrates it, name it here.

# Test antipatterns — C# and this project

## The frame

The suite is xUnit v3 on .NET 11, with **Shouldly** as the assertion library and **Moq** for mocks.

| Project | Subject |
| --- | --- |
| `tests/JobTracker.Contracts.Tests` | The wire format, against the examples in `docs/api/`. |
| `tests/JobTracker.Domain.Tests` | Entities, value objects, business rules. Pure in-memory; no Docker, no database. |
| `tests/JobTracker.Application.Tests` | Use cases, against fakes or mocks of the ports. No database. |
| `tests/JobTracker.Infrastructure.MongoDb.Tests` | The data layer, against a real MongoDB in Docker (`MongoDbFixture`). |
| `tests/JobTracker.Api.Tests` | The service over HTTP, hosted in memory by `WebApplicationFactory`. |
| `tests/JobTracker.UI.Tests` | View models, the JSON flattener and the HTTP client. No window, no display, no service. |
| `tests/JobTracker.Architecture.Tests` | The guardrails. See [§12](#12-the-architecture-suite-is-a-ratchet). |

`tests/JobTracker.TestSupport` backs all of them. It is a library, not a test project, and hosts no tests.

**A test belongs to the project named after what it tests.** A test that needs a project reference its home
project does not have is filed in the wrong place. Move the test; do not widen the references.

**Test names** are `Subject_WhenCondition_ExpectedOutcome`: `Close_WhenAlreadyClosed_IsRejected`. Test
classes are `public sealed`.

---

## 0. What every test must say, and what it must assert

A test name is read when it fails, months later, by someone who was not there. The name alone rarely carries
enough. So every test carries a three-line header directly above its attribute.

```csharp
// Trigger: the recruiter's call is recorded after the interview it led to, which was recorded first.
// Behavior: the timeline still reads in the order things happened.
// Assertion: the events are the 22 October call, then the 28 October interview.
[Fact]
public void RecordEvent_WhenOlderThanTheLastOne_KeepsTheTimelineInOrder()
```

### 0.1 Trigger — one line, in the user's world

**The situation, and what someone did to get there.** Written as a person using the app would describe it: an
application, a company, an answer, an interview. Not a method name, not a class name, not a mock.

- ✅ "An application sits in Interview and the company sends a rejection."
- ✅ "The user types a search while an application is open."
- ❌ "`JobApplication.MoveTo` is called with `Rejected`." — that is the call, not the trigger.

**For a bug fix this line is the reproduction.** If you cannot write it, you do not yet know what you fixed.

For a test whose subject genuinely has no user — an architecture rule, a serializer detail — the trigger is the
developer action that hits it: "A `[BsonElement]` attribute is added to a domain type."

### 0.2 Behavior — one line, the effect the caller should get

**What the app must do**, stated as an observable outcome. Not the mechanism, not the call sequence.

- ✅ "The application is closed, and a second close is refused."
- ❌ "`IJobApplicationRepository.UpdateAsync` is called." — that is the implementation.

### 0.3 Assertion — one line, what proves it

**Name what proves the behavior, with its expected value.** In domain terms, not framework terms.

- ✅ "The status is closed, the reason is Rejected, and the timeline has three entries."
- ❌ "`ShouldBe` passes." — restates that an assertion exists.

**One assertion per test is a guideline, not a rule.** A test verifies one *behavior*, and a behavior often needs
several facts pinned. Where the assertions are facets of the same outcome, name the outcome on this line and let
the body assert as many as it takes.

Two things do warrant a split, and neither is a count:

- **Two different triggers.** If the header needs two trigger lines, it is two tests.
- **A verdict that contradicts another verdict in the same body.**

**What is never the answer is a swarm of near-identical tests.** If splitting would give you five methods with the
same body and one different literal, you want a `[Theory]`. See §0.6.

### 0.4 Link the documentation when the test depends on documented platform behavior

When a test turns on how MongoDB, the C# driver, or .NET behaves — not on our own code — link the page that says
so, on the line that needs it, and say what it establishes.

```csharp
// The driver stores a Guid as subtype 4 only when told to; the default since 3.0 refuses to guess:
// https://www.mongodb.com/docs/drivers/csharp/current/serialization/guids/
```

- **A real, specific page**, not a search result and not the root of a documentation set.
- **Say what it establishes**, in the same comment.
- **Only where the knowledge is load-bearing.** A link on `ShouldBe` is noise. A link on GUID representation,
  `Decimal128`, transaction requirements, or `ReplaceOne` match semantics is the reason the test is shaped the way
  it is.

### 0.5 Assert on the effect; verify an interaction when the interaction is the contract

**Behavior is the default verdict.** A test that only checks that a collaborator was called tests today's
implementation. Assert on what the caller can observe: a returned value, the resulting state, a stored document.

**An interaction assertion is valuable when the interaction itself is the contract.** Use it deliberately for:

- **zero calls** — "a command that fails validation saves nothing": that the repository is never written **is**
  the contract;
- **exactly one call** — the coordination contract is that something happens once;
- **an exact number of calls with exact arguments**;
- **a required order** — only when the protocol makes that order observable.

In those tests, state that fact in the three-line header and name the regression it catches.

**Moq is the mocking library.** Use it when one test needs a small configured response, exception, callback or
interaction check and a hand-written type would only repeat that setup. Prefer a hand-written fake when several
tests share meaningful stateful behavior. §3.3 gives the decision rule. Avoid broad `VerifyAll` and verifications
of incidental calls.

### 0.6 Ask whether it should be data-driven — before you write the second test

**Every time you write a test, consider a `[Theory]` first.** The moment you are about to copy a test body and
change one literal, stop: you have found a table. Validation rules are the obvious case here (see
`WireFormatTests.NewApplication_WhenARequiredFieldIsMissingOrNull_IsRefused`).

**It does not fit when:**

- the cases need different arrange or act — a `[Theory]` whose body branches on its own input is two tests wearing
  one name;
- a case needs a comment explaining why it is special — write that one as its own `[Fact]`;
- there are two cases and they are genuinely different edge conditions.

| Source | Use it for |
|---|---|
| `[InlineData]` | literals — strings, numbers, bools, enums |
| `TheoryData<…>` via `[MemberData]` | typed data the compiler should check, or values `[InlineData]` cannot hold |
| A generator method | a case space too large to write out |

**Keep it diagnosable.** Where the arguments do not explain themselves, add a description parameter and pass it to
the assertion as its custom message.

**The header still applies.** One three-line header covers the family.

### 0.7 Mark only phases that exist

`// Arrange`, `// Act`, and `// Assert` are boundaries between executable sections. A marker earns its line only
when at least one statement belongs to that phase.

- Full setup, operation, and verification use all three markers.
- A theory whose inputs come entirely from attributes can begin with `// Act`.
- An assertion API that performs the action — `Should.Throw`, ArchUnitNET `.Check` — belongs under `// Assert`.

---

## 1. Never synchronise on the wall clock

**Banned in a test or a test fixture:**

| Construct | Why it fails |
|---|---|
| `Thread.Sleep`, `await Task.Delay` | A loaded build agent is slower than your machine. The sleep either fails a correct test or hides a broken one. |
| `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today` | The result changes between runs, and a run crossing midnight changes it mid-suite. |
| A polling loop with a wall-clock deadline | Reports a timeout instead of a diff, and scales its flakiness with machine load. |
| `new Random()` without a fixed seed | A failure that cannot be reproduced is not a failure you can fix. |

**This repository has a specific version of the problem.** "Applied date cannot be in the future", "closed
before it was applied", and the order of the timeline are all about dates. Every one of them is tested with an
injected `TimeProvider` set to a **fixed** date (`FakeTimeProvider`), and every test date is a literal. "Seven days
before now" is still the clock. A test that would pass in June and fail in July is not a test.

Production code is held to the same rule by `BannedSymbols.txt` under `Debug_Analyzers`.

**A sleep can never prove a negative.** Assert on state instead.

**Instead:** inject `TimeProvider` and advance a `FakeTimeProvider`; complete a `TaskCompletionSource` from the
notification you are waiting for and `await` it with `TestContext.Current.CancellationToken`; seed your random.

## 2. A timeout ends the run, not the work

At the deadline xUnit signals `TestContext.Current.CancellationToken`, reports the test as failed, and moves on.
.NET has no thread abort, so a body that never reads that token keeps running until the process exits.

- **A test that awaits, does I/O, talks to MongoDB, or waits on an event gets `[Fact(Timeout = …)]`** — including
  a synchronous one; [xUnit v3 allows it there](https://xunit.net/docs/getting-started/v3/whats-new).
- **Pass `TestContext.Current.CancellationToken` to whatever the test awaits.** `xUnit1069` enforces the pairing
  under `Debug_Analyzers`. Where an API takes no token (an MVVM `AsyncRelayCommand.ExecuteAsync`), bound it with
  `.WaitAsync(TestContext.Current.CancellationToken)`.
- **A synchronous test with nothing to wait on gets no timeout.** Domain tests are almost all of this shape.

**Take the value from a named constant, never a literal:** `TestTimeouts.DefaultMilliseconds`, or
`TestTimeouts.ContainerMilliseconds` for a test that may be the one paying for the MongoDB container start.

## 3. Use the support that exists, and share it the moment a second project needs it

A fake lives with the tests for the layer whose types it implements. A helper more than one project needs moves to
`JobTracker.TestSupport`.

A test that starts MongoDB to check *validation arithmetic* is an integration test wearing a unit test's name.
Domain rules take literal in-memory values. Keep the container for what genuinely needs a database.

### 3.1 Before writing a fixture, fake, or helper, look for it three times

Search by what the thing *does*, not by the name you were about to give it.

- **In `JobTracker.TestSupport` already?** Reference it. Do not write a local variant.
- **In another test project?** You are the second consumer: move it to test support, point both projects at it,
  and take its comments with it. Never copy it across.
- **A private helper in the class you are editing?** Same rule once a second class wants it.
- **Nowhere?** Write it in the project that needs it. One consumer stays local.

The in-memory repository fake is the first thing in this repository with two consumers
(Application tests and the repository contract tests): it lives in test support from the start.

### 3.2 A fake stays dumb, and says so when it cannot

A fake records state, returns what it was handed, and carries no logic worth a test of its own. Where a fake's
behavior genuinely is load-bearing, it carries a `///` remark naming what depends on it.

**The exception is a fake that is checked against a contract.** The in-memory repository must behave like the
MongoDB one (same "not found", same concurrency conflict), and it is held to that by running the same abstract
contract tests against both. That shared contract is what makes it safe for Application tests to
rely on it.

### 3.3 Choose the smallest test double that explains the scenario

- **A Moq mock** when the test needs one or two configured members, a callback, or an exact interaction assertion.
  Keep setup in the test so the scenario is visible.
- **A fake** when tests need a reusable in-memory model with meaningful state: a dictionary-backed repository.
- **A fixture** when tests share ownership of a real resource: the MongoDB container, a database per test class.

## 4. Every test asserts, and asserts on a value

- **No assertion is a failed test.**
- **Never compute the expected value the way the implementation does.** Write the literal: `"PLN"`, not
  `input.ToUpperInvariant()`.
- **Never assert a value against itself**, directly or through a round trip expected to be an identity. This is the
  trap a data layer is most exposed to: "save, load, `ShouldBe(original)`" passes when the mapper drops a field on
  the way out *and* fills the same default back in on the way in. Pin each direction against a literal instead:
  write, then read the raw `BsonDocument` and assert its fields; insert a literal `BsonDocument`, then load it and
  assert the domain values.
- **A fixture never builds its input through the code under test.** A document a read test needs is inserted with
  the plain driver as a literal `BsonDocument`, never written through the repository being tested.
- **Reach for the assertion that prints both sides.** Shouldly makes the strong form the short one:

  ```csharp
  applications.ShouldBe([newer, older]);           // sequence, in order
  stored["status"].AsString.ShouldBe("open");
  Should.Throw<ArgumentException>(() => SalaryRange.Create(30_000, 20_000, "PLN"));
  ```

- **The exception is a test whose subject is the operator itself** — value-object equality. Assert `a == b` on
  purpose, and say so in a comment.
- **Never wrap an assertion in a condition.** `if (…) { x.ShouldBe(…); }` silently does nothing for some rows.
- **An assertion you have not watched fail is not verified.** In TDD this is free: the red step is that watch.
  When a test is written after the code, break the code on purpose once and confirm it goes red.

## 5. No reflection into the code under test

`GetUninitializedObject`, `BindingFlags.NonPublic`, and `MethodInfo.Invoke` produce a test of an object that
cannot exist at runtime. If a type is too awkward to construct, that is the finding. `InternalsVisibleTo` plus
`internal` is the sanctioned seam.

Reading metadata to assert a convention is fine (that is what the architecture tests do); reaching through it to
manipulate a live object is not.

## 6. State a machine requirement, do not discover it

- The requirement this repository has: **Docker running**, for the infrastructure tests. `MongoDbFixture` fails
  every test there with a message naming Docker when the container cannot start.
- **Never convert an exception into a skip.** `catch (DockerUnavailableException) { Assert.Skip(…); }` turns the
  whole data-layer suite into a vacuous pass on any machine where Docker happens to be off.
- If a gate is ever added with `SkipUnless`, remember that xUnit builds a class fixture before it reads any test's
  gate: a fixture that needs a gated capability acquires it on first use, not in its constructor.

## 7. Fixtures clean up, and say so when they cannot

- The container is removed when the test assembly finishes (`MongoDbFixture.DisposeAsync`).
- Each test class works in **its own database** (a fresh name per class), so no cleanup between classes is needed
  and nothing one class writes is visible to another.
- Report what could not be cleaned through `ITestOutputHelper`. A silent `catch` in cleanup is still a swallowed
  exception.
- Teardown belongs in `Dispose`/`DisposeAsync`, which xUnit runs whatever the outcome — never as the last line under
  `// Assert`.

## 8. Shared static state is a scheduling bug waiting to happen

xUnit runs test classes in parallel. Two hazards are specific to this repository:

- **`BsonClassMap.RegisterClassMap` and `BsonSerializer.RegisterSerializer` are process-wide.** A registration made
  inside one test is seen by every other test in the run, and registering the same type twice throws. Register once,
  from one start-up method, and have the tests call that same method.
- **One MongoDB server is shared by every test class.** A test that asserts "the collection holds two documents"
  in a database another class also writes to fails whenever the scheduler interleaves them. One database per class.

## 9. Name the constants

A reader must be able to tell an arbitrary value from a meaningful one. The load-bearing numbers here are the
schema version and the salary bounds, and any "no answer for N days" threshold you add. A test asserting a boundary names the boundary; `14` and `15`
scattered through a body say nothing about which side of the threshold they are on.

## 10. Bound the runtime before you commit

A single test must finish in well under a minute. The container start is paid once per run; after that a MongoDB
test costs milliseconds. If a test needs hundreds of documents to prove a point, find the three that prove it.

## 11. Test code is production code

`Nullable`, `ImplicitUsings`, and the comment policy do not stop at the production projects.

- **Every type gets a `/// <summary>`.** A test class says which behavior area it pins. A fake says what it stands
  in for **and why the real thing is wrong here**.
- **Narrowest accessibility.** Test classes are `public sealed` because xUnit discovers them; every fake, builder
  and helper beside them is `internal`. Exceptions: `JobTracker.TestSupport`, an assembly or class fixture the
  runner instantiates, and `TheoryData` members.
- **One `GlobalUsings.cs` per test project**, holding `Xunit`, `Shouldly`, and the namespaces every file needs.

## 12. The architecture suite is a ratchet

`tests/JobTracker.Architecture.Tests` exists to stop an agent — or a tired human — doing quiet harm.

- **Rules are written with ArchUnitNET alone** in an `ArchUnitRules` folder: no reflection, no file parsing.
- **An agent may add a rule. Only a human may change or delete one that is already committed.**
- **A superseded rule is added next to, not substituted for, the one it supersedes.**
- **A rule that stops matching anything is a failure, not a pass.** A selector over an empty project matches zero
  types and passes for ever, so an empty project gets no selector until it has a type, and a selector-coverage test
  asserts that every selector matches something.

**Why these are tests and not documentation:** a subagent dispatched to do one bounded job may never read this
file, but it will build the solution and run the suite. A rule that lives in a test is the only form that reaches
it. What actually binds is the other end: every commit is reviewed by a human before it lands, enforced by the
`PreToolUse` hook in `.claude/settings.json`.

---

## Reviewing a test diff

- [ ] Does every test carry the three-line header, with the trigger in the user's world? (§0)
- [ ] Is the test named `Subject_WhenCondition_ExpectedOutcome`, in a `public sealed` class? (frame)
- [ ] Does an interaction assertion describe a real zero/once/count/argument/order contract? (§0.5)
- [ ] When a mock is used, is it smaller and clearer than a fake; when a fake is used, does its state justify it? (§3.3)
- [ ] Do two or more new tests share a body and differ by a literal? That is a `[Theory]`. (§0.6)
- [ ] Is MongoDB, driver or .NET behavior the test depends on backed by a specific documentation link? (§0.4)
- [ ] Any `Thread.Sleep`, `Task.Delay`, `DateTime.*`, or polling loop? Is every date a literal or a fixed `FakeTimeProvider`? (§1)
- [ ] Does every test that can wait carry a timeout from `TestTimeouts`, and pass the cancellation token? (§2)
- [ ] Does a fixture build its input through the code under test? (§4)
- [ ] Is any expected value computed the way the implementation computes it, or checked by a round trip to itself? (§4)
- [ ] Does it add a fixture, fake, or helper that already exists elsewhere? (§3.1)
- [ ] Does every test end in an assertion on a literal expected value, written with Shouldly? (§4)
- [ ] Is any assertion wrapped in an `if`? (§4)
- [ ] Has each new assertion been seen failing (the red step)? (§4)
- [ ] Any reflection or private-field access into the code under test? (§5)
- [ ] Does anything turn a missing Docker into a skip? (§6)
- [ ] Does each test class use its own database? (§7, §8)
- [ ] Does a test register a class map or serializer itself? (§8)
- [ ] Are threshold numbers named? (§9)
- [ ] Does every type the diff adds carry a `///` summary, and is everything but the test classes `internal`? (§11)
- [ ] **Does the diff change or delete an existing architecture rule?** If a human did not ask for that, it is the
      finding. (§12)
- [ ] Does every non-empty phase have its marker, with no marker for an empty phase? (§0.7)

## Related

- [`../../AGENTS.md`](../../AGENTS.md) — repository conventions, the hard rules, and the working agreement.
- The global `test-anti-patterns` skill — the polyglot catalogue this file specialises.
- [Moq Quickstart](https://github.com/devlooped/moq/wiki/Quickstart) — setups, callbacks, call counts.
- The originals: `C:\_github\Scripts\docs\conventions\test-antipatterns.md` and
  `C:\_github\WindowsFileManager\docs\conventions\test-antipatterns.md`.
