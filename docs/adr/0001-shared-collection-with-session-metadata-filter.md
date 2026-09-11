# 0001 — One shared Chroma collection, session isolation by metadata filter

- Status: Accepted (retroactive — documents the design the code already implements)
- Date: 2026-09-11

## Context

`CONTEXT.md` originally described a session as owning one Chroma collection per session (`<sessionId>` as the collection name). The implementation does not do that: `ChromaService` get-or-creates a single collection (`Rag.CollectionName`, default `vault_collection`) at startup of each request's `InitializeAsync`, stamps every stored record with a `session_id` metadata key, and scopes queries and deletes with a Chroma `where` filter on that key.

## Decision

All sessions live in one shared Chroma collection. Session isolation is enforced purely by metadata:

- Add: every record carries `session_id` (`ChromaService.AddSessionRecordsAsync`).
- Query: `where = { "session_id": <sessionId> }` (`QuerySessionAsync`).
- Delete: same filter (`TerminateSessionAsync`).

## Consequences

- **Simplicity**: no collection lifecycle management; a new session costs zero Chroma setup beyond the one shared collection. The get/create race is already handled by the 409-fallback in `CreateCollectionAsync`.
- **Cost of session cleanup scales with collection size**: `TerminateSessionAsync` is a filtered delete over the whole collection, not a collection drop. Fine at current scale; revisit if vault volume grows large.
- **No per-session index tuning**: HNSW parameters (`hnsw:space=cosine`) are shared by all sessions.
- **Blast radius**: a bug that forgets the `session_id` filter (in add, query, or delete) leaks or destroys data across sessions. Any new Chroma operation must carry the filter; review with that lens.

## Alternatives considered

- **Collection per session** (`<sessionId>` as name): trivially safe deletes (`delete_collection`), no cross-session leak risk; but unbounded collection creation from an unauthenticated endpoint, and Chroma collection churn on the sweeper path.
- **Prefix-partitioned ids only** (encode session in the chunk id, filter client-side): rejected — filtering on metadata is native to Chroma and keeps ids stable for dedup.
