# CONTEXT

> The shared glossary for `Obsidian_RAG`. Capture terms here when `grill-with-docs` surfaces them, or when an existing term gets refined. Each entry is one short paragraph: definition, what it is _not_ (when the line is easy to blur), and any example shape that disambiguates.

## The service in one paragraph

`Obsidian_RAG` is an ASP.NET Core 10 minimal-API service that ingests Obsidian vaults (uploaded as zip files), chunks and embeds their Markdown contents with a local ONNX model, and stores the vectors in a ChromaDB instance inside one shared collection, isolated per session by metadata (see [ADR-0001](docs/adr/0001-shared-collection-with-session-metadata-filter.md)). A client opens a session, uploads a vault, runs a `/query`, and the service returns the top-K nearest chunks. Sessions are short-lived (TTL sweeper) and named by client-generated id.

## Glossary

### Vault
A user's Obsidian vault: a folder of `.md` files (plus assets) that gets uploaded as a single zip. The service treats it as a **read-only** blob of documents; the on-disk shape (linked notes, frontmatter, tags, `[[wikilinks]]`) is preserved as part of the document text but not parsed beyond chunking. Uploads are bounded per **request**: the sum of decompressed bytes across every file in one request (all zips and raw `.md` files alike) must stay under `Rag.MaxZipTotalUncompressedBytes` — not per zip archive.

### Document
A single Markdown file extracted from a vault. After upload, a `Document` is the unit of source attribution: a chunk carries the `Document`'s filename and an in-document index.

### Chunk
A contiguous slice of a `Document`, bounded by `Rag.ChunkThreshold` (default 600 characters). Chunks are the unit of embedding and retrieval; a `Document` is the unit of attribution. An upload is rejected (400) before any embedding runs when chunking produces more than `Rag.MaxChunkCount` chunks (default 20 000) — the bound that keeps worst-case inference time finite.

### Embedding
A fixed-length float vector produced by the local ONNX model (`model/model.onnx` + `model/vocab.txt`). The service does not call any external embedding API. Upload chunks are embedded in batches of `Rag.EmbedBatchSize` (default 32) with dynamic padding; a one-row batch is bit-identical to the historical single-chunk path. On CPU the transformer is compute-bound (measured: batching is throughput-neutral), so the meaningful latency bound is `MaxChunkCount`, not batching. Embeddings are non-deterministic across model versions, so tests against real embeddings must use a pinned model and a tolerance, not a snapshot.

### Session
A client-scoped namespace inside the single shared Chroma collection. The client creates a session via `POST /session`, receives a `sessionId`, uploads vault(s) into it, queries it, and either lets it expire (`SessionTtlMinutes`, default 10) or terminates it via `DELETE /session/{id}`. Sessions are **not** persistent across restarts: the sweeper deletes idle sessions' records via a metadata filter. A session owns **no** Chroma collection of its own — see ADR-0001. The registry caps live sessions at `Rag.MaxConcurrentSessions` (default 1000); `POST /session` rejects with **503** when the cap is reached rather than evicting live sessions.

### Session scope
The set of records in the shared collection whose `session_id` metadata equals the session's id. Every operation on Chroma (add, query, delete) must carry this filter; it is the only boundary between sessions. Records carry the synthetic chunk id (`{sessionId}_{fileName}_{shortHash}_{index}`) so the service can compute ids without a round-trip and so the same chunk uploaded twice into the same session deduplicates.

### Session expiry (three-state registry)
`SessionRegistry` tracks each session in one of three states — alive (`_lastSeen`), popped-but-resurrectable (`_expiring`), and claimed-for-deletion (`_deleting`). `PopExpired` moves alive→expiring; a `Touch` **resurrects** an expiring session (upload mid-flight wins); `TryClaimExpired` moves expiring→deleting atomically and is what the sweeper gates its Chroma delete on. The `/md` endpoint Touches again **immediately before the Chroma add**, so a session claimed mid-upload fails that touch and returns 404 instead of a 200 whose vectors then get deleted — the two operations serialize under the registry lock, making "200 with vectors gone" impossible.

### Top-K
The number of nearest chunks returned by `/query`. Bounded between 1 and `Rag.MaxTopK` (default 50). The client may pass an override; defaults to `Rag.DefaultTopK` (5).

## Open questions

_None yet. Run `grill-with-docs` before starting non-trivial work to populate this section._

## Changelog

- 2026-09-21: `/diagnostics/requests` no longer exposes live session ids — `RequestLoggingMiddleware` redacts the session GUID from recorded paths (issue #4).
- 2026-09-21: session registry is three-state (`_lastSeen`/`_expiring`/`_deleting`): a Touch resurrects a popped session, the sweeper claims before deleting (atomic vs. touch), and `/md` re-touches before the Chroma add — closes the sweep-vs-upload race that could return 200 then delete the just-uploaded vectors (issue #6).
- 2026-09-20: chunks embed in batches of `EmbedBatchSize` (new, default 32; measured throughput-neutral on CPU — the model is compute-bound) and uploads are rejected with 400 above `MaxChunkCount` (new, default 20 000) before any embedding runs (issue #3).
- 2026-09-19: `POST /session` now rejects with 503 once `MaxConcurrentSessions` (new, default 1000) live sessions are registered; `SessionRegistry.Create` became `TryCreate` (fixes the unbounded-registry OOM, issue #2).
- 2026-09-18: `MaxZipTotalUncompressedBytes` is now enforced per request (all files combined), not per zip; raw `.md` uploads count toward the total (fixes the multi-zip cap bypass, issue #1).
- 2026-09-11: corrected Session / "Session-scoped collection" to describe the implemented design (one shared collection, `session_id` metadata isolation) and recorded it in ADR-0001.
- 2026-09-01: skeleton created during port of `mattpocock/skills` to opencode. Terms inferred from `Program.cs` and `appsettings.json`; not yet signed off by the maintainer. Treat as draft.
