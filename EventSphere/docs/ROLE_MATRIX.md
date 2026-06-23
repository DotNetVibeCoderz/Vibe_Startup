# 👥 Role Matrix — Access Control

## 6 User Roles

| Role | Deskripsi | Registrasi |
|------|-----------|-----------|
| **Admin** | Full system control | Via seeder only |
| **Organizer** | Event planner profesional | Via Admin |
| **Client** | Pemilik event | Self-register |
| **Vendor** | Penyedia jasa | Self-register |
| **Guest** | Tamu undangan | Self-register |
| **Moderator** | Forum manager | Via Admin |

---

## Menu Access Matrix

| Menu | Admin | Organizer | Client | Vendor | Guest | Moderator |
|------|:-----:|:---------:|:------:|:------:|:-----:|:---------:|
| 📊 Dashboard | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| 📅 Events | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 👥 Guests | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 💰 Budget | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| ✅ Tasks | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| 🏢 Vendors | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| 💬 Chat | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| 🔔 Notifications | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| 📁 Documents | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| 🪑 Seating Plan | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| 🖼️ Gallery | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ |
| 🤖 Tante Sherly | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| 💡 Forum | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| ⚙️ User Mgmt | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| 👤 Profile | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## Authorization Policies

Di `Program.cs`:
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("OrganizerAccess", p => p.RequireRole("Admin", "Organizer"));
    options.AddPolicy("ClientAccess", p => p.RequireRole("Admin", "Organizer", "Client"));
    options.AddPolicy("VendorAccess", p => p.RequireRole("Admin", "Organizer", "Vendor"));
    options.AddPolicy("AllUsers", p => p.RequireRole("Admin", "Organizer", "Client", "Vendor", "Guest", "Moderator"));
});
```

## Page-Level Authorization

| Page | Policy |
|------|--------|
| `/events/**` | `[Authorize]` |
| `/guests/**` | `[Authorize(Roles = "Admin,Organizer,Client")]` |
| `/budget/**` | `[Authorize(Roles = "Admin,Organizer,Client")]` |
| `/tasks/**` | `[Authorize]` |
| `/seating/**` | `[Authorize(Roles = "Admin,Organizer")]` |
| `/admin/**` | `[Authorize(Roles = "Admin")]` |

---

## Role-Specific Features

### Admin
- Full CRUD on all entities
- User management (create, edit, delete, role management)
- System-wide analytics
- Export all data

### Organizer
- Create & manage multiple events
- Assign vendors via contracts
- Coordinate with clients and vendors
- Seating plan design
- Budget oversight

### Client
- View event progress
- Approve budgets & vendors
- Submit feedback
- Access digital invitations
- View event gallery

### Vendor
- View assigned event details
- Upload documents & portfolios
- Update work status
- Submit invoices
- Track payments

### Guest
- Receive digital invitation
- RSVP online
- View seating arrangement
- Upload photos to event gallery
- Submit event feedback

### Moderator
- Manage forum content
- Pin/lock posts
- Filter inappropriate content
- Engage community
