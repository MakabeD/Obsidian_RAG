---
name: tdd
description: Test-driven development. Use when the user wants to build features or fix bugs test-first, mentions "red-green-refactor", or wants integration tests. Drives a vertical slice per cycle: one failing test, one minimal implementation, repeat.
---

# Test-Driven Development

TDD is the red → green loop. This skill is the reference that makes that loop produce tests worth keeping: what a good test is, where tests go, the anti-patterns, and the rules of the loop. Every section applies on every cycle: consult them before and during the loop, not after.

When exploring the codebase, read `CONTEXT.md` (if it exists) so test names and interface vocabulary match the project's domain language, and respect ADRs in the area you're touching.

## What a good test is

Tests verify behavior through public interfaces, not implementation details. Code can change entirely; tests shouldn't. A good test reads like a specification: "user can checkout with valid cart" tells you exactly what capability exists, and it survives refactors because it doesn't care about internal structure.

## Seams: where tests go

A **seam** is the public boundary you test at: the interface where you observe behavior without reaching inside. Tests live at seams, never against internals.

**Test only at pre-agreed seams.** Before writing any test, write down the seams under test and confirm them with the user. No test is written at an unconfirmed seam. You can't test everything, so agreeing the seams up front is how testing effort lands on the critical paths and complex logic instead of every edge case.

Ask: "What's the public interface, and which seams should we test?"

When the shape of that interface is itself in question (how deep the module is, where the seam belongs, what the interface should expose), consult the `codebase-design` skill for the vocabulary. It is the shared source of the module, interface, depth, seam, adapter, leverage and locality terms, and it is a reference to consult, not a session to run.

## Mocking at the seam

Two rules:

- **Mock at the seam, not inside the module.** If a module takes an `IChromaClient`, the test substitutes a fake `IChromaClient`. It does not mock the HTTP layer unless the seam under test is the HTTP layer.
- **Prefer fakes over mocks-with-assertions.** A hand-rolled fake (in-memory dict, recorder) almost always tests behavior more honestly than a framework mock that records calls.

For .NET specifically: use a fake implementation of the interface (a class in the test project that returns canned data) and inject it via the constructor. Reserve `Moq` / `NSubstitute` for cases where the interaction shape is itself the thing under test.

## Anti-patterns

- **Implementation-coupled**: mocks internal collaborators, tests private methods, or verifies through a side channel (querying the database instead of using the interface). The tell: the test breaks when you refactor but behavior hasn't changed.
- **Tautological**: the assertion recomputes the expected value the way the code does (`expect(add(a, b)).toBe(a + b)`, a snapshot derived by hand the same way, a constant asserted equal to itself), so it passes by construction and can never disagree with the code. Expected values must come from an independent source of truth: a known-good literal, a worked example, the spec.
- **Horizontal slicing**: writing all tests first, then all implementation. Bulk tests verify _imagined_ behavior: you test the _shape_ of things rather than user-facing behavior, the tests go insensitive to real changes, and you commit to test structure before understanding the implementation. Work in **vertical slices** instead: one test → one implementation → repeat, each test a **tracer bullet** that responds to what the last cycle taught you.

## Rules of the loop

- **Red before green.** Write the failing test first, then only enough code to pass it. Don't anticipate future tests or add speculative features.
- **One slice at a time.** One seam, one test, one minimal implementation per cycle.
- **Refactoring is not part of the loop.** It belongs to the review stage (see the `code-review` skill), not the red → green implementation cycle.

## For this repo specifically

- **Test framework**: if not already chosen, ask the user. For .NET, the default is `xUnit` + `FluentAssertions` (or `Shouldly`), with `Microsoft.AspNetCore.Mvc.Testing` for endpoint tests.
- **Test project layout**: `tests/ObsidianRAG.Tests/` next to `obsidian_RAG.csproj`, with subfolders mirroring the module under test (`ChromaService`, `SessionRegistry`, `Chunker`, `VaultReader`, …).
- **Embeddings are non-deterministic by default.** If a test depends on `EmbeddingService`, either (a) inject a fake `IEmbedder` and skip the real model, or (b) pin the model and assert on a tolerance, not on a specific vector. Don't snapshot real embeddings.
- **Chroma is external.** Never hit a real Chroma in unit tests. Use a fake `IChromaClient` (or whatever the seam turns out to be — see `codebase-design`).
- **Health checks** are easy to test and high-value: a tiny in-process WebApplicationFactory harness around `/healthz/ready` pays back on every PR.
