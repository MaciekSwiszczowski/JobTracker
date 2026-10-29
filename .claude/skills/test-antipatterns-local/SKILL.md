---
name: test-antipatterns-local
description: Use when writing, editing, or reviewing any test in this repository, when adding a fake, fixture, or wait to tests/, or when deciding whether a test is flaky. The C# and project-specific half of the generic test-anti-patterns skill — use both together, this one wins where they differ. Named "-local" to distinguish it from the global test-anti-patterns skill.
---

# Test antipatterns here

**Open and read [`docs/conventions/test-antipatterns.md`](../../../docs/conventions/test-antipatterns.md) before
you write or review a test. Read the whole file.**

This file deliberately states none of the rules, not even in summary. Each one exists because this suite already
broke it, and a rule without the symptom that identifies it and the fix that resolves it is worth nothing. There
is nothing to learn on this page.

**This file is a pointer and must stay one** — do not copy the rules here or summarise them; see the sibling
`code-behind` skill for why the content lives in `docs/`.

The generic `test-anti-patterns` skill covers eleven languages and is worth running for a full audit. This file
covers C# and this project, and overrides it wherever the two disagree.
