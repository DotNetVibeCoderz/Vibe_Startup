# Configuration Reference

All configuration lives in `src/BlazorViz/appsettings.json` (reloaded on change for `Ai` options).
Secrets should go to user-secrets or environment variables — nested keys use `__`
(e.g. `Ai__Providers__OpenAI__ApiKey`).

## Ai section

```jsonc
"Ai": {
  "Provider": "Ollama",              // OpenAI | Anthropic | Gemini | Ollama
  "AssistantName": "Data Wizard",
  "SystemPrompt": "…persona…",       // system prompt / persona
  "Temperature": 0.7,
  "MaxTokens": 2048,
  "Providers": {
    "OpenAI":    { "ApiKey": "", "Model": "gpt-4o-mini", "Endpoint": null },
    "Anthropic": { "ApiKey": "", "Model": "claude-sonnet-5", "Endpoint": "https://api.anthropic.com/v1" },
    "Gemini":    { "ApiKey": "", "Model": "gemini-2.0-flash" },
    "Ollama":    { "Model": "llama3.1", "Endpoint": "http://localhost:11434" }
  },
  "Tavily": {
    "ApiKey": "",                    // required for the Data Wizard's internet search (https://tavily.com)
    "Endpoint": "https://api.tavily.com/search",
    "SearchDepth": "basic",          // basic | advanced
    "MaxResults": 5
  },
  "Embeddings": {
    "Provider": "Local",             // Local (offline) | OpenAI | Ollama
    "Model": "text-embedding-3-small",
    "ApiKey": "",
    "Endpoint": null,                // custom OpenAI-compatible endpoint
    "Dimensions": 384
  },
  "Rag": {
    "VectorStore": "InMemory",       // InMemory | Qdrant | Chroma | AzureAISearch
    "Collection": "blazorviz-docs",
    "TopK": 4,
    "ChunkSize": 1200,
    "ChunkOverlap": 200,
    "QdrantEndpoint": "http://localhost:6333",
    "QdrantApiKey": "",
    "ChromaEndpoint": "http://localhost:8000",
    "AzureSearchEndpoint": "",       // https://<name>.search.windows.net
    "AzureSearchApiKey": ""
  }
}
```

Notes:

- **Anthropic** is accessed through its OpenAI-compatible endpoint using the OpenAI connector; set the
  `Anthropic.ApiKey` and pick a model id.
- **Ollama** needs no key — install Ollama and `ollama pull llama3.1` (any tool-calling model works best).
- **Tavily** powers the Data Wizard's `search_internet` tool; without `Ai:Tavily:ApiKey` the tool politely
  reports that search is not configured (everything else keeps working).
- Any OpenAI-compatible gateway (LM Studio, vLLM, LiteLLM…) works by setting `OpenAI.Endpoint`.
- The **Local** embedder is a deterministic hashing embedder: zero-setup and offline, adequate for
  keyword-ish retrieval. Use OpenAI/Ollama embeddings for semantic quality; re-index documents after switching.
- The **InMemory** vector store persists to `App_Data/vector-index.json`. Qdrant/Chroma/Azure AI Search are
  contacted via REST — point the endpoints at your instances.

## Storage section

```jsonc
"Storage": {
  "Provider": "FileSystem",          // FileSystem | AzureBlob | S3 (S3 also = MinIO)
  "Root": "App_Data/files",          // FileSystem root (relative to content root)
  "AzureConnectionString": "",
  "AzureContainer": "blazorviz",
  "S3AccessKey": "",
  "S3SecretKey": "",
  "S3Bucket": "blazorviz",
  "S3ServiceUrl": "",                // e.g. http://localhost:9000 for MinIO
  "S3Region": "us-east-1"
}
```

Uploaded dataset files and RAG documents are stored through this abstraction.
Chat attachments are always saved under `wwwroot/uploads` so they can be linked in the thread.

## Database

`ConnectionStrings:DefaultConnection` — SQLite by default (`Data/app.db`). Migrations run automatically at
startup (`SeedData.RunAsync`), which also seeds roles, sample users, sample datasets/dashboards, and a demo
API key on first run.

## Other

- **Python scripts** require a `python` executable on PATH.
- **Plugins** load from the `plugins/` folder at startup; reload from Admin → Settings.
- **QuestPDF** runs under the Community license (configured in code).
