---
name: grill-with-docs
description: A relentless interview to sharpen a plan or design, which also produces domain docs (a glossary in `CONTEXT.md` and ADRs under `docs/adr/`) as the conversation resolves terms and decisions. Use when the user has a plan, a feature idea, or a design question and wants to align before any code is written.
---

# Grill With Docs

A grilling session that builds the project's domain model as it goes. Two things happen in parallel:

1. **Grilling** — the user is interviewed relentlessly about a plan or design until every branch of the design tree is resolved. The reusable interview primitive.
2. **Domain modeling** — terms and decisions that surface during the interview are captured into `CONTEXT.md` (glossary) and `docs/adr/NNNN-*.md` (decisions), with the user's sign-off each time.

The two activities share the same interview; the docs are simply the durable output of it.

## When to invoke

- The user has a plan, a feature idea, or a design question, and wants to align before any code is written.
- The user is about to make a non-trivial change to the codebase.
- A new concept has appeared that the team doesn't have a shared name for yet.

## When NOT to invoke

- The user wants a quick clarification, not a deep interview. Use the `question` tool directly.
- The user is ready to implement a fully-specified feature. Skip the interview and go straight to `tdd` or `implement`.
- The repo is brand new and has no `CONTEXT.md` or `docs/adr/` yet. This skill will create them lazily; just invoke it.

## Process

### 1. Open the interview

Acknowledge what the user said in one or two sentences — restate the plan, design, or question in your own words. Then ask the first question. Do not propose a solution until the user has answered the first question; otherwise you're foreclosing the interview.

If the user gave a one-line prompt (e.g. "I want to add hybrid search"), open by asking what problem it solves for them, not how it should be implemented.

### 2. Continue relentlessly

Each turn:

1. **Read the latest answer carefully.** If it contains a branch, contradiction, or unresolved sub-decision, drill into that branch. If it doesn't, ask the next unresolved question from your running list of open branches.
2. **Maintain a list of open branches** (mentally or in a scratch file) so you don't forget them.
3. **Name what's being decided.** Frame the question in terms of a decision: "Are we deciding X here?" The user should know what they're answering.
4. **Prefer concrete over abstract.** When the user says "it should be fast," ask "what's the budget?" When they say "secure," ask "against which threat model?"

Stop conditions — the interview is over when:

- Every open branch has a resolution the user has signed off on.
- The user explicitly says "that's enough" or "let's stop grilling."
- You have produced or updated a `CONTEXT.md` and the relevant `docs/adr/` entries for every term/decision that surfaced.

### 3. Capture terms and decisions inline

As terms get defined or refined, and as decisions get made, capture them. Don't batch this at the end — capture each one at the moment it's decided, then continue grilling. The docs are a side effect of the interview, not a deliverable.

**Terms** go into the **Glossary** section of `CONTEXT.md` (creating the file if needed). Each entry is one short paragraph: the term, the precise definition in the project's voice, and what it is _not_ (when the line is easy to blur).

**Decisions** become ADRs under `docs/adr/`. Number them `NNNN-kebab-case-slug.md` starting at 0001. Each ADR has at least: **Context**, **Decision**, **Consequences**. Keep ADRs short. If a decision is fully trivial ("we'll use kebab-case for filenames"), skip the ADR and capture it as a one-liner in `CONTEXT.md` instead — ADRs are for things that constrain future work.

### 4. Confirm before finalising

Before ending the interview, present a summary:

- The resolved plan, in numbered form.
- The terms added or changed in `CONTEXT.md`.
- The ADRs created or updated.

Ask the user to confirm or amend. Only after their sign-off is the interview done.

## Output discipline

- One question per turn. Don't stack questions; the user can only really answer one at a time.
- Keep the question short and named. Long preambles drain focus.
- When the user says "I don't know," that's a real answer: capture the open question as a `## Open questions` section in the most relevant ADR (or in a new ADR if there isn't one), and continue the interview by exploring adjacent branches.
- Don't write any code or modify any non-doc files during this skill. This is a docs-and-alignment skill.

## Anti-patterns

- **Foreclosing.** Asking "should we use X or Y?" when the user hasn't yet articulated what they're trying to do. Always open with the _why_, not the _how_.
- **Doc dump at the end.** Writing all the `CONTEXT.md` and ADR updates in one shot at the end, after a long interview. The user forgets what they decided. Capture inline.
- **Capturing the wrong thing.** ADRs for trivial choices; glossary entries for jargon the project doesn't actually use. If a term doesn't come up in conversation, don't add it.
- **Re-interviewing after a confirmation.** Once the user signs off on a decision, it's done. Move to the next branch.
