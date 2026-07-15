# Security & Governance

## Authentication

ASP.NET Core Identity with cookie auth (login/register/2FA/passkeys from the standard Identity UI
under `/Account/*`). Machine-to-machine access uses **API keys** (`Api:Keys` in appsettings,
sent as `X-Api-Key`); each key maps to a tenant and acts as a Developer-role principal.

> Change the seeded API key (`dev-api-key-change-me`) and sample-user passwords before any
> non-local deployment.

## Roles (RBAC)

| Role | Capabilities |
|---|---|
| **Admin** | everything + user management + settings |
| **Developer** | Data Hub, form/workflow/connector designers, AI Studio, monitoring, import/export |
| **EndUser** | run published apps, fill forms, browse entity data, chat with App Guru |

Pages are guarded with `[Authorize(Roles = ...)]`; published apps additionally declare a
`RequiredRole` (or `AllowAnonymous`) checked at render time.

## Multi-tenancy

Every tenant-scoped table carries a `TenantId`. The scoped `ITenantContext` is resolved from the
authenticated user's `org` claim (middleware for HTTP/API, layout initialization for Blazor
circuits) and every service filters queries by it. Public app URLs (`/a/{slug}`) execute under
the app owner's tenant.

## Audit logs

`AuditService` records create/update/delete/publish/rollback/submit/export/import/sync for
entities, records, forms, workflows, apps, connectors, and users — visible at
**Monitoring → Audit Logs** with type filtering.

## Versioning & rollback

Every save of an entity/form/workflow (and every app publish) writes an immutable
`VersionSnapshot` (full JSON + author + comment). The entity designer offers one-click restore;
the same mechanism backs the workflow/app history.

## Hardening checklist for production

- [ ] Replace `Api:Keys` and sample-user passwords
- [ ] Serve over HTTPS with a real certificate (HSTS is already enabled outside Development)
- [ ] Move SMTP/AI/Tavily secrets to environment variables or user-secrets
      (e.g. `AI__Providers__openai__ApiKey`)
- [ ] Restrict `/swagger` if the API should not be publicly documented
- [ ] Review workflow scripting: Run C#/JS/Python executes designer-authored code on the server —
      restrict the Developer role to trusted users
- [ ] Back up `appbender.db` and the `storage/` folder (or move to PostgreSQL/S3, see deployment)
