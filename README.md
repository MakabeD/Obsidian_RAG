# Obsidian RAG

A local Retrieval-Augmented Generation (RAG) service for Obsidian vaults. It ingests Markdown files or vault `.zip` uploads, chunks them, embeds them locally with `all-MiniLM-L6-v2` (ONNX Runtime — no cloud, no API keys), and stores the vectors in a Chroma database for semantic search.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://docs.docker.com/get-docker/) with Docker Compose (for the Chroma server)
- The embedding model file, downloaded once (~90 MB, step 2 below)

## Getting started

1. **Start the Chroma server** (binds to `127.0.0.1:8000`):

   ```bash
   docker compose -f chromadb/docker-compose.yml up -d
   ```

2. **Download the embedding model** into `model/`:

   ```bash
   mkdir -p model
   curl -L -o model/model.onnx \
     https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/1110a243fdf4706b3f48f1d95db1a4f5529b4d41/onnx/model.onnx
   sha256sum -c - <<'EOF'
   6fd5d72fe4589f189f8ebc006442dbb529bb7ce38f8082112682524616046452  model/model.onnx
   EOF
   ```

   The URL is pinned to a specific Hugging Face commit and the checksum verifies the downloaded bytes — `sha256sum -c` must report `OK` before running the API.

   `model/vocab.txt` ships with the repo. `model.onnx` is not tracked by Git — this one-time download is all you need.

   > **Using a different model?** The one above is the model we use and test with, but any BERT-family model works as long as it matches the pipeline's contract: a WordPiece tokenizer with its own `vocab.txt` (with the standard BERT token IDs, `[CLS]` = 101 and `[SEP]` = 102), and a raw BERT-style ONNX export — inputs `input_ids`, `attention_mask`, `token_type_ids`; output is token-level embeddings of shape `[batch, seq, hidden]` (no pooling head included). Drop the pair as `model/model.onnx` + `model/vocab.txt` and everything works the same.

3. **Run the API**:

   ```bash
   dotnet run
   ```

   The service listens at `http://localhost:5011`.

## Quick API tour

Create a session:

```bash
curl -X POST http://localhost:5011/session
```

Upload documents (`.md` files or an Obsidian vault `.zip`) into the session:

```bash
curl -F "files=@my-note.md" http://localhost:5011/session/<sessionId>/md
```

Ask a semantic query:

```bash
curl -X POST http://localhost:5011/session/<sessionId>/query \
  -H "Content-Type: application/json" \
  -d '{"prompt": "what did I write about X?", "topK": 5}'
```

Delete the session when done:

```bash
curl -X DELETE http://localhost:5011/session/<sessionId>
```

Health endpoints: `GET /healthz/live` and `GET /healthz/ready`. A log of recent requests is available at `GET /diagnostics/requests`.
