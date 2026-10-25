# syntax=docker/dockerfile:1

# Build stage (digest-pinned; was mcr.microsoft.com/dotnet/sdk:10.0, resolved 2026-10-26)
FROM mcr.microsoft.com/dotnet/sdk@sha256:afd0ccff9d306812a69ef0f47458376b6acb544ae866cfc4b8d28ff0eef35ea7 AS build
WORKDIR /src
COPY obsidian_RAG.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish obsidian_RAG.csproj -c Release -o /app --no-restore

# Model stage: downloads the embedding model from the pinned Hugging Face commit
# and verifies its checksum (same pin as .github/workflows/ci.yml and the README)
FROM mcr.microsoft.com/dotnet/sdk@sha256:afd0ccff9d306812a69ef0f47458376b6acb544ae866cfc4b8d28ff0eef35ea7 AS model
ARG MODEL_SHA256=6fd5d72fe4589f189f8ebc006442dbb529bb7ce38f8082112682524616046452
RUN curl -fsSL \
        "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/1110a243fdf4706b3f48f1d95db1a4f5529b4d41/onnx/model.onnx" \
        -o /model.onnx \
    && echo "${MODEL_SHA256}  /model.onnx" | sha256sum -c -

# Final stage (digest-pinned; was mcr.microsoft.com/dotnet/aspnet:10.0, resolved 2026-10-26)
FROM mcr.microsoft.com/dotnet/aspnet@sha256:d247d17dd429d96580eeb96cfce220c1089d860dbbf9f94d599ca64cf3604fe4 AS final
WORKDIR /app
COPY --from=build /app .
COPY model/vocab.txt /app/model/
COPY --from=model /model.onnx /app/model/model.onnx
EXPOSE 8080
USER app
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080; printf 'GET /healthz/live HTTP/1.0\r\n\r\n' >&3; head -n1 <&3 | grep -q ' 200 '"]
ENTRYPOINT ["dotnet", "obsidian_RAG.dll"]
