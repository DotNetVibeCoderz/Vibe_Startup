using AppBender.Core.Connectors;
using AppBender.Core.Data;
using AppBender.Core.Models;
using AppBender.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppBender.Web.Services;

/// <summary>Seeds roles, sample users, demo entities/records, forms, workflows, connectors and snippets.</summary>
public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var tenant = services.GetRequiredService<ITenantContext>();
        tenant.Set(TenantContext.DefaultTenant, null, "seed");

        var dbFactory = services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        bool firstRun;
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            firstRun = !await db.Organizations.AnyAsync();
        }

        if (firstRun)
        {
            await SeedIdentityAsync(services);
            await SeedDataHubAsync(services);
            await SeedFormsAndWorkflowsAsync(services);
            await SeedConnectorsAsync(services);
            await SeedAppAsync(services);
        }

        // top-up seeds: added in later versions, checked per item so existing databases get them too
        await SeedSnippetsAsync(services);
        await SeedShowcaseFormsAsync(services);
    }

    private static async Task SeedIdentityAsync(IServiceProvider services)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.Organizations.Add(new Organization
            {
                Id = TenantContext.DefaultTenant,
                Name = "Acme Demo Organization",
                Slug = "acme"
            });
            await db.SaveChangesAsync();
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        async Task CreateUser(string email, string password, string displayName, string jobTitle, string role)
        {
            if (await userManager.FindByEmailAsync(email) is not null) return;
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                JobTitle = jobTitle,
                OrganizationId = TenantContext.DefaultTenant
            };
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded) await userManager.AddToRoleAsync(user, role);
        }

        await CreateUser("admin@appbender.io", "Admin123!", "Alex Admin", "Platform Administrator", AppRoles.Admin);
        await CreateUser("dev@appbender.io", "Dev123!", "Dina Developer", "Citizen Developer", AppRoles.Developer);
        await CreateUser("user@appbender.io", "User123!", "Udin User", "Sales Staff", AppRoles.EndUser);
    }

    private static async Task SeedDataHubAsync(IServiceProvider services)
    {
        var dataHub = services.GetRequiredService<IDataHubService>();

        var customers = await dataHub.SaveEntityAsync(new EntityDefinition
        {
            Name = "customers",
            DisplayName = "Customers",
            Description = "Pelanggan / customer master data",
            Icon = "👥",
            Fields =
            [
                new() { Name = "customer_no", DisplayName = "Customer No", Type = FieldType.AutoNumber },
                new() { Name = "name", DisplayName = "Name", Type = FieldType.Text, Required = true },
                new() { Name = "email", DisplayName = "Email", Type = FieldType.Email, Required = true },
                new() { Name = "phone", DisplayName = "Phone", Type = FieldType.Phone },
                new() { Name = "city", DisplayName = "City", Type = FieldType.Text },
                new() { Name = "status", DisplayName = "Status", Type = FieldType.Choice, Options = ["Lead", "Active", "Inactive"], DefaultValue = "Lead" },
                new() { Name = "notes", DisplayName = "Notes", Type = FieldType.LongText },
            ]
        }, "seed");

        await dataHub.SaveEntityAsync(new EntityDefinition
        {
            Name = "products",
            DisplayName = "Products",
            Description = "Katalog produk",
            Icon = "📦",
            Fields =
            [
                new() { Name = "sku", DisplayName = "SKU", Type = FieldType.Text, Required = true, Unique = true },
                new() { Name = "name", DisplayName = "Product Name", Type = FieldType.Text, Required = true },
                new() { Name = "category", DisplayName = "Category", Type = FieldType.Choice, Options = ["Electronics", "Fashion", "Food", "Services"] },
                new() { Name = "price", DisplayName = "Price", Type = FieldType.Currency, Required = true, Min = 0 },
                new() { Name = "stock", DisplayName = "Stock", Type = FieldType.Number, DefaultValue = "0" },
                new() { Name = "active", DisplayName = "Active", Type = FieldType.Boolean, DefaultValue = "true" },
            ]
        }, "seed");

        await dataHub.SaveEntityAsync(new EntityDefinition
        {
            Name = "orders",
            DisplayName = "Orders",
            Description = "Order penjualan; terhubung ke customers via lookup",
            Icon = "🧾",
            Fields =
            [
                new() { Name = "order_no", DisplayName = "Order No", Type = FieldType.AutoNumber },
                new() { Name = "customer_id", DisplayName = "Customer", Type = FieldType.Lookup, LookupEntity = "customers", Required = true },
                new() { Name = "order_date", DisplayName = "Order Date", Type = FieldType.Date, Required = true },
                new() { Name = "qty", DisplayName = "Quantity", Type = FieldType.Number, Required = true, Min = 1 },
                new() { Name = "unit_price", DisplayName = "Unit Price", Type = FieldType.Currency, Required = true },
                new() { Name = "total", DisplayName = "Total", Type = FieldType.Formula, Formula = "qty * unit_price" },
                new() { Name = "status", DisplayName = "Status", Type = FieldType.Choice, Options = ["New", "Paid", "Shipped", "Done", "Cancelled"], DefaultValue = "New" },
            ],
            Relationships =
            [
                new() { Name = "customer_orders", FromEntity = "customers", ToEntity = "orders", LookupField = "customer_id" }
            ]
        }, "seed");

        await dataHub.SaveEntityAsync(new EntityDefinition
        {
            Name = "tasks",
            DisplayName = "Tasks",
            Description = "To-do / tugas tim",
            Icon = "✅",
            Fields =
            [
                new() { Name = "title", DisplayName = "Title", Type = FieldType.Text, Required = true },
                new() { Name = "assignee", DisplayName = "Assignee", Type = FieldType.Text },
                new() { Name = "due_date", DisplayName = "Due Date", Type = FieldType.Date },
                new() { Name = "priority", DisplayName = "Priority", Type = FieldType.Choice, Options = ["Low", "Medium", "High"], DefaultValue = "Medium" },
                new() { Name = "done", DisplayName = "Done", Type = FieldType.Boolean, DefaultValue = "false" },
            ]
        }, "seed");

        // sample records
        string[][] customerRows =
        [
            ["Budi Santoso", "budi@example.com", "0812-1111-2222", "Jakarta", "Active"],
            ["Siti Rahma", "siti@example.com", "0813-3333-4444", "Bandung", "Active"],
            ["John Miller", "john@example.com", "0815-5555-6666", "Surabaya", "Lead"],
            ["Ayu Lestari", "ayu@example.com", "0817-7777-8888", "Yogyakarta", "Active"],
            ["Rudi Hartono", "rudi@example.com", "0819-9999-0000", "Medan", "Inactive"],
        ];
        var customerIds = new List<string>();
        foreach (var row in customerRows)
        {
            var record = await dataHub.CreateRecordAsync("customers", new Dictionary<string, object?>
            {
                ["name"] = row[0], ["email"] = row[1], ["phone"] = row[2], ["city"] = row[3], ["status"] = row[4]
            });
            customerIds.Add(record.Id);
        }

        object[][] productRows =
        [
            ["SKU-001", "Wireless Mouse", "Electronics", 150000, 120],
            ["SKU-002", "Mechanical Keyboard", "Electronics", 750000, 45],
            ["SKU-003", "Batik Shirt", "Fashion", 250000, 80],
            ["SKU-004", "Arabica Coffee 1kg", "Food", 180000, 200],
            ["SKU-005", "Consulting (1 hour)", "Services", 500000, 999],
        ];
        foreach (var row in productRows)
        {
            await dataHub.CreateRecordAsync("products", new Dictionary<string, object?>
            {
                ["sku"] = row[0], ["name"] = row[1], ["category"] = row[2], ["price"] = row[3], ["stock"] = row[4]
            });
        }

        var random = new Random(42);
        for (var i = 0; i < 12; i++)
        {
            await dataHub.CreateRecordAsync("orders", new Dictionary<string, object?>
            {
                ["customer_id"] = customerIds[random.Next(customerIds.Count)],
                ["order_date"] = DateTime.Today.AddDays(-random.Next(1, 60)).ToString("yyyy-MM-dd"),
                ["qty"] = random.Next(1, 10),
                ["unit_price"] = new[] { 150000, 250000, 500000, 750000 }[random.Next(4)],
                ["status"] = new[] { "New", "Paid", "Shipped", "Done" }[random.Next(4)]
            });
        }

        string[][] taskRows =
        [
            ["Follow up lead John Miller", "Dina Developer", "High"],
            ["Prepare monthly sales report", "Alex Admin", "Medium"],
            ["Restock Arabica Coffee", "Udin User", "Low"],
        ];
        foreach (var row in taskRows)
        {
            await dataHub.CreateRecordAsync("tasks", new Dictionary<string, object?>
            {
                ["title"] = row[0], ["assignee"] = row[1], ["priority"] = row[2],
                ["due_date"] = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd")
            });
        }
    }

    private static async Task SeedFormsAndWorkflowsAsync(IServiceProvider services)
    {
        var forms = services.GetRequiredService<IFormService>();
        var workflows = services.GetRequiredService<IWorkflowService>();

        await forms.SaveAsync(new FormDefinition
        {
            Name = "Customer Registration",
            Slug = "customer_registration",
            Description = "Form pendaftaran pelanggan baru",
            Icon = "👥",
            EntityName = "customers",
            IsPublished = true,
            SubmitLabel = "Register",
            Layout =
            [
                new() { Type = ComponentTypes.Heading, Label = "Customer Registration", Width = 12 },
                new() { Type = ComponentTypes.Paragraph, Label = "Daftarkan pelanggan baru. Field bertanda * wajib diisi.", Width = 12 },
                new() { Type = ComponentTypes.TextBox, Label = "Full Name", Field = "name", Required = true, Width = 6, Placeholder = "e.g. Budi Santoso" },
                new() { Type = ComponentTypes.Email, Label = "Email", Field = "email", Required = true, Width = 6 },
                new() { Type = ComponentTypes.Phone, Label = "Phone", Field = "phone", Width = 6 },
                new() { Type = ComponentTypes.TextBox, Label = "City", Field = "city", Width = 6 },
                new() { Type = ComponentTypes.Dropdown, Label = "Status", Field = "status", Width = 6,
                        Props = new() { ["options"] = "Lead,Active,Inactive" } },
                new() { Type = ComponentTypes.TextArea, Label = "Notes", Field = "notes", Width = 12 },
            ]
        }, "seed");

        await forms.SaveAsync(new FormDefinition
        {
            Name = "Product Entry",
            Slug = "product_entry",
            Description = "Input produk baru ke katalog",
            Icon = "📦",
            EntityName = "products",
            IsPublished = true,
            Layout =
            [
                new() { Type = ComponentTypes.Heading, Label = "Product Entry", Width = 12 },
                new() { Type = ComponentTypes.TextBox, Label = "SKU", Field = "sku", Required = true, Width = 4 },
                new() { Type = ComponentTypes.TextBox, Label = "Product Name", Field = "name", Required = true, Width = 8 },
                new() { Type = ComponentTypes.Dropdown, Label = "Category", Field = "category", Width = 4,
                        Props = new() { ["options"] = "Electronics,Fashion,Food,Services" } },
                new() { Type = ComponentTypes.Number, Label = "Price", Field = "price", Required = true, Width = 4 },
                new() { Type = ComponentTypes.Number, Label = "Stock", Field = "stock", Width = 4 },
                new() { Type = ComponentTypes.Toggle, Label = "Active", Field = "active", Width = 12, DefaultValue = "true" },
            ]
        }, "seed");

        await forms.SaveAsync(new FormDefinition
        {
            Name = "Task Quick Add",
            Slug = "task_quick_add",
            Description = "Tambah tugas cepat",
            Icon = "✅",
            EntityName = "tasks",
            IsPublished = true,
            Layout =
            [
                new() { Type = ComponentTypes.Heading, Label = "New Task", Width = 12 },
                new() { Type = ComponentTypes.TextBox, Label = "Title", Field = "title", Required = true, Width = 12 },
                new() { Type = ComponentTypes.TextBox, Label = "Assignee", Field = "assignee", Width = 6 },
                new() { Type = ComponentTypes.Date, Label = "Due Date", Field = "due_date", Width = 6 },
                new() { Type = ComponentTypes.Radio, Label = "Priority", Field = "priority", Width = 12,
                        Props = new() { ["options"] = "Low,Medium,High" } },
            ]
        }, "seed");

        await workflows.SaveAsync(new WorkflowDefinition
        {
            Name = "Welcome New Customer",
            Description = "Saat customer baru dibuat: log + kirim email selamat datang (jika SMTP dikonfigurasi).",
            Icon = "👋",
            TriggerType = TriggerType.EntityCreated,
            TriggerConfig = new() { ["entityName"] = "customers" },
            Steps =
            [
                new()
                {
                    Type = "log", Name = "Log new customer",
                    Config = new() { ["message"] = "New customer: {{trigger.record.name}} ({{trigger.record.email}})" }
                },
                new()
                {
                    Type = "condition", Name = "Has email?",
                    Config = new() { ["left"] = "{{trigger.record.email}}", ["op"] = "notempty" },
                    TrueSteps =
                    [
                        new()
                        {
                            Type = "send_email", Name = "Send welcome email",
                            Config = new()
                            {
                                ["to"] = "{{trigger.record.email}}",
                                ["subject"] = "Selamat datang, {{trigger.record.name}}!",
                                ["body"] = "<h2>Halo {{trigger.record.name}} 👋</h2><p>Terima kasih telah bergabung bersama kami.</p>"
                            }
                        }
                    ]
                }
            ]
        }, "seed");

        await workflows.SaveAsync(new WorkflowDefinition
        {
            Name = "Daily Order Summary",
            Description = "Setiap hari jam 08:00 UTC: hitung order berstatus New dan tulis ke log run.",
            Icon = "📈",
            TriggerType = TriggerType.Schedule,
            TriggerConfig = new() { ["cron"] = "0 8 * * *" },
            Steps =
            [
                new()
                {
                    Type = "data_query", Name = "Get new orders",
                    Config = new() { ["entity"] = "orders", ["filter"] = "status eq New", ["top"] = "100" }
                },
                new()
                {
                    Type = "run_javascript", Name = "Compute totals",
                    Config = new()
                    {
                        ["code"] = "var orders = context.steps.get_new_orders.output; " +
                                   "var total = 0; for (var i = 0; i < orders.length; i++) total += Number(orders[i].total || 0); " +
                                   "({ count: orders.length, grandTotal: total })"
                    }
                },
                new()
                {
                    Type = "log", Name = "Log summary",
                    Config = new() { ["message"] = "New orders: {{steps.compute_totals.output.count}}, total: {{steps.compute_totals.output.grandTotal}}" }
                }
            ]
        }, "seed");

        await workflows.SaveAsync(new WorkflowDefinition
        {
            Name = "Order Intake Webhook",
            Description = "POST /api/webhooks/order-intake → membuat order baru dan merespon dengan id.",
            Icon = "🪝",
            TriggerType = TriggerType.Webhook,
            TriggerConfig = new() { ["webhookKey"] = "order-intake" },
            Steps =
            [
                new()
                {
                    Type = "data_create", Name = "Create order",
                    Config = new()
                    {
                        ["entity"] = "orders",
                        ["data"] = """{"customer_id": "{{trigger.body.customerId}}", "order_date": "{{today}}", "qty": "{{trigger.body.qty}}", "unit_price": "{{trigger.body.unitPrice}}"}"""
                    }
                },
                new()
                {
                    Type = "respond", Name = "Respond",
                    Config = new() { ["body"] = """{"ok": true, "orderId": "{{steps.create_order.output.id}}"}""" }
                }
            ]
        }, "seed");

        await workflows.SaveAsync(new WorkflowDefinition
        {
            Name = "AI Lead Summarizer",
            Description = "Manual: ringkas semua customer berstatus Lead memakai LLM (perlu API key AI).",
            Icon = "🤖",
            TriggerType = TriggerType.Manual,
            Steps =
            [
                new()
                {
                    Type = "data_query", Name = "Get leads",
                    Config = new() { ["entity"] = "customers", ["filter"] = "status eq Lead" }
                },
                new()
                {
                    Type = "ai_summarize", Name = "Summarize leads",
                    Config = new() { ["text"] = "{{steps.get_leads.output}}", ["length"] = "short" }
                },
                new()
                {
                    Type = "log", Name = "Log summary",
                    Config = new() { ["message"] = "{{steps.summarize_leads.output}}" }
                }
            ]
        }, "seed");
    }

    private static async Task SeedConnectorsAsync(IServiceProvider services)
    {
        var connectors = services.GetRequiredService<IConnectorRuntime>();

        await connectors.SaveDefinitionAsync(new ConnectorDefinition
        {
            Name = "JSONPlaceholder API",
            Provider = "rest",
            Category = "API",
            Icon = "🧪",
            Description = "Public demo REST API — coba action get dengan path /todos/1",
            Config = new() { ["baseUrl"] = "https://jsonplaceholder.typicode.com" }
        });

        await connectors.SaveDefinitionAsync(new ConnectorDefinition
        {
            Name = "Local SQLite (AppBender DB)",
            Provider = "sqlite",
            Category = "Database",
            Icon = "🗄️",
            Description = "Contoh koneksi SQL langsung ke database AppBender sendiri",
            Config = new() { ["connectionString"] = "DataSource=appbender.db;Cache=Shared" }
        });

        // ----- Custom Connector Builder examples (JSON-defined)
        await connectors.SaveDefinitionAsync(new ConnectorDefinition
        {
            Name = "Open-Meteo Weather",
            Provider = "custom",
            Category = "Custom",
            Icon = "🌦️",
            IsCustom = true,
            Description = "Contoh custom connector tanpa API key: cuaca via open-meteo.com",
            Spec = new CustomConnectorSpec
            {
                BaseUrl = "https://api.open-meteo.com",
                AuthType = "none",
                Actions =
                [
                    new()
                    {
                        Key = "current_weather",
                        Name = "Get current weather",
                        Description = "Input: latitude, longitude",
                        Method = "GET",
                        PathTemplate = "/v1/forecast?latitude={{latitude}}&longitude={{longitude}}&current_weather=true",
                        Inputs = ["latitude", "longitude"]
                    }
                ]
            }
        });

        await connectors.SaveDefinitionAsync(new ConnectorDefinition
        {
            Name = "GitHub API (Custom)",
            Provider = "custom",
            Category = "Custom",
            Icon = "🐙",
            IsCustom = true,
            Description = "Contoh custom connector dengan bearer auth (isi apiKey dengan token GitHub)",
            Spec = new CustomConnectorSpec
            {
                BaseUrl = "https://api.github.com",
                AuthType = "bearer",
                DefaultHeaders = new() { ["User-Agent"] = "AppBender", ["Accept"] = "application/vnd.github+json" },
                Actions =
                [
                    new()
                    {
                        Key = "get_repo",
                        Name = "Get repository info",
                        Method = "GET",
                        PathTemplate = "/repos/{{owner}}/{{repo}}",
                        Inputs = ["owner", "repo"]
                    },
                    new()
                    {
                        Key = "create_issue",
                        Name = "Create issue",
                        Method = "POST",
                        PathTemplate = "/repos/{{owner}}/{{repo}}/issues",
                        BodyTemplate = """{"title": "{{title}}", "body": "{{body}}"}""",
                        Inputs = ["owner", "repo", "title", "body"]
                    }
                ]
            }
        });
    }

    private static async Task SeedSnippetsAsync(IServiceProvider services)
    {
        var snippets = services.GetRequiredService<ISnippetService>();
        var existingTitles = (await snippets.GetAllAsync()).Select(s => s.Title).ToHashSet();
        (string Title, string Lang, string Category, string Description, string Code)[] items =
        [
            ("Call Data API (C#)", "csharp", "API Calls", "Query Data Hub records via REST API",
             """
             using var http = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };
             http.DefaultRequestHeaders.Add("X-Api-Key", "YOUR_API_KEY");
             var json = await http.GetStringAsync("/api/data/customers?filter=status eq Active&pageSize=10");
             Console.WriteLine(json);
             """),
            ("Call Data API (JavaScript)", "javascript", "API Calls", "Fetch Data Hub records from the browser",
             """
             const res = await fetch('/api/data/customers?search=budi', {
               headers: { 'X-Api-Key': 'YOUR_API_KEY' }
             });
             const data = await res.json();
             console.log(data.records);
             """),
            ("GraphQL query (JavaScript)", "javascript", "API Calls", "Query via the GraphQL endpoint",
             """
             const res = await fetch('/api/graphql', {
               method: 'POST',
               headers: { 'Content-Type': 'application/json', 'X-Api-Key': 'YOUR_API_KEY' },
               body: JSON.stringify({ query: '{ customers(top: 5) { id name email } }' })
             });
             console.log((await res.json()).data.customers);
             """),
            ("Trigger webhook workflow (curl)", "json", "Workflows", "Fire the seeded order-intake webhook",
             """
             curl -X POST https://localhost:5001/api/webhooks/order-intake \
               -H "Content-Type: application/json" \
               -d '{"customerId": "CUSTOMER_ID", "qty": 2, "unitPrice": 150000}'
             """),
            ("Validate email (C# workflow script)", "csharp", "Validation", "Use inside a Run C# step",
             """
             var email = Context["trigger"] is IDictionary<string, object?> t
                 && t["record"] is IDictionary<string, object?> r ? r["email"]?.ToString() : null;
             return !string.IsNullOrEmpty(email) && email.Contains('@');
             """),
            ("Loop with index (JS workflow script)", "javascript", "Looping", "Use inside a Run JavaScript step",
             """
             var items = context.steps.get_new_orders.output;
             var lines = [];
             for (var i = 0; i < items.length; i++) lines.push((i + 1) + '. ' + items[i].order_no);
             lines.join('\n')
             """),
            ("Group & sum (JS workflow script)", "javascript", "Data", "Aggregate records by field",
             """
             var rows = context.steps.query1.output, sums = {};
             for (var i = 0; i < rows.length; i++) {
               var key = rows[i].status || 'Unknown';
               sums[key] = (sums[key] || 0) + Number(rows[i].total || 0);
             }
             (sums)
             """),
            ("Pandas from context (Python)", "python", "Data", "Use inside a Run Python step (needs python + pandas)",
             """
             rows = context["steps"]["query1"]["output"]
             total = sum(float(r.get("total") or 0) for r in rows)
             result = {"count": len(rows), "total": total}
             """),
            ("Formula field examples", "json", "Data Hub", "Formula field expressions for entities",
             """
             qty * unit_price
             round(subtotal * 1.11, 2)
             max(price - discount, 0)
             """),
            ("Retry HTTP with Do-Until", "json", "Workflows", "Pattern: retry an HTTP call until success (paste into workflow JSON)",
             """
             {"type":"do_until","name":"Retry until ok",
              "config":{"left":"{{steps.call_api.output.isSuccess}}","op":"eq","right":"true","maxIterations":"5"},
              "children":[{"type":"http_request","name":"Call API","config":{"method":"GET","url":"https://example.com/health"}},
                          {"type":"delay","name":"Wait","config":{"seconds":"2"}}]}
             """),

            // ---------------- API Calls ----------------
            ("POST JSON with HttpClient (C#)", "csharp", "API Calls", "Create a record via the Data API from C#",
             """
             using var http = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };
             http.DefaultRequestHeaders.Add("X-Api-Key", "YOUR_API_KEY");
             var payload = new { name = "Budi", email = "budi@example.com", status = "Lead" };
             var response = await http.PostAsJsonAsync("/api/data/customers", payload);
             response.EnsureSuccessStatusCode();
             Console.WriteLine(await response.Content.ReadAsStringAsync());
             """),
            ("Upload file to AppBender (C#)", "csharp", "API Calls", "Multipart upload to /api/files/upload",
             """
             using var http = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };
             http.DefaultRequestHeaders.Add("X-Api-Key", "YOUR_API_KEY");
             using var form = new MultipartFormDataContent();
             form.Add(new ByteArrayContent(await File.ReadAllBytesAsync("report.pdf")), "file", "report.pdf");
             var response = await http.PostAsync("/api/files/upload", form);
             Console.WriteLine(await response.Content.ReadAsStringAsync()); // { url, fileName, ... }
             """),
            ("Call Data API from Python", "python", "API Calls", "Query records using requests",
             """
             import requests
             r = requests.get(
                 "https://localhost:5001/api/data/orders",
                 params={"filter": "status eq New", "pageSize": 20},
                 headers={"X-Api-Key": "YOUR_API_KEY"}, verify=False)
             for row in r.json()["records"]:
                 print(row["order_no"], row["total"])
             """),
            ("GraphQL mutation (JavaScript)", "javascript", "API Calls", "Create + update records via GraphQL",
             """
             const query = `mutation {
               create_customers(data: { name: "Jo", email: "jo@x.io" }) { id }
             }`;
             const res = await fetch('/api/graphql', {
               method: 'POST',
               headers: { 'Content-Type': 'application/json', 'X-Api-Key': 'YOUR_API_KEY' },
               body: JSON.stringify({ query })
             });
             console.log(await res.json());
             """),
            ("Export workspace via API (curl)", "json", "API Calls", "Download the full workspace package",
             """
             curl -H "X-Api-Key: YOUR_API_KEY" -o backup.json \
               "https://localhost:5001/api/package/export?includeRecords=true"
             """),

            // ---------------- Validation ----------------
            ("Validate email + phone (JS)", "javascript", "Validation", "Regex validation inside a Run JavaScript step",
             """
             var email = context.trigger.values.email || '';
             var phone = context.trigger.values.phone || '';
             var ok = /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email) &&
                      /^[0-9+\-\s]{8,16}$/.test(phone);
             ({ valid: ok, email: email, phone: phone })
             """),
            ("Validate required fields (C#)", "csharp", "Validation", "Check a set of required keys on trigger values",
             """
             var values = Context["trigger"] is IDictionary<string, object?> t &&
                          t["values"] is IDictionary<string, object?> v ? v : null;
             string[] required = ["name", "email", "city"];
             var missing = required.Where(k => values?[k] is null ||
                 string.IsNullOrWhiteSpace(values[k]?.ToString())).ToList();
             return missing.Count == 0 ? "ok" : $"Missing: {string.Join(", ", missing)}";
             """),
            ("Validate NIK/KTP 16 digit (JS)", "javascript", "Validation", "Indonesian ID number sanity check",
             """
             var nik = String(context.vars.nik || '');
             var valid = /^\d{16}$/.test(nik);
             ({ valid: valid, message: valid ? 'OK' : 'NIK harus 16 digit angka' })
             """),

            // ---------------- Looping & Data ----------------
            ("Batch process with foreach (workflow JSON)", "json", "Looping", "Query then process each record",
             """
             [{"type":"data_query","name":"leads","config":{"entity":"customers","filter":"status eq Lead"}},
              {"type":"foreach","name":"each lead","config":{"items":"{{steps.leads.output}}"},
               "children":[
                 {"type":"log","name":"log lead","config":{"message":"#{{vars.index}}: {{vars.item.name}}"}},
                 {"type":"data_update","name":"touch","config":{"entity":"customers","id":"{{vars.item.id}}",
                  "data":"{\"notes\": \"contacted {{today}}\"}"}}]}]
             """),
            ("Deduplicate array (JS)", "javascript", "Looping", "Remove duplicate objects by key",
             """
             var rows = context.steps.query1.output, seen = {}, unique = [];
             for (var i = 0; i < rows.length; i++) {
               var key = rows[i].email;
               if (!seen[key]) { seen[key] = true; unique.push(rows[i]); }
             }
             (unique)
             """),
            ("Chunk a list (C#)", "csharp", "Looping", "Split a list into batches of N",
             """
             var items = Enumerable.Range(1, 95).ToList();
             var batches = items.Chunk(20).ToList();
             return $"{batches.Count} batches, last has {batches[^1].Length} items";
             """),
            ("Pivot / group with sum (Python)", "python", "Data", "Aggregate records by two keys",
             """
             from collections import defaultdict
             rows = context["steps"]["query1"]["output"]
             pivot = defaultdict(float)
             for r in rows:
                 pivot[(r.get("status"), r.get("customer_id"))] += float(r.get("total") or 0)
             result = [{"status": k[0], "customer": k[1], "total": v} for k, v in pivot.items()]
             """),
            ("Sort + take top N (JS)", "javascript", "Data", "Top 5 orders by total",
             """
             var rows = context.steps.query1.output.slice();
             rows.sort(function (a, b) { return (b.total || 0) - (a.total || 0); });
             (rows.slice(0, 5))
             """),
            ("Format Rupiah (JS)", "javascript", "Data", "Format numbers as IDR currency",
             """
             var amount = Number(context.vars.total || 0);
             ('Rp ' + amount.toLocaleString('id-ID'))
             """),
            ("Date helpers (C#)", "csharp", "Data", "Common date calculations",
             """
             var today = DateTime.Today;
             var startOfMonth = new DateTime(today.Year, today.Month, 1);
             var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
             var age = (int)((today - new DateTime(1990, 5, 17)).TotalDays / 365.25);
             return new { startOfMonth, endOfMonth, age };
             """),

            // ---------------- SQL ----------------
            ("Top-N per group (SQL)", "sql", "SQL", "Latest order per customer with a window function",
             """
             SELECT * FROM (
               SELECT o.*, ROW_NUMBER() OVER (PARTITION BY customer_id ORDER BY order_date DESC) AS rn
               FROM orders o
             ) t WHERE rn = 1;
             """),
            ("Upsert (SQLite)", "sql", "SQL", "Insert or update on conflict",
             """
             INSERT INTO products (sku, name, price)
             VALUES ('SKU-001', 'Wireless Mouse', 150000)
             ON CONFLICT(sku) DO UPDATE SET name = excluded.name, price = excluded.price;
             """),
            ("Monthly sales summary (SQL)", "sql", "SQL", "Aggregate by month for dashboards",
             """
             SELECT strftime('%Y-%m', order_date) AS bulan,
                    COUNT(*)                       AS jumlah_order,
                    SUM(qty * unit_price)          AS omzet
             FROM orders
             GROUP BY 1 ORDER BY 1 DESC;
             """),

            // ---------------- Workflows (patterns) ----------------
            ("Approval pattern (workflow JSON)", "json", "Workflows", "Condition + switch on approval status",
             """
             [{"type":"switch","name":"route by status","config":{"on":"{{trigger.record.status}}"},
               "cases":{
                 "Approved":[{"type":"send_email","config":{"to":"{{trigger.record.email}}",
                   "subject":"Approved ✔","body":"<p>Pengajuan Anda disetujui.</p>"}}],
                 "Rejected":[{"type":"send_email","config":{"to":"{{trigger.record.email}}",
                   "subject":"Rejected","body":"<p>Mohon maaf, pengajuan ditolak.</p>"}}],
                 "default":[{"type":"log","config":{"message":"status masih {{trigger.record.status}}"}}]}}]
             """),
            ("Scheduled report to Slack (workflow JSON)", "json", "Workflows", "Cron trigger → query → AI summary → Slack",
             """
             [{"type":"data_query","name":"orders","config":{"entity":"orders","filter":"status eq New"}},
              {"type":"ai_summarize","name":"ringkas","config":{"text":"{{steps.orders.output}}","length":"short"}},
              {"type":"connector_action","name":"kirim slack","config":{
                "connectorId":"NAMA_CONNECTOR_SLACK","action":"post_message",
                "input":"{\"text\": \"Laporan harian: {{steps.ringkas.output}}\"}"}}]
             """),
            ("Enrich record with AI extract (workflow JSON)", "json", "Workflows", "Extract structured data from free text",
             """
             [{"type":"ai_extract","name":"parse","config":{
                "text":"{{trigger.body.rawText}}","schema":"nama, alamat, nomor_telepon, total_tagihan"}},
              {"type":"data_create","name":"simpan","config":{"entity":"customers",
                "data":"{\"name\": \"{{steps.parse.output.nama}}\", \"phone\": \"{{steps.parse.output.nomor_telepon}}\"}"}}]
             """),
            ("Sync external DB nightly (C# script)", "csharp", "Workflows", "Use IDataSyncService pattern from a scheduled workflow",
             """
             // In a scheduled workflow, add a "Connector Action" step on your SQL connector:
             //   action: query, input: {"sql": "SELECT email, name, city FROM crm.customers"}
             // then a "Run C#" step is not needed — use Data Hub steps, or call the sync API pattern:
             // PullAsync(connector, "query", input, entity: "customers", keyField: "email")
             return "See docs/data-hub.md — cross-connector sync";
             """),

            // ---------------- Forms ----------------
            ("Calculated field expressions", "json", "Forms", "Expressions for the Calculated Value component",
             """
             qty * unit_price
             round(subtotal * 1.11, 2)                          -- PPN 11%
             if(gte(total_harta, nisab), total_harta * 0.025, 0) -- zakat mal
             min(gaji * 0.3, 2500000)                            -- cap 30% of salary
             """),
            ("Master-detail grid filter", "json", "Forms", "Data Grid filter that follows a lookup field",
             """
             In the form: add a Lookup (field: customer_id, entity: customers)
             and a Data Grid (entity: orders) with filter:
             customer_id eq {{customer_id}}
             The grid reloads whenever the lookup changes.
             """),
            ("Custom connector spec template", "json", "Connectors", "Starting point for the Custom Connector Builder",
             """
             {
               "baseUrl": "https://api.example.com",
               "authType": "apikey_header",
               "authParamName": "X-Api-Key",
               "defaultHeaders": { "Accept": "application/json" },
               "actions": [
                 { "key": "list_items", "name": "List items", "method": "GET",
                   "pathTemplate": "/v1/items?limit={{limit}}", "inputs": ["limit"] },
                 { "key": "create_item", "name": "Create item", "method": "POST",
                   "pathTemplate": "/v1/items",
                   "bodyTemplate": "{\"name\": \"{{name}}\"}", "inputs": ["name"] }
               ]
             }
             """),
        ];

        foreach (var item in items)
        {
            if (existingTitles.Contains(item.Title)) continue;
            await snippets.SaveAsync(new CodeSnippet
            {
                Title = item.Title,
                Language = item.Lang,
                Category = item.Category,
                Description = item.Description,
                Code = item.Code,
                IsBuiltIn = true,
                Tags = item.Category.ToLowerInvariant()
            });
        }
    }

    /// <summary>Showcase forms demonstrating dashboards, filtered grids, master-detail, and calculators.</summary>
    private static async Task SeedShowcaseFormsAsync(IServiceProvider services)
    {
        var forms = services.GetRequiredService<IFormService>();

        async Task AddIfMissing(FormDefinition form)
        {
            if (await forms.GetBySlugAsync(form.Slug) is null)
                await forms.SaveAsync(form, "seed");
        }

        await AddIfMissing(new FormDefinition
        {
            Name = "Sales Dashboard",
            Slug = "sales_dashboard",
            Description = "Dashboard interaktif: KPI, chart, dan tabel order",
            Icon = "📊",
            IsPublished = true,
            SubmitLabel = "Refresh",
            Layout =
            [
                new() { Type = ComponentTypes.Heading, Label = "📊 Sales Dashboard", Width = 12 },
                new() { Type = ComponentTypes.Chart, Label = "Order per Status", Width = 6,
                        Props = new() { ["entity"] = "orders", ["chartType"] = "pie", ["groupBy"] = "status" } },
                new() { Type = ComponentTypes.Chart, Label = "Omzet per Status", Width = 6,
                        Props = new() { ["entity"] = "orders", ["chartType"] = "bar", ["groupBy"] = "status", ["valueField"] = "total" } },
                new() { Type = ComponentTypes.Chart, Label = "Produk per Kategori", Width = 6,
                        Props = new() { ["entity"] = "products", ["chartType"] = "bar", ["groupBy"] = "category" } },
                new() { Type = ComponentTypes.Chart, Label = "Customer per Kota", Width = 6,
                        Props = new() { ["entity"] = "customers", ["chartType"] = "pie", ["groupBy"] = "city" } },
                new() { Type = ComponentTypes.DataGrid, Label = "Order Terbaru", Width = 12,
                        Props = new() { ["entity"] = "orders", ["pageSize"] = "8",
                                        ["columns"] = "order_no,order_date,qty,unit_price,total,status" } },
            ]
        });

        await AddIfMissing(new FormDefinition
        {
            Name = "Orders Grid + Filter",
            Slug = "orders_grid_filter",
            Description = "Grid dengan filter status via dropdown",
            Icon = "🗃️",
            IsPublished = true,
            SubmitLabel = "Apply",
            Layout =
            [
                new() { Type = ComponentTypes.Heading, Label = "🗃️ Orders Explorer", Width = 12 },
                new() { Type = ComponentTypes.Dropdown, Label = "Filter status", Field = "flt_status", Width = 4,
                        Props = new() { ["options"] = "New,Paid,Shipped,Done,Cancelled" } },
                new() { Type = ComponentTypes.Paragraph, Label = "Pilih status untuk memfilter grid di bawah.", Width = 8 },
                new() { Type = ComponentTypes.DataGrid, Label = "Orders", Width = 12,
                        Props = new() { ["entity"] = "orders", ["filter"] = "status eq {{flt_status}}",
                                        ["pageSize"] = "15",
                                        ["columns"] = "order_no,order_date,qty,unit_price,total,status" } },
                new() { Type = ComponentTypes.Divider, Width = 12 },
                new() { Type = ComponentTypes.DataGrid, Label = "Semua Order (tanpa filter)", Width = 12,
                        Props = new() { ["entity"] = "orders", ["pageSize"] = "5" } },
            ]
        });

        await AddIfMissing(new FormDefinition
        {
            Name = "Customer Orders (Master-Detail)",
            Slug = "customer_orders_master_detail",
            Description = "Pilih customer (master) → grid order miliknya (detail)",
            Icon = "🧭",
            IsPublished = true,
            SubmitLabel = "OK",
            Layout =
            [
                new() { Type = ComponentTypes.Heading, Label = "🧭 Customer → Orders", Width = 12 },
                new() { Type = ComponentTypes.Lookup, Label = "Customer", Field = "customer_id", Width = 6,
                        Props = new() { ["entity"] = "customers", ["displayField"] = "name" } },
                new() { Type = ComponentTypes.Spacer, Width = 6 },
                new() { Type = ComponentTypes.DataGrid, Label = "Orders milik customer terpilih", Width = 12,
                        Props = new() { ["entity"] = "orders", ["filter"] = "customer_id eq {{customer_id}}",
                                        ["pageSize"] = "15",
                                        ["columns"] = "order_no,order_date,qty,unit_price,total,status" } },
                new() { Type = ComponentTypes.Chart, Label = "Omzet customer per status", Width = 12,
                        Props = new() { ["entity"] = "orders", ["filter"] = "customer_id eq {{customer_id}}",
                                        ["chartType"] = "bar", ["groupBy"] = "status", ["valueField"] = "total" } },
            ]
        });

        await AddIfMissing(new FormDefinition
        {
            Name = "Kalkulator Cicilan",
            Slug = "loan_calculator",
            Description = "Kalkulator cicilan flat: pokok, bunga per tahun, tenor",
            Icon = "🧮",
            IsPublished = true,
            SubmitLabel = "Simpan",
            Layout =
            [
                new() { Type = ComponentTypes.Heading, Label = "🧮 Kalkulator Cicilan (bunga flat)", Width = 12 },
                new() { Type = ComponentTypes.Number, Label = "Pokok pinjaman (Rp)", Field = "pokok", Width = 4, DefaultValue = "12000000" },
                new() { Type = ComponentTypes.Number, Label = "Bunga per tahun (%)", Field = "bunga", Width = 4, DefaultValue = "10" },
                new() { Type = ComponentTypes.Number, Label = "Tenor (bulan)", Field = "tenor", Width = 4, DefaultValue = "12" },
                new() { Type = ComponentTypes.Calc, Label = "Total bunga", Width = 4,
                        Props = new() { ["expression"] = "pokok * (bunga / 100) * (tenor / 12)", ["format"] = "currency", ["prefix"] = "Rp " } },
                new() { Type = ComponentTypes.Calc, Label = "Total pembayaran", Width = 4,
                        Props = new() { ["expression"] = "pokok + pokok * (bunga / 100) * (tenor / 12)", ["format"] = "currency", ["prefix"] = "Rp " } },
                new() { Type = ComponentTypes.Calc, Label = "Cicilan per bulan", Width = 4,
                        Props = new() { ["expression"] = "(pokok + pokok * (bunga / 100) * (tenor / 12)) / max(tenor, 1)", ["format"] = "currency", ["prefix"] = "Rp " } },
                new() { Type = ComponentTypes.Paragraph, Label = "Ubah angka di atas — hasil dihitung langsung (komponen Calculated Value).", Width = 12 },
            ]
        });

        await AddIfMissing(new FormDefinition
        {
            Name = "Hitung Zakat Mal",
            Slug = "zakat_mal",
            Description = "Kalkulator zakat mal 2,5% dengan nisab 85 gram emas",
            Icon = "🕌",
            IsPublished = true,
            SubmitLabel = "Selesai",
            Layout =
            [
                new() { Type = ComponentTypes.Heading, Label = "🕌 Kalkulator Zakat Mal", Width = 12 },
                new() { Type = ComponentTypes.Paragraph,
                        Label = "Zakat mal wajib jika total harta bersih mencapai nisab (85 gram emas) dan telah dimiliki 1 tahun (haul). Tarif 2,5%.",
                        Width = 12 },
                new() { Type = ComponentTypes.Number, Label = "Harga emas per gram (Rp)", Field = "harga_emas", Width = 6, DefaultValue = "1350000" },
                new() { Type = ComponentTypes.Calc, Label = "Nisab (85 gram emas)", Width = 6,
                        Props = new() { ["expression"] = "harga_emas * 85", ["format"] = "currency", ["prefix"] = "Rp " } },
                new() { Type = ComponentTypes.Divider, Width = 12 },
                new() { Type = ComponentTypes.Number, Label = "Uang tunai & tabungan (Rp)", Field = "uang", Width = 4, DefaultValue = "0" },
                new() { Type = ComponentTypes.Number, Label = "Emas & perak — nilai (Rp)", Field = "emas", Width = 4, DefaultValue = "0" },
                new() { Type = ComponentTypes.Number, Label = "Aset investasi/dagang (Rp)", Field = "aset", Width = 4, DefaultValue = "0" },
                new() { Type = ComponentTypes.Number, Label = "Piutang lancar (Rp)", Field = "piutang", Width = 6, DefaultValue = "0" },
                new() { Type = ComponentTypes.Number, Label = "Hutang jatuh tempo (Rp)", Field = "hutang", Width = 6, DefaultValue = "0" },
                new() { Type = ComponentTypes.Calc, Label = "Total harta bersih", Width = 6,
                        Props = new() { ["expression"] = "uang + emas + aset + piutang - hutang", ["format"] = "currency", ["prefix"] = "Rp " } },
                new() { Type = ComponentTypes.Calc, Label = "Zakat yang wajib dibayar (2,5%)", Width = 6,
                        Props = new()
                        {
                            ["expression"] = "if(gte(uang + emas + aset + piutang - hutang, harga_emas * 85), (uang + emas + aset + piutang - hutang) * 0.025, 0)",
                            ["format"] = "currency", ["prefix"] = "Rp "
                        },
                        HelpText = "Rp 0 berarti harta belum mencapai nisab — belum wajib zakat mal." },
            ]
        });
    }

    private static async Task SeedAppAsync(IServiceProvider services)
    {
        var apps = services.GetRequiredService<IAppService>();
        var forms = services.GetRequiredService<IFormService>();
        var all = await forms.GetAllAsync();
        var app = await apps.SaveAsync(new AppDefinition
        {
            Name = "Customer Portal",
            Slug = "customer-portal",
            Description = "Demo app: registrasi pelanggan, entry produk, dan quick task",
            Icon = "🏪",
            Color = "#4f6ef7",
            FormIds = all.Select(f => f.Id).ToList(),
            HomeFormId = all.FirstOrDefault(f => f.Slug == "customer_registration")?.Id,
            AllowAnonymous = false,
            RequiredRole = "EndUser"
        });
        await apps.PublishAsync(app.Id, true);
    }
}
