using BlazePoint.Services.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace BlazePoint.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var storage = services.GetRequiredService<IFileStorage>();

        foreach (var role in new[] { "Admin", "Editor", "Viewer" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        async Task<ApplicationUser> EnsureUser(string email, string name, string role, string color)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email, Email = email, EmailConfirmed = true,
                    DisplayName = name, AvatarColor = color
                };
                await userManager.CreateAsync(user, "Blaze123!");
                await userManager.AddToRoleAsync(user, role);
            }
            return user;
        }

        // --- Users (password semua: Blaze123!) ---
        var admin = await EnsureUser("admin@blazepoint.local", "Admin BlazePoint", "Admin", "#e41e3f");
        var editor = await EnsureUser("editor@blazepoint.local", "Eka Editor", "Editor", "#31a24c");
        var viewer = await EnsureUser("viewer@blazepoint.local", "Vina Viewer", "Viewer", "#f7b928");
        var budi = await EnsureUser("budi@blazepoint.local", "Budi Santoso", "Editor", "#0ea5e9");
        var sari = await EnsureUser("sari@blazepoint.local", "Sari Dewi", "Editor", "#9360f7");
        var rudi = await EnsureUser("rudi@blazepoint.local", "Rudi Hartono", "Viewer", "#f02849");
        var maya = await EnsureUser("maya@blazepoint.local", "Maya Putri", "Viewer", "#31a24c");
        var andi = await EnsureUser("andi@blazepoint.local", "Andi Wijaya", "Viewer", "#1877f2");

        if (await db.Sites.AnyAsync()) return; // already seeded

        // --- Sites ---
        var hrSite = new Site { Name = "Human Resources", Slug = "hr", Description = "Portal tim HR: kebijakan, onboarding, dan pengumuman.", Department = "HR", Icon = "👥", Color = "#31a24c", CreatedById = admin.Id };
        var itSite = new Site { Name = "IT Department", Slug = "it", Description = "Dokumentasi teknis, helpdesk, dan knowledge base IT.", Department = "IT", Icon = "💻", Color = "#1877f2", CreatedById = admin.Id };
        var mktSite = new Site { Name = "Marketing", Slug = "marketing", Description = "Kampanye, brand assets, dan kalender konten.", Department = "Marketing", Icon = "📣", Color = "#f02849", CreatedById = admin.Id };
        var finSite = new Site { Name = "Finance", Slug = "finance", Description = "Anggaran, laporan keuangan, dan reimbursement.", Department = "Finance", Icon = "💰", Color = "#f7b928", CreatedById = admin.Id };
        db.Sites.AddRange(hrSite, itSite, mktSite, finSite);
        await db.SaveChangesAsync();

        // --- Navigation ---
        db.NavigationItems.AddRange(
            new NavigationItem { Title = "Home", Url = "/", Icon = "🏠", Order = 0, Location = NavLocation.TopNav },
            new NavigationItem { Title = "Documents", Url = "/documents", Icon = "📁", Order = 1, Location = NavLocation.TopNav },
            new NavigationItem { Title = "Sites", Url = "/sites", Icon = "🏢", Order = 2, Location = NavLocation.TopNav },
            new NavigationItem { Title = "Intranet", Url = "/p/intranet-home", Icon = "🌐", Order = 3, Location = NavLocation.TopNav },
            new NavigationItem { Title = "Clippy", Url = "/chat", Icon = "🤖", Order = 4, Location = NavLocation.TopNav },
            new NavigationItem { Title = "Dashboard", Url = "/", Icon = "📊", Order = 0, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Documents", Url = "/documents", Icon = "📁", Order = 1, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Lists", Url = "/lists", Icon = "📋", Order = 2, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Pages", Url = "/pages", Icon = "📄", Order = 3, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Search", Url = "/search", Icon = "🔍", Order = 4, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Workflows", Url = "/workflows", Icon = "⚙️", Order = 5, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Discussions", Url = "/discussions", Icon = "💬", Order = 6, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Calendar", Url = "/calendar", Icon = "📅", Order = 7, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Forms", Url = "/forms", Icon = "📝", Order = 8, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Team Sites", Url = "/sites", Icon = "🏢", Order = 9, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Shared Links", Url = "/shares", Icon = "🔗", Order = 10, Location = NavLocation.QuickLaunch },
            new NavigationItem { Title = "Clippy Chat", Url = "/chat", Icon = "🤖", Order = 11, Location = NavLocation.QuickLaunch }
        );

        // --- Sample documents (real files in storage) ---
        async Task SeedDocument(string folder, string name, string contentType, string content,
            ApplicationUser owner, int? siteId, Dictionary<string, string> meta)
        {
            var key = $"documents/seed/{Guid.NewGuid():N}/{name}";
            var bytes = Encoding.UTF8.GetBytes(content);
            using var ms = new MemoryStream(bytes);
            await storage.SaveAsync(key, ms, contentType);
            var doc = new Document
            {
                SiteId = siteId, FolderPath = folder, Name = name, ContentType = contentType,
                Size = bytes.Length, StorageKey = key, CreatedById = owner.Id,
                MetadataJson = JsonSerializer.Serialize(meta)
            };
            db.Documents.Add(doc);
            await db.SaveChangesAsync();
            db.DocumentVersions.Add(new DocumentVersion
            {
                DocumentId = doc.Id, Version = 1, StorageKey = key, Size = bytes.Length,
                Comment = "Initial upload", CreatedById = owner.Id
            });
            await db.SaveChangesAsync();
        }

        await SeedDocument("/", "Panduan-Onboarding.md", "text/markdown", """
            # Panduan Onboarding Karyawan Baru

            Selamat bergabung di perusahaan! Ikuti langkah-langkah berikut di minggu pertama Anda:

            1. **Hari 1** — Ambil laptop & ID card di meja IT (lantai 2), aktivasi akun email dan BlazePoint.
            2. **Hari 2** — Sesi orientasi bersama HR pukul 09:00 di Aula Utama.
            3. **Hari 3-4** — Shadowing bersama buddy dari tim Anda.
            4. **Hari 5** — 1-on-1 pertama dengan manajer, setup OKR pribadi.

            > Pertanyaan? Hubungi hr@blazepoint.local atau tanya Clippy di menu Chat.
            """, admin, hrSite.Id, new() { ["kategori"] = "HR", ["tag"] = "onboarding, panduan", ["status"] = "final" });

        await SeedDocument("/", "Kebijakan-Cuti-2026.md", "text/markdown", """
            # Kebijakan Cuti Tahun 2026

            | Jenis Cuti | Jatah | Keterangan |
            |---|---|---|
            | Tahunan | 12 hari | Dapat dicairkan maksimal 5 hari |
            | Sakit | Sesuai surat dokter | Wajib lampirkan surat untuk >2 hari |
            | Melahirkan | 3 bulan | Sesuai UU Ketenagakerjaan |
            | Penting | 3 hari | Menikah, keluarga inti wafat, dll |

            Pengajuan cuti dilakukan melalui **Formulir Cuti** di menu Forms, minimal H-3.
            """, sari, hrSite.Id, new() { ["kategori"] = "HR", ["tag"] = "cuti, kebijakan", ["tahun"] = "2026" });

        await SeedDocument("/IT", "Standar-Keamanan-IT.md", "text/markdown", """
            # Standar Keamanan IT

            - Gunakan password minimal 12 karakter + MFA di semua sistem internal.
            - Laptop wajib full-disk encryption (BitLocker/FileVault).
            - Dilarang menyimpan kredensial di repository kode.
            - Laporkan insiden keamanan ke it-security@blazepoint.local dalam 1x24 jam.
            - Review akses sistem dilakukan tiap kuartal oleh tim IT.
            """, budi, itSite.Id, new() { ["kategori"] = "IT", ["tag"] = "keamanan, standar", ["review"] = "quarterly" });

        await SeedDocument("/IT", "Arsitektur-Sistem-Internal.md", "text/markdown", """
            # Arsitektur Sistem Internal

            Sistem internal berjalan di atas .NET 10 dengan arsitektur modular:
            - **BlazePoint** — portal kolaborasi (Blazor Server, SQLite/PostgreSQL)
            - **API Gateway** — REST + GraphQL untuk integrasi
            - **Storage** — MinIO on-premise dengan replikasi ke S3

            Diagram lengkap tersedia di folder /IT/diagrams.
            """, budi, itSite.Id, new() { ["kategori"] = "IT", ["tag"] = "arsitektur, dokumentasi" });

        await SeedDocument("/Marketing", "Brand-Guideline-Ringkas.md", "text/markdown", """
            # Brand Guideline Ringkas

            - Warna utama: **#1877f2** (biru BlazePoint), sekunder #31a24c.
            - Font: Segoe UI / system-ui, heading bold 700.
            - Logo minimal clear-space 16px, jangan diregangkan.
            - Tone of voice: ramah, ringkas, profesional.
            """, maya, mktSite.Id, new() { ["kategori"] = "Marketing", ["tag"] = "brand, guideline" });

        await SeedDocument("/Finance", "Prosedur-Reimbursement.txt", "text/plain", """
            PROSEDUR REIMBURSEMENT

            1. Kumpulkan bukti pembayaran (struk/invoice) maksimal 30 hari setelah transaksi.
            2. Isi form reimbursement di menu Forms dengan nominal dan kategori biaya.
            3. Persetujuan berjenjang: atasan langsung -> Finance.
            4. Dana cair ke rekening payroll dalam 7 hari kerja setelah disetujui.

            Batas reimbursement tanpa persetujuan direksi: Rp 5.000.000 per pengajuan.
            """, admin, finSite.Id, new() { ["kategori"] = "Finance", ["tag"] = "reimbursement, prosedur" });

        await SeedDocument("/", "Notulen-Townhall-Juli.md", "text/markdown", """
            # Notulen Townhall — Juli 2026

            **Agenda:** capaian H1, roadmap H2, sesi tanya-jawab.

            - Pendapatan H1 tumbuh 18% YoY; target H2 dinaikkan 10%.
            - Peluncuran produk baru dijadwalkan Oktober.
            - Program hybrid working diperpanjang hingga akhir tahun.
            - Q&A: kebijakan bonus akan diumumkan September.
            """, editor, null, new() { ["kategori"] = "Umum", ["tag"] = "notulen, townhall" });

        // --- Lists ---
        string Item(params (string k, object v)[] kv) =>
            JsonSerializer.Serialize(kv.ToDictionary(x => x.k, x => x.v));

        var assetColumns = new List<ListColumn>
        {
            new() { Name = "Asset", Type = "Text", Required = true },
            new() { Name = "Category", Type = "Choice", Choices = ["Laptop", "Monitor", "Phone", "Peripheral", "Server"] },
            new() { Name = "Owner", Type = "Text" },
            new() { Name = "PurchaseDate", Type = "Date" },
            new() { Name = "Price", Type = "Number" },
            new() { Name = "InUse", Type = "Boolean" }
        };
        var assetList = new ListDefinition
        {
            SiteId = itSite.Id, Name = "IT Assets", Icon = "🖥️",
            Description = "Inventaris aset IT perusahaan",
            ColumnsJson = JsonSerializer.Serialize(assetColumns)
        };
        db.Lists.Add(assetList);
        await db.SaveChangesAsync();
        db.ListItems.AddRange(
            new ListItemEntity { ListId = assetList.Id, CreatedById = admin.Id, ValuesJson = Item(("Asset", "ThinkPad X1 Carbon"), ("Category", "Laptop"), ("Owner", "Eka Editor"), ("PurchaseDate", "2025-03-10"), ("Price", 21000000), ("InUse", true)) },
            new ListItemEntity { ListId = assetList.Id, CreatedById = admin.Id, ValuesJson = Item(("Asset", "Dell U2723QE"), ("Category", "Monitor"), ("Owner", "Vina Viewer"), ("PurchaseDate", "2025-05-02"), ("Price", 9500000), ("InUse", true)) },
            new ListItemEntity { ListId = assetList.Id, CreatedById = editor.Id, ValuesJson = Item(("Asset", "iPhone 15"), ("Category", "Phone"), ("Owner", "Admin BlazePoint"), ("PurchaseDate", "2024-11-20"), ("Price", 16000000), ("InUse", false)) },
            new ListItemEntity { ListId = assetList.Id, CreatedById = budi.Id, ValuesJson = Item(("Asset", "MacBook Pro M4"), ("Category", "Laptop"), ("Owner", "Budi Santoso"), ("PurchaseDate", "2026-01-15"), ("Price", 32000000), ("InUse", true)) },
            new ListItemEntity { ListId = assetList.Id, CreatedById = budi.Id, ValuesJson = Item(("Asset", "Dell PowerEdge R760"), ("Category", "Server"), ("Owner", "IT Infra"), ("PurchaseDate", "2025-09-01"), ("Price", 145000000), ("InUse", true)) },
            new ListItemEntity { ListId = assetList.Id, CreatedById = budi.Id, ValuesJson = Item(("Asset", "Logitech MX Master 3S"), ("Category", "Peripheral"), ("Owner", "Sari Dewi"), ("PurchaseDate", "2026-02-20"), ("Price", 1600000), ("InUse", true)) });

        var contactCols = new List<ListColumn>
        {
            new() { Name = "Nama", Type = "Text", Required = true },
            new() { Name = "Departemen", Type = "Choice", Choices = ["HR", "IT", "Marketing", "Finance"] },
            new() { Name = "Email", Type = "Text" },
            new() { Name = "Telepon", Type = "Text" }
        };
        var contactList = new ListDefinition
        {
            SiteId = hrSite.Id, Name = "Kontak Karyawan", Icon = "📇",
            Description = "Direktori kontak karyawan",
            ColumnsJson = JsonSerializer.Serialize(contactCols)
        };
        db.Lists.Add(contactList);
        await db.SaveChangesAsync();
        db.ListItems.AddRange(
            new ListItemEntity { ListId = contactList.Id, CreatedById = admin.Id, ValuesJson = Item(("Nama", "Budi Santoso"), ("Departemen", "IT"), ("Email", "budi@blazepoint.local"), ("Telepon", "0812-1111-2222")) },
            new ListItemEntity { ListId = contactList.Id, CreatedById = admin.Id, ValuesJson = Item(("Nama", "Sari Dewi"), ("Departemen", "HR"), ("Email", "sari@blazepoint.local"), ("Telepon", "0813-3333-4444")) },
            new ListItemEntity { ListId = contactList.Id, CreatedById = admin.Id, ValuesJson = Item(("Nama", "Maya Putri"), ("Departemen", "Marketing"), ("Email", "maya@blazepoint.local"), ("Telepon", "0815-5555-6666")) },
            new ListItemEntity { ListId = contactList.Id, CreatedById = admin.Id, ValuesJson = Item(("Nama", "Rudi Hartono"), ("Departemen", "Finance"), ("Email", "rudi@blazepoint.local"), ("Telepon", "0816-7777-8888")) },
            new ListItemEntity { ListId = contactList.Id, CreatedById = admin.Id, ValuesJson = Item(("Nama", "Andi Wijaya"), ("Departemen", "IT"), ("Email", "andi@blazepoint.local"), ("Telepon", "0817-9999-0000")) });

        var projectCols = new List<ListColumn>
        {
            new() { Name = "Proyek", Type = "Text", Required = true },
            new() { Name = "PIC", Type = "Text" },
            new() { Name = "Status", Type = "Choice", Choices = ["Planning", "On Track", "At Risk", "Done"] },
            new() { Name = "Deadline", Type = "Date" },
            new() { Name = "Progress", Type = "Number" }
        };
        var projectList = new ListDefinition
        {
            Name = "Project Tracker", Icon = "🎯",
            Description = "Pantauan proyek lintas departemen",
            ColumnsJson = JsonSerializer.Serialize(projectCols)
        };
        db.Lists.Add(projectList);
        await db.SaveChangesAsync();
        db.ListItems.AddRange(
            new ListItemEntity { ListId = projectList.Id, CreatedById = editor.Id, ValuesJson = Item(("Proyek", "Migrasi .NET 10"), ("PIC", "Budi Santoso"), ("Status", "On Track"), ("Deadline", "2026-08-30"), ("Progress", 65)) },
            new ListItemEntity { ListId = projectList.Id, CreatedById = editor.Id, ValuesJson = Item(("Proyek", "Kampanye Q3"), ("PIC", "Maya Putri"), ("Status", "Planning"), ("Deadline", "2026-09-15"), ("Progress", 20)) },
            new ListItemEntity { ListId = projectList.Id, CreatedById = admin.Id, ValuesJson = Item(("Proyek", "Audit Keamanan Tahunan"), ("PIC", "Andi Wijaya"), ("Status", "At Risk"), ("Deadline", "2026-07-31"), ("Progress", 40)) },
            new ListItemEntity { ListId = projectList.Id, CreatedById = admin.Id, ValuesJson = Item(("Proyek", "Onboarding Batch Juli"), ("PIC", "Sari Dewi"), ("Status", "Done"), ("Deadline", "2026-07-10"), ("Progress", 100)) });

        // --- CMS pages with webparts ---
        var homeParts = new List<WebPartModel>
        {
            new() { Type = "Text", Title = "Selamat Datang", Column = 0, Order = 0, Settings = new() { ["content"] = "## Selamat datang di BlazePoint 🎉\n\nPortal kolaborasi modern untuk tim Anda. Kelola **dokumen**, **list**, **workflow**, dan banyak lagi.\n\n- Upload dan versioning dokumen\n- Pencarian full-text & semantik\n- Chatbot Clippy siap membantu" } },
            new() { Type = "Clock", Title = "Waktu Server", Column = 1, Order = 0, Settings = new() { ["timezone"] = "SE Asia Standard Time" } },
            new() { Type = "Weather", Title = "Cuaca Jakarta", Column = 1, Order = 1, Settings = new() { ["latitude"] = "-6.2", ["longitude"] = "106.8", ["city"] = "Jakarta" } },
            new() { Type = "Calculator", Title = "Kalkulator", Column = 2, Order = 0, Settings = [] },
            new() { Type = "Map", Title = "Kantor Pusat", Column = 0, Order = 1, Settings = new() { ["latitude"] = "-6.2088", ["longitude"] = "106.8456", ["zoom"] = "13", ["label"] = "Kantor Pusat BlazePoint, Jakarta" } }
        };
        var welcomePage = new CmsPage
        {
            Title = "Welcome to BlazePoint", Slug = "welcome", Layout = "Intranet",
            ContentJson = JsonSerializer.Serialize(homeParts),
            PublishedJson = JsonSerializer.Serialize(homeParts),
            IsPublished = true, Version = 1, CreatedById = admin.Id
        };

        var newsParts = new List<WebPartModel>
        {
            new() { Type = "Text", Title = "Berita Utama", Column = 0, Order = 0, Settings = new() { ["content"] = "# Kuartal Ketiga Melampaui Target 🚀\n\nTim penjualan berhasil melampaui target Q3 sebesar **15%**. Terima kasih untuk seluruh tim atas kerja kerasnya!\n\n> \"Pencapaian ini adalah hasil kolaborasi semua departemen.\" — CEO" } },
            new() { Type = "Text", Title = "Pengumuman", Column = 1, Order = 0, Settings = new() { ["content"] = "### Pengumuman\n\n- Townhall meeting: Jumat, 15:00\n- Libur nasional: 17 Agustus\n- Program wellness baru dimulai bulan depan" } },
            new() { Type = "Rss", Title = "Berita .NET", Column = 1, Order = 1, Settings = new() { ["feedUrl"] = "https://devblogs.microsoft.com/dotnet/feed/", ["count"] = "4" } }
        };
        var newsPage = new CmsPage
        {
            Title = "Company News", Slug = "news", Layout = "News",
            ContentJson = JsonSerializer.Serialize(newsParts),
            PublishedJson = JsonSerializer.Serialize(newsParts),
            IsPublished = true, Version = 1, CreatedById = editor.Id
        };

        // Intranet home — showcase of the SharePoint-style webparts
        var intranetParts = new List<WebPartModel>
        {
            new() { Type = "Hero", Title = "", Column = 0, Order = 0, Settings = new()
            {
                ["imageUrl"] = "https://picsum.photos/id/180/1200/400",
                ["title"] = "Intranet BlazePoint",
                ["subtitle"] = "Satu portal untuk semua kebutuhan kolaborasi tim",
                ["buttonText"] = "📁 Buka Dokumen", ["buttonUrl"] = "/documents"
            } },
            new() { Type = "QuickLinks", Title = "Akses Cepat", Column = 0, Order = 1, Settings = new()
            {
                ["links"] = "Dokumen|/documents|📁\nKalender|/calendar|📅\nDiskusi|/discussions|💬\nForms|/forms|📝\nWorkflows|/workflows|⚙️\nClippy|/chat|🤖\nTeam Sites|/sites|🏢\nBerita|/p/news|📰"
            } },
            new() { Type = "News", Title = "Berita Terbaru", Column = 0, Order = 2, Settings = new() { ["count"] = "4" } },
            new() { Type = "People", Title = "Tim Kami", Column = 0, Order = 3, Settings = new() { ["count"] = "8" } },
            new() { Type = "Countdown", Title = "Hitung Mundur", Column = 1, Order = 0, Settings = new()
            {
                ["label"] = "Menuju Peluncuran Produk Oktober",
                ["targetDate"] = DateTime.Today.AddDays(80).ToString("yyyy-MM-dd") + " 09:00"
            } },
            new() { Type = "Events", Title = "Event Mendatang", Column = 1, Order = 1, Settings = new() { ["count"] = "5" } },
            new() { Type = "ListView", Title = "Project Tracker", Column = 1, Order = 2, Settings = new() { ["listId"] = projectList.Id.ToString(), ["count"] = "4" } },
            new() { Type = "Activity", Title = "Aktivitas Portal", Column = 2, Order = 0, Settings = new() { ["count"] = "6" } },
            new() { Type = "Gallery", Title = "Galeri Kantor", Column = 2, Order = 1, Settings = new()
            {
                ["urls"] = "https://picsum.photos/id/1011/400\nhttps://picsum.photos/id/1015/400\nhttps://picsum.photos/id/1025/400\nhttps://picsum.photos/id/1035/400"
            } },
            new() { Type = "Button", Title = "", Column = 2, Order = 2, Settings = new()
            {
                ["text"] = "🤖 Tanya Clippy", ["url"] = "/chat", ["color"] = "#31a24c", ["align"] = "center"
            } }
        };
        var intranetPage = new CmsPage
        {
            Title = "Intranet Home", Slug = "intranet-home", Layout = "Intranet",
            ContentJson = JsonSerializer.Serialize(intranetParts),
            PublishedJson = JsonSerializer.Serialize(intranetParts),
            IsPublished = true, Version = 1, CreatedById = admin.Id
        };

        var itKbParts = new List<WebPartModel>
        {
            new() { Type = "Text", Title = "Knowledge Base IT", Column = 0, Order = 0, Settings = new() { ["content"] = "## Knowledge Base IT 💻\n\nKumpulan panduan teknis untuk karyawan:\n\n1. **Reset password** — gunakan menu Lupa Password di halaman login.\n2. **VPN** — unduh profil dari /IT, hubungi helpdesk untuk token.\n3. **Printer** — driver tersedia di \\\\printserver\\drivers.\n\nButuh bantuan? Buat tiket ke helpdesk@blazepoint.local" } },
            new() { Type = "Video", Title = "Video Tutorial", Column = 1, Order = 0, Settings = new() { ["url"] = "https://www.youtube.com/watch?v=4XU2Bient0U", ["caption"] = "Pengenalan .NET untuk pemula" } },
            new() { Type = "Divider", Title = "", Column = 0, Order = 1, Settings = [] },
            new() { Type = "ListView", Title = "Aset IT Terbaru", Column = 0, Order = 2, Settings = new() { ["listId"] = assetList.Id.ToString(), ["count"] = "5" } }
        };
        var itKbPage = new CmsPage
        {
            Title = "IT Knowledge Base", Slug = "it-kb", Layout = "Web", SiteId = itSite.Id,
            ContentJson = JsonSerializer.Serialize(itKbParts),
            PublishedJson = JsonSerializer.Serialize(itKbParts),
            IsPublished = true, Version = 1, CreatedById = budi.Id
        };

        db.CmsPages.AddRange(welcomePage, newsPage, intranetPage, itKbPage);
        await db.SaveChangesAsync();
        db.CmsPageVersions.AddRange(
            new CmsPageVersion { PageId = welcomePage.Id, Version = 1, ContentJson = welcomePage.PublishedJson, Comment = "Initial publish", CreatedById = admin.Id },
            new CmsPageVersion { PageId = newsPage.Id, Version = 1, ContentJson = newsPage.PublishedJson, Comment = "Initial publish", CreatedById = editor.Id },
            new CmsPageVersion { PageId = intranetPage.Id, Version = 1, ContentJson = intranetPage.PublishedJson, Comment = "Initial publish", CreatedById = admin.Id },
            new CmsPageVersion { PageId = itKbPage.Id, Version = 1, ContentJson = itKbPage.PublishedJson, Comment = "Initial publish", CreatedById = budi.Id });

        // --- Workflows ---
        var docApprovalJson = """
        {
          "nodes": [
            { "id": "start", "type": "Start", "label": "Mulai", "x": 60, "y": 160 },
            { "id": "review", "type": "Approval", "label": "Review Manager", "x": 280, "y": 160, "assignRole": "Editor" },
            { "id": "approve", "type": "Approval", "label": "Persetujuan Admin", "x": 520, "y": 160, "assignRole": "Admin" },
            { "id": "notify", "type": "Notify", "label": "Notifikasi Pemohon", "x": 760, "y": 160, "message": "Dokumen Anda telah disetujui." },
            { "id": "end", "type": "End", "label": "Selesai", "x": 980, "y": 160 }
          ],
          "edges": [
            { "from": "start", "to": "review" },
            { "from": "review", "to": "approve" },
            { "from": "approve", "to": "notify" },
            { "from": "notify", "to": "end" }
          ]
        }
        """;
        var leaveApprovalJson = """
        {
          "nodes": [
            { "id": "start", "type": "Start", "label": "Pengajuan", "x": 50, "y": 140 },
            { "id": "hr", "type": "Approval", "label": "Verifikasi HR", "x": 260, "y": 140, "assignRole": "Editor" },
            { "id": "cond", "type": "Condition", "label": "Cuti > 5 hari?", "x": 490, "y": 140, "conditionKey": "durasi", "conditionValue": "panjang" },
            { "id": "director", "type": "Approval", "label": "Persetujuan Direksi", "x": 700, "y": 60, "assignRole": "Admin" },
            { "id": "notify", "type": "Notify", "label": "Notifikasi Hasil", "x": 700, "y": 230, "message": "Pengajuan cuti Anda telah diproses." },
            { "id": "end", "type": "End", "label": "Selesai", "x": 930, "y": 140 }
          ],
          "edges": [
            { "from": "start", "to": "hr" },
            { "from": "hr", "to": "cond" },
            { "from": "cond", "to": "director", "label": "yes" },
            { "from": "cond", "to": "notify", "label": "no" },
            { "from": "director", "to": "notify" },
            { "from": "notify", "to": "end" }
          ]
        }
        """;
        db.WorkflowDefinitions.AddRange(
            new WorkflowDefinition
            {
                Name = "Document Approval",
                Description = "Alur persetujuan dokumen dua tingkat: review Editor lalu persetujuan Admin.",
                DefinitionJson = docApprovalJson
            },
            new WorkflowDefinition
            {
                Name = "Leave Approval",
                Description = "Persetujuan cuti: verifikasi HR, lalu ke Direksi bila durasi panjang (context: durasi=panjang).",
                DefinitionJson = leaveApprovalJson
            });

        // --- Discussions ---
        var thread1 = new DiscussionThread
        {
            SiteId = itSite.Id, Title = "Migrasi ke .NET 10 — pengalaman dan tips?",
            Body = "Tim kita baru saja mulai migrasi ke .NET 10. Ada yang sudah pengalaman dengan breaking changes-nya? Share di sini ya!",
            CreatedById = admin.Id
        };
        var thread2 = new DiscussionThread
        {
            SiteId = hrSite.Id, Title = "Usulan kegiatan team building Q3",
            Body = "HR sedang menyusun agenda team building kuartal ini. Drop ide kalian: outdoor, workshop, atau volunteering? @sari",
            CreatedById = sari.Id
        };
        var thread3 = new DiscussionThread
        {
            Title = "Tips memakai Clippy untuk kerja harian",
            Body = "Sudah coba tanya Clippy soal statistik portal dan hitung-hitungan? Ternyata bisa juga scrape halaman web. Share use case menarik kalian di sini!",
            CreatedById = editor.Id
        };
        db.DiscussionThreads.AddRange(thread1, thread2, thread3);
        await db.SaveChangesAsync();
        db.DiscussionPosts.AddRange(
            new DiscussionPost { ThreadId = thread1.Id, Body = "Sejauh ini mulus, cuma perlu update beberapa NuGet package. Performa Blazor Server terasa lebih cepat. @admin", CreatedById = editor.Id },
            new DiscussionPost { ThreadId = thread1.Id, Body = "Setuju! Jangan lupa cek kompatibilitas EF Core provider-nya juga.", CreatedById = viewer.Id },
            new DiscussionPost { ThreadId = thread1.Id, Body = "Kami sempat kena issue di serializer JSON — pastikan test menyeluruh sebelum production.", CreatedById = budi.Id },
            new DiscussionPost { ThreadId = thread2.Id, Body = "Vote outdoor! Sudah lama tidak hiking bareng 🏔️", CreatedById = rudi.Id },
            new DiscussionPost { ThreadId = thread2.Id, Body = "Workshop masak juga seru dan lebih inklusif untuk semua.", CreatedById = maya.Id },
            new DiscussionPost { ThreadId = thread3.Id, Body = "Saya pakai Clippy buat rangkum notulen townhall, tinggal paste link dokumennya. Hemat waktu banget.", CreatedById = andi.Id });

        // --- Calendar events ---
        var now = DateTime.Today;
        db.CalendarEvents.AddRange(
            new CalendarEvent { Title = "Townhall Meeting", Description = "Update kuartalan seluruh perusahaan", Location = "Aula Utama", Start = now.AddDays(3).AddHours(15), End = now.AddDays(3).AddHours(17), Color = "#1877f2", CreatedById = admin.Id, ReminderMinutes = 30 },
            new CalendarEvent { Title = "Sprint Planning", Description = "Planning sprint 24", Location = "Ruang Meeting IT", Start = now.AddDays(1).AddHours(9), End = now.AddDays(1).AddHours(11), Color = "#31a24c", SiteId = itSite.Id, CreatedById = editor.Id, ReminderMinutes = 15 },
            new CalendarEvent { Title = "Interview Kandidat DevOps", Location = "Online - Teams", Start = now.AddDays(5).AddHours(13), End = now.AddDays(5).AddHours(14), Color = "#f7b928", SiteId = hrSite.Id, CreatedById = sari.Id },
            new CalendarEvent { Title = "Peluncuran Kampanye Q3", Description = "Go-live kampanye digital", Start = now.AddDays(8), End = now.AddDays(9), AllDay = true, Color = "#f02849", SiteId = mktSite.Id, CreatedById = maya.Id },
            new CalendarEvent { Title = "Review Anggaran H2", Description = "Pembahasan realokasi anggaran semester 2", Location = "Ruang Finance", Start = now.AddDays(6).AddHours(10), End = now.AddDays(6).AddHours(12), Color = "#f7b928", SiteId = finSite.Id, CreatedById = rudi.Id, ReminderMinutes = 60 },
            new CalendarEvent { Title = "Patch Server Bulanan", Description = "Maintenance window — layanan internal down 22:00-01:00", Location = "Data Center", Start = now.AddDays(10).AddHours(22), End = now.AddDays(11).AddHours(1), Color = "#9360f7", SiteId = itSite.Id, CreatedById = budi.Id, ReminderMinutes = 1440 },
            new CalendarEvent { Title = "Team Building HR", Description = "Outdoor activity — lokasi menyusul", Start = now.AddDays(15), End = now.AddDays(16), AllDay = true, Color = "#31a24c", SiteId = hrSite.Id, CreatedById = sari.Id },
            new CalendarEvent { Title = "Deadline Laporan Pajak", Start = now.AddDays(12).AddHours(17), End = now.AddDays(12).AddHours(18), Color = "#f02849", SiteId = finSite.Id, CreatedById = rudi.Id, ReminderMinutes = 1440 });

        // --- Form templates ---
        var leaveFields = new List<FormField>
        {
            new() { Label = "Nama Lengkap", Type = "text", Required = true, Placeholder = "Nama sesuai KTP" },
            new() { Label = "Departemen", Type = "select", Required = true, Options = ["HR", "IT", "Marketing", "Finance"] },
            new() { Label = "Jenis Cuti", Type = "radio", Required = true, Options = ["Tahunan", "Sakit", "Melahirkan", "Penting"] },
            new() { Label = "Tanggal Mulai", Type = "date", Required = true },
            new() { Label = "Tanggal Selesai", Type = "date", Required = true },
            new() { Label = "Alasan", Type = "textarea", Placeholder = "Jelaskan alasan cuti" }
        };
        var fbFields = new List<FormField>
        {
            new() { Label = "Email", Type = "email", Required = true },
            new() { Label = "Rating Layanan", Type = "select", Required = true, Options = ["⭐", "⭐⭐", "⭐⭐⭐", "⭐⭐⭐⭐", "⭐⭐⭐⭐⭐"] },
            new() { Label = "Setuju dihubungi kembali", Type = "checkbox" },
            new() { Label = "Masukan", Type = "textarea", Required = true }
        };
        var reimburseFields = new List<FormField>
        {
            new() { Label = "Nama", Type = "text", Required = true },
            new() { Label = "Kategori Biaya", Type = "select", Required = true, Options = ["Transportasi", "Akomodasi", "Makan", "Peralatan", "Lainnya"] },
            new() { Label = "Nominal (Rp)", Type = "number", Required = true },
            new() { Label = "Tanggal Transaksi", Type = "date", Required = true },
            new() { Label = "Keterangan", Type = "textarea", Placeholder = "Detail pengeluaran dan nomor bukti" }
        };
        var helpdeskFields = new List<FormField>
        {
            new() { Label = "Nama Pelapor", Type = "text", Required = true },
            new() { Label = "Kategori", Type = "select", Required = true, Options = ["Hardware", "Software", "Jaringan", "Akun/Akses", "Lainnya"] },
            new() { Label = "Prioritas", Type = "radio", Required = true, Options = ["Rendah", "Sedang", "Tinggi", "Kritis"] },
            new() { Label = "Deskripsi Masalah", Type = "textarea", Required = true, Placeholder = "Jelaskan gejala, pesan error, dan langkah yang sudah dicoba" }
        };
        db.Forms.AddRange(
            new FormDefinition { Name = "Formulir Cuti", Description = "Pengajuan cuti karyawan", SchemaJson = JsonSerializer.Serialize(leaveFields), IsTemplate = true, CreatedById = admin.Id },
            new FormDefinition { Name = "Survey Kepuasan", Description = "Feedback layanan internal", SchemaJson = JsonSerializer.Serialize(fbFields), IsTemplate = true, CreatedById = admin.Id },
            new FormDefinition { Name = "Reimbursement", Description = "Klaim penggantian biaya", SchemaJson = JsonSerializer.Serialize(reimburseFields), IsTemplate = true, CreatedById = admin.Id },
            new FormDefinition { Name = "Tiket Helpdesk IT", Description = "Laporan gangguan IT", SchemaJson = JsonSerializer.Serialize(helpdeskFields), IsTemplate = true, CreatedById = budi.Id });

        // --- Welcome notifications ---
        foreach (var user in new[] { admin, editor, viewer, budi, sari, rudi, maya, andi })
            db.Notifications.Add(new Notification
            {
                UserId = user.Id, Title = "Selamat datang di BlazePoint!",
                Message = "Jelajahi dashboard, buka Intranet Home (/p/intranet-home), dan coba chatbot Clippy.",
                Link = "/p/intranet-home"
            });

        db.AuditLogs.Add(new AuditLog { Category = "System", Message = "Database seeded dengan sample data lengkap", UserName = "System" });

        await db.SaveChangesAsync();
    }
}
