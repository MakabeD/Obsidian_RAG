# CONTEXT

> The shared glossary for `Obsidian_RAG`. Capture terms here when `grill-with-docs` surfaces them, or when an existing term gets refined. Each entry is one short paragraph: definition, what it is _not_ (when the line is easy to blur), and any example shape that disambiguates.

## The service in one paragraph

`Obsidian_RAG` is an ASP.NET Core 10 minimal-API service that ingests Obsidian vaults (uploaded as zip files), chunks and embeds their Markdown contents with a local ONNX model, and stores the vectors in a ChromaDB instance behind a session-scoped collection. A client opens a session, uploads a vault, runs a `/query`, and the service returns the top-K nearest chunks. Sessions are short-lived (TTL sweeper) and named by client-generated id.

## Glossary

### Vault
A user's Obsidian vault: a folder of `.md` files (plus assets) that gets uploaded as a single zip. The service treats it as a **read-only** blob of documents; the on-disk shape (linked notes, frontmatter, tags, `[[wikilinks]]`) is preserved as part of the document text but not parsed beyond chunking.

### Document
A single Markdown file extracted from a vault. After upload, a `Document` is the unit of source attribution: a chunk carries the `Document`'s filename and an in-document index.

### Chunk
A contiguous slice of a `Document`, bounded by `Rag.ChunkThreshold` (default 600 characters). Chunks are the unit of embedding and retrieval; a `Document` is the unit of attribution.

### Embedding
A fixed-length float vector produced by the local ONNX model (`model/model.onnx` + `model/vocab.txt`). The service does not call any external embedding API. Embeddings are non-deterministic across model versions, so tests against real embeddings must use a pinned model and a tolerance, not a snapshot.

### Session
A client-scoped namespace that owns one Chroma collection. The client creates a session via `POST /session`, receives a `sessionId`, uploads vault(s) into it, queries it, and either lets it expire (`SessionTtlMinutes`, default 10) or terminates it via `DELETE /session/{id}`. Sessions are **not** persistent across restarts: the sweeper deletes idle sessions and their collections.

### Session-scoped collection
The Chroma collection backing a single session. Identified by `<sessionId>` in Chroma. Records inside carry a synthetic chunk id (`{sessionId}_{fileName}_{shortHash}_{index}`) so the service can compute ids without a round-trip and so the same chunk uploaded twice into the same session deduplicates.

### Top-K
The number of nearest chunks returned by `/query`. Bounded between 1 and `Rag.MaxTopK` (default 50). The client may pass an override; defaults to `Rag.DefaultTopK` (5).

## Open questions

_None yet. Run `grill-with-docs` before starting non-trivial work to populate this section._

## Changelog

- 2026-09-01: skeleton created during port of `mattpocock/skills` to opencode. Terms inferred from `Program.cs` and `appsettings.json`; not yet signed off by the maintainer. Treat as draft.
