# AI & App Guru

## Providers

AppBender talks to **OpenAI, Anthropic, Gemini, and Ollama** through one Semantic Kernel client
(all four expose OpenAI-compatible chat endpoints, so function calling works everywhere).
Configure in `appsettings.json`:

```jsonc
"AI": {
  "DefaultProvider": "openai",
  "AssistantName": "App Guru",
  "SystemPrompt": "You are App Guru...",   // persona
  "Temperature": 0.7,
  "MaxTokens": 4000,
  "Providers": {
    "openai":    { "ApiKey": "sk-...", "Model": "gpt-4o-mini",
                   "EmbeddingModel": "text-embedding-3-small",
                   "ImageModel": "dall-e-3", "AudioModel": "whisper-1" },
    "anthropic": { "ApiKey": "sk-ant-...", "Model": "claude-sonnet-5",
                   "Endpoint": "https://api.anthropic.com/v1" },
    "gemini":    { "ApiKey": "...", "Model": "gemini-2.0-flash",
                   "Endpoint": "https://generativelanguage.googleapis.com/v1beta/openai" },
    "ollama":    { "Model": "llama3.2", "Endpoint": "http://localhost:11434/v1" }
  }
}
```

- `EmbeddingModel` powers RAG vector search (keyword fallback if unset).
- `ImageModel` / `AudioModel` power image generation and transcription (OpenAI-style endpoints).
- Per-chat-session provider/model overrides are available in the chat header.

## App Guru chat (`/chat`)

- **Multi-session**: create, switch, rename (auto-titled from the first message), delete, reset.
- **Attachments**: images become multimodal image content; documents are uploaded and linked
  into the message.
- **Markdown rendering**: tables, fenced code, task lists, images/video/audio links — rendered
  to HTML with Markdig (raw HTML disabled for safety).
- **Kernel tools** (function calling): `calculate`, `current_datetime`, `date_diff_days`,
  `search_internet` (Tavily), `scrape_url`, `list_datasets`, `query_dataset`.
- **Sample prompts** are shown on an empty chat (create form / workflow / dataset, analyze data,
  calculator, web search).
- Token usage is tracked per message and aggregated on the Analytics dashboard.

## AI Studio (`/ai-studio`)

| Tab | What it does |
|---|---|
| 🪄 Schema Generator | Description → entities (fields, options, relations) created in Data Hub |
| 🏗️ Prompt-to-App | Description → schema + one form per entity + starter workflow + published-ready app |
| ⚡ Workflow Assistant | Description → full step tree using the real action catalog; save & open in designer |
| 📊 Dashboard Generator | AI picks 3–5 chart widgets for a dataset; rendered immediately |
| 🧮 Model Builder | Train models on entity records (native, no extra deps): **linear regression** (numbers, R²), **logistic regression** (binary classification — accuracy/precision/recall), **kNN** (multi-class classification, leave-one-out accuracy), and a **collaborative-filtering recommender** (features = user field + item field, optional rating; predict `{"user": "...", "top": 5}` → ranked items) |
| 📚 Knowledge Base (RAG) | Upload **PDF, DOCX, XLSX, CSV, TXT, MD, HTML** → chunk → embed → ask questions with cited sources |
| 🧪 AI Testing | Generates a markdown test-case table for any form or workflow definition |
| 🚢 Deployment Advisor | Feeds real usage stats to the LLM for scaling/DB/storage recommendations |

## AI workflow actions

`ai_chat`, `ai_summarize`, `ai_extract` (JSON), `ai_classify`, `ai_generate_code`,
`ai_vision` (computer vision), `ai_ocr`, `ai_generate_image`, `ai_transcribe`,
`ai_search_internet`, `ai_scrape`, `ai_rag_query` — see [workflows.md](workflows.md).

## Notes

- Document text extraction is dependency-light: PDF via PdfPig; DOCX/XLSX parsed straight from
  their OpenXML zip parts.
- If no provider is configured, AI features fail gracefully with a clear message; everything
  else in the platform keeps working.
