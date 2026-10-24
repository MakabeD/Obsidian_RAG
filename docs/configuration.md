# Configuration reference

All runtime knobs live under the `Rag` section of `appsettings.json` and bind to
`configuration/RagOptions.cs`. Options are validated with data annotations and
`ValidateOnStart()`: an invalid value (outside its range, or a missing required
string) fails startup immediately instead of surfacing as a bad request later.

`appsettings.json` lists every option with its default value. Two tests keep the
three surfaces in sync — if you add or rename an option, they fail until
`RagOptions`, `appsettings.json`, and this document all agree:

- `AppsettingsParityTests.Every_RagOption_is_listed_in_appsettings`
- `ConfigurationDocsTests.Every_RagOption_is_documented_in_docs_configuration`

## Model and embedding

| Option | Default | Valid range | Effect |
| --- | --- | --- | --- |
| `ModelPath` | `model/model.onnx` | required (non-empty) | Path to the ONNX embedding model. The file is gitignored; download it as described in the README (pinned Hugging Face commit + checksum). |
| `VocabPath` | `model/vocab.txt` | required (non-empty) | Path to the tokenizer vocabulary used by the embedding model. |
| `MaxTokenLength` | `512` | 1 – 100,000 | Maximum tokens per embedding pass; longer inputs are truncated at tokenization. |
| `EmbedBatchSize` | `32` | 1 – 256 | Rows per embedding batch. Batches are internally length-bucketed so padding waste stays low on mixed-length vaults. |

## Chroma client

| Option | Default | Valid range | Effect |
| --- | --- | --- | --- |
| `ChromaBaseUrl` | `http://127.0.0.1:8000` | required (non-empty) | Chroma HTTP base address. Keep it bound to localhost — see ADR-0001 and the docker compose defaults. |
| `ChromaTimeoutSeconds` | `30` | 1 – 600 | Timeout applied to every outbound Chroma request. |
| `ChromaTenant` | `default_tenant` | required (non-empty) | Tenant segment of the Chroma v2 API path. |
| `ChromaDatabase` | `default_database` | required (non-empty) | Database segment of the Chroma v2 API path. |
| `CollectionName` | `vault_collection` | required (non-empty) | Collection used for vault vectors; created on first initialization if missing. |
| `ChromaAddBatchSize` | `500` | 1 – 100,000 | Documents per `/add` POST to Chroma. |
| `HealthCheckTimeoutMs` | `2000` | 1 – int.MaxValue | Request timeout used by the Chroma readiness check (`/healthz/ready`). |

## Chunking and retrieval

| Option | Default | Valid range | Effect |
| --- | --- | --- | --- |
| `ChunkThreshold` | `600` | 1 – 1,000,000 | Target chunk size in characters; the chunker splits (and merges small tails) around it. |
| `DefaultTopK` | `5` | 1 – 10,000 | `topK` used when a query body omits it. |
| `MaxTopK` | `50` | 1 – 10,000 | Maximum accepted `topK`; larger values get a 400 before embedding. |
| `MaxPromptChars` | `4096` | 1 – 100,000 | Maximum `/query` prompt length in characters; longer prompts get a 400 before any embedding runs. |

## Upload limits and abuse guards

| Option | Default | Valid range | Effect |
| --- | --- | --- | --- |
| `MaxUploadBytes` | `26214400` (25 MiB) | 1 – long.MaxValue | Maximum request body size (applied to both Kestrel and the multipart reader). |
| `MaxZipEntryBytes` | `5242880` (5 MiB) | 1 – long.MaxValue | Maximum decompressed size of a single zip entry. |
| `MaxZipTotalUncompressedBytes` | `209715200` (200 MiB) | 1 – long.MaxValue | Cap on the total decompressed size of a vault upload. |
| `MaxZipEntries` | `5000` | 1 – int.MaxValue | Maximum number of entries accepted in one zip. |
| `MaxZipCompressionRatio` | `100` | 1 – 10,000 | Maximum compressed-to-decompressed ratio per entry (zip-bomb guard). |
| `MaxChunkCount` | `20000` | 1 – 1,000,000 | Uploads producing more chunks get a 400 before embedding starts. |
| `MaxConcurrentUploads` | `4` | 1 – 1,000 | Concurrent `/md` uploads in flight; excess requests get an immediate 503 (no queueing). |
| `MaxFileNameLength` | `260` | 1 – 65,535 | Truncation cap for source file names; bounds chunk IDs and Chroma metadata. |

## Sessions and diagnostics

| Option | Default | Valid range | Effect |
| --- | --- | --- | --- |
| `SessionTtlMinutes` | `10` | 1 – int.MaxValue | Idle TTL; a session is swept (and its Chroma vectors deleted) after this. |
| `MaxConcurrentSessions` | `1000` | 1 – int.MaxValue | Live-session registry cap; new sessions above it get a 503. |
| `SweepIntervalSeconds` | `30` | 1 – int.MaxValue | Period of the background sweeper (effective minimum of 5 seconds). |
| `RecentRequestCapacity` | `100` | 1 – 10,000 | Ring-buffer capacity behind `/diagnostics/requests` (session ids are redacted). |

## Interactive API documentation

The service serves its OpenAPI contract at `GET /openapi/v1.json` and a
navigable Scalar UI at `GET /scalar/v1` (the UI page loads its web assets from
Scalar's CDN, so it needs internet access; the JSON contract itself is fully
local). Both are unauthenticated like the rest of the API — run the service
locally only, as ADR-0001 requires.
