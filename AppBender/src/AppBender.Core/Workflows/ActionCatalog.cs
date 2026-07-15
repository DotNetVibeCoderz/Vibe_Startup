namespace AppBender.Core.Workflows;

public record ActionConfigField(string Key, string Label, string Kind = "text", string? Placeholder = null, string[]? Options = null);

public record ActionDescriptor(
    string Type, string Name, string Category, string Icon, string Description,
    ActionConfigField[] ConfigFields);

/// <summary>Metadata for every step type shown in the workflow designer palette.</summary>
public static class ActionCatalog
{
    public static readonly ActionDescriptor[] All =
    [
        // ----- Control flow (handled by the engine itself)
        new("condition", "Condition", "Control", "🔀", "Branch on a comparison (if/else).",
            [new("left", "Left value", "text", "{{trigger.body.total}}"),
             new("op", "Operator", "select", null, ["eq","neq","gt","gte","lt","lte","contains","startswith","empty","notempty"]),
             new("right", "Right value", "text", "100")]),
        new("switch", "Switch", "Control", "🔁", "Branch on a value with multiple cases.",
            [new("on", "Switch on", "text", "{{vars.status}}")]),
        new("foreach", "For Each", "Control", "📚", "Loop over a list; current item = {{vars.item}}.",
            [new("items", "Items", "text", "{{steps.query1.output}}")]),
        new("do_until", "Do Until", "Control", "🔂", "Repeat children until the condition is true.",
            [new("left", "Left value", "text"), new("op", "Operator", "select", null, ["eq","neq","gt","gte","lt","lte"]),
             new("right", "Right value", "text"), new("maxIterations", "Max iterations", "number", "100")]),
        new("scope", "Scope", "Control", "📦", "Group steps together.", []),
        new("delay", "Delay", "Control", "⏸️", "Wait for N seconds.",
            [new("seconds", "Seconds", "number", "5")]),
        new("terminate", "Terminate", "Control", "🛑", "Stop the workflow.",
            [new("status", "Status", "select", null, ["succeeded","failed","cancelled"])]),

        // ----- Data
        new("data_query", "Query Records", "Data Hub", "🗃️", "Query Data Hub records.",
            [new("entity", "Entity", "entity"), new("filter", "Filter", "text", "status eq Active"),
             new("search", "Search", "text"), new("sortBy", "Sort by", "text"), new("top", "Top", "number", "50")]),
        new("data_get", "Get Record", "Data Hub", "📄", "Get one record by id.",
            [new("entity", "Entity", "entity"), new("id", "Record id", "text", "{{trigger.record.id}}")]),
        new("data_create", "Create Record", "Data Hub", "➕", "Create a Data Hub record.",
            [new("entity", "Entity", "entity"), new("data", "Data (JSON)", "json", "{\"name\": \"{{vars.name}}\"}")]),
        new("data_update", "Update Record", "Data Hub", "✏️", "Update a Data Hub record.",
            [new("entity", "Entity", "entity"), new("id", "Record id", "text"), new("data", "Data (JSON)", "json")]),
        new("data_delete", "Delete Record", "Data Hub", "🗑️", "Delete a Data Hub record.",
            [new("entity", "Entity", "entity"), new("id", "Record id", "text")]),

        // ----- Integration
        new("http_request", "HTTP Request", "Integration", "🌐", "Call any REST endpoint.",
            [new("method", "Method", "select", null, ["GET","POST","PUT","PATCH","DELETE"]),
             new("url", "URL", "text", "https://api.example.com/items"),
             new("headers", "Headers (JSON)", "json", "{\"Authorization\": \"Bearer ...\"}"),
             new("body", "Body", "json")]),
        new("connector_action", "Connector Action", "Integration", "🔌", "Run an action from the connector library.",
            [new("connectorId", "Connector", "connector"), new("action", "Action key", "text"),
             new("input", "Input (JSON)", "json", "{}")]),
        new("send_email", "Send Email", "Integration", "📧", "Send an HTML email via SMTP.",
            [new("to", "To", "text"), new("cc", "Cc", "text"), new("subject", "Subject", "text"),
             new("body", "Body (HTML)", "textarea")]),
        new("respond", "Respond to Webhook", "Integration", "↩️", "Set the HTTP response for webhook triggers.",
            [new("body", "Body (JSON)", "json", "{\"ok\": true}")]),
        new("call_workflow", "Call Workflow", "Integration", "⚡", "Run another workflow and use its output.",
            [new("workflowId", "Workflow", "workflow"), new("input", "Input (JSON)", "json", "{}")]),

        // ----- Variables & expressions
        new("set_variable", "Set Variable", "Variables", "🧮", "Store a value at {{vars.name}}.",
            [new("name", "Name", "text", "total"), new("value", "Value", "text", "{{steps.http1.output.body.total}}")]),
        new("compose", "Compose", "Variables", "🧩", "Build a value/object from templates.",
            [new("value", "Value (JSON or text)", "json")]),
        new("math", "Math Expression", "Variables", "➗", "Evaluate arithmetic (supports functions).",
            [new("expression", "Expression", "text", "round({{vars.subtotal}} * 1.11, 2)")]),
        new("transform", "Transform (Map)", "Variables", "🗺️", "Build an object; each property value is a template.",
            [new("mapping", "Mapping (JSON)", "json", "{\"fullName\": \"{{vars.first}} {{vars.last}}\"}")]),
        new("log", "Log Message", "Variables", "📝", "Write a message to the run log.",
            [new("message", "Message", "text")]),

        // ----- Code
        new("run_csharp", "Run C#", "Code", "🟣", "Run a C# script (Context dictionary available).",
            [new("code", "Code", "code", "return Context[\"vars\"];")]),
        new("run_javascript", "Run JavaScript", "Code", "🟡", "Run JS with Jint (context object available).",
            [new("code", "Code", "code", "context.vars")]),
        new("run_python", "Run Python", "Code", "🐍", "Run Python (requires python on PATH).",
            [new("code", "Code", "code", "result = context[\"vars\"]")]),

        // ----- AI
        new("ai_chat", "Ask AI (LLM)", "AI", "🤖", "Send a prompt to the configured LLM.",
            [new("prompt", "Prompt", "textarea"), new("system", "System prompt", "textarea"),
             new("provider", "Provider", "text"), new("model", "Model", "text")]),
        new("ai_summarize", "AI Summarize", "AI", "📋", "Summarize text.",
            [new("text", "Text", "textarea"), new("length", "Length", "select", null, ["short","medium","long"])]),
        new("ai_extract", "AI Extract (JSON)", "AI", "🔎", "Extract structured JSON from text.",
            [new("text", "Text", "textarea"), new("schema", "Fields to extract", "text", "name, email, amount")]),
        new("ai_classify", "AI Classify", "AI", "🏷️", "Classify text into one of the given categories.",
            [new("text", "Text", "textarea"), new("categories", "Categories (comma separated)", "text", "positive, neutral, negative")]),
        new("ai_generate_code", "AI Generate Code", "AI", "💻", "Generate code from a description.",
            [new("description", "Description", "textarea"), new("language", "Language", "text", "csharp")]),
        new("ai_vision", "AI Vision / Describe Image", "AI", "👁️", "Analyze an image with a vision model.",
            [new("imageUrl", "Image URL", "text"), new("prompt", "Question", "text", "Describe this image")]),
        new("ai_ocr", "AI OCR (Image to Text)", "AI", "🔤", "Extract all text from an image.",
            [new("imageUrl", "Image URL", "text")]),
        new("ai_generate_image", "AI Generate Image", "AI", "🎨", "Generate an image from a prompt.",
            [new("prompt", "Prompt", "textarea"), new("size", "Size", "select", null, ["1024x1024","512x512","1792x1024"])]),
        new("ai_transcribe", "AI Transcribe Audio", "AI", "🎙️", "Speech-to-text for an audio file.",
            [new("audioUrl", "Audio URL or path", "text")]),
        new("ai_search_internet", "Search Internet", "AI", "🔍", "Web search via Tavily.",
            [new("query", "Query", "text"), new("maxResults", "Max results", "number", "5")]),
        new("ai_scrape", "Scrape URL", "AI", "🕸️", "Fetch a web page as readable text.",
            [new("url", "URL", "text")]),
        new("ai_rag_query", "Knowledge Base Query (RAG)", "AI", "📚", "Answer a question from indexed documents.",
            [new("question", "Question", "textarea")]),

        // ----- Files
        new("file_write", "Write File", "Files", "💾", "Write text to storage.",
            [new("path", "Path", "text", "exports/report.txt"), new("content", "Content", "textarea")]),
        new("file_read", "Read File", "Files", "📖", "Read text from storage.",
            [new("path", "Path", "text")]),
    ];

    public static ActionDescriptor? Find(string type) => All.FirstOrDefault(a => a.Type == type);

    public static readonly string[] ContainerTypes = ["condition", "switch", "foreach", "do_until", "scope"];
}
