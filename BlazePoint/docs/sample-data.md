# BlazePoint — Sample Data & Users

Seeded automatically on first run (`Data/DbSeeder.cs`). To re-seed, stop the app and delete `src/BlazePoint/App_Data/`.

## Users (password semua akun: `Blaze123!`)

| Nama | Email | Role |
|---|---|---|
| Admin BlazePoint | `admin@blazepoint.local` | **Admin** |
| Eka Editor | `editor@blazepoint.local` | Editor |
| Budi Santoso | `budi@blazepoint.local` | Editor |
| Sari Dewi | `sari@blazepoint.local` | Editor |
| Vina Viewer | `viewer@blazepoint.local` | Viewer |
| Rudi Hartono | `rudi@blazepoint.local` | Viewer |
| Maya Putri | `maya@blazepoint.local` | Viewer |
| Andi Wijaya | `andi@blazepoint.local` | Viewer |

## Team Sites

| Site | Slug | Departemen |
|---|---|---|
| 👥 Human Resources | `/sites/hr` | HR |
| 💻 IT Department | `/sites/it` | IT |
| 📣 Marketing | `/sites/marketing` | Marketing |
| 💰 Finance | `/sites/finance` | Finance |

## Documents (file nyata di storage)

| Dokumen | Folder | Site |
|---|---|---|
| Panduan-Onboarding.md | `/` | HR |
| Kebijakan-Cuti-2026.md | `/` | HR |
| Notulen-Townhall-Juli.md | `/` | — |
| Standar-Keamanan-IT.md | `/IT` | IT |
| Arsitektur-Sistem-Internal.md | `/IT` | IT |
| Brand-Guideline-Ringkas.md | `/Marketing` | Marketing |
| Prosedur-Reimbursement.txt | `/Finance` | Finance |

Semua dokumen punya metadata (kategori, tag) dan bisa dicari via full-text/semantik.

## Custom Lists

- **🖥️ IT Assets** (site IT) — 6 aset dengan kolom Category/Owner/Price/InUse
- **📇 Kontak Karyawan** (site HR) — 5 kontak
- **🎯 Project Tracker** — 4 proyek dengan Status/Deadline/Progress

## CMS Pages

| Halaman | URL | Layout | Isi |
|---|---|---|---|
| Welcome to BlazePoint | `/p/welcome` | Intranet | Text, Clock, Weather, Calculator, Map |
| Company News | `/p/news` | News | Text, RSS feed |
| **Intranet Home** | `/p/intranet-home` | Intranet | **Showcase webpart SharePoint-style**: Hero, QuickLinks, News, People, Countdown, Events, ListView, Activity, Gallery, Button |
| IT Knowledge Base | `/p/it-kb` | Web | Text, Video, Divider, ListView |

## Workflows

- **Document Approval** — Start → Review (Editor) → Persetujuan (Admin) → Notify → End
- **Leave Approval** — Start → Verifikasi HR → Condition (`durasi=panjang`?) → Direksi / langsung Notify → End

## Lainnya

- 8 event kalender (townhall, sprint planning, maintenance window, dll) tersebar 2 minggu ke depan
- 3 thread diskusi dengan balasan dan @mention
- 4 template form: Formulir Cuti, Survey Kepuasan, Reimbursement, Tiket Helpdesk IT
- Notifikasi selamat datang untuk semua user
