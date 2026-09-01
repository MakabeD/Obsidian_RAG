# AGENTS

> This file is for any AI coding agent (or human) operating in this repo. It points at the project's domain docs and lists the engineering skills that are installed under `.opencode/skills/`.

## Agent skills

### Issue tracker

GitHub issues at `https://github.com/MakabeD/Obsidian_RAG/issues`, accessed via the `gh` CLI. See `docs/agents/issue-tracker.md` for conventions.

### Domain docs

Single-context layout: one `CONTEXT.md` at the repo root, ADRs under `docs/adr/`. See `docs/agents/domain.md` for how to consume them.

### Installed skills

Located under `.opencode/skills/`:

- `setup-matt-pocock-skills` — run once per repo; sets up the issue tracker choice and the domain-doc layout. Already done for this repo; the `## Agent skills` block above is its output.
- `grill-with-docs` — interview-driven planning that produces `CONTEXT.md` and ADR updates as a side effect. Invoke before any non-trivial change.
- `tdd` — red-green-refactor at pre-agreed seams. Reference skill; not invoked by name.
- `code-review` — two-axis review (Standards + Spec) of a diff against a fixed point. Invoke with a fixed point (`HEAD~1`, `main`, a SHA, etc.).
- `codebase-design` — deep-modules vocabulary (seam, interface, depth, adapter, leverage, locality). Reference skill; reached by name when interface design is on the table.

## Module map (one-line summary of each top-level folder)

- `chromaService/` — `ChromaService` (HTTP client to Chroma), health checks, the global exception handler, request logging, and `RagOptions`.
- `sessionService/` — `SessionRegistry` (in-memory id → `SessionState`) and `SessionSweeper` (background TTL sweeper).
- `model/` — the ONNX model and vocab file. **Gitignored.** LFS or external download required.
- `inputing/` — `VaultReader` (zip → `DocumentData` stream), the chunker, and the embedding adapter.
- `configuration/` — additional config glue. Empty or trivial at the moment.
- `chromadb/` — `docker-compose.yml` for a local Chroma instance.
- `Program.cs` — composition root: DI wiring, options, Kestrel limits, endpoint mapping, and the id-rewriting helpers (`RewriteChunkIds`, `SanitizeForId`, `ShortHash`). A candidate for splitting; see `codebase-design`.

## Conventions

- **.NET 10**, nullable enabled, implicit usings, `WebApplication.CreateBuilder` minimal hosting.
- **HTTP only locally**: Chroma is bound to `127.0.0.1:8000` in `docker-compose.yml`. Don't change this without an ADR.
- **No `CONTRIBUTING.md` yet.** For now, follow the patterns in `Program.cs` and the existing services; the `codebase-design` skill is the source of truth on module shape.
- **No test project yet.** When you add one, the `tdd` skill is the reference, and the test project should be `tests/ObsidianRAG.Tests/` with subfolders per module under test.
