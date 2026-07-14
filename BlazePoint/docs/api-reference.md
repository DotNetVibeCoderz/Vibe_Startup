# BlazePoint — API Reference

Base URL: `http://localhost:5112` (development). All `/api/*` endpoints except the ICS feed and share downloads require an authenticated session cookie (log in via `/auth/login`).

## Authentication (form-post endpoints)

| Method | Path | Body (form-urlencoded) | Notes |
|---|---|---|---|
| POST | `/auth/login` | `email`, `password`, `returnUrl` | Sets auth cookie, redirects |
| POST | `/auth/logout` | — | Clears session |
| POST | `/auth/register` | `email`, `displayName`, `password`, `confirmPassword` | New users get the Viewer role |
| POST | `/auth/forgot` | `email` | Demo mode returns the reset link in the redirect (no SMTP) |
| POST | `/auth/reset` | `email`, `token`, `password` | Completes password reset |

## Documents

| Method | Path | Description |
|---|---|---|
| GET | `/api/documents?folder=/&siteId=` | List documents in a folder |
| GET | `/api/documents/{id}` | Document detail incl. version history |
| GET | `/api/documents/{id}/download?version=` | Download current or specific version |
| POST | `/api/documents/upload` | Multipart: `file` + `folder`. Re-uploading the same name auto-versions |
| DELETE | `/api/documents/{id}` | Soft delete (recycle bin) |

Example:

```bash
curl -b cookies.txt -F "file=@report.pdf" -F "folder=/Finance" \
     http://localhost:5112/api/documents/upload
```

## Lists

| Method | Path | Description |
|---|---|---|
| GET | `/api/lists` | All list definitions (columns as JSON) |
| GET | `/api/lists/{id}/items` | Items of a list |
| POST | `/api/lists/{id}/items` | JSON body `{ "Column": value, ... }` creates an item |

## Pages, Search, Sites, Events

| Method | Path | Description |
|---|---|---|
| GET | `/api/pages` | Published CMS pages |
| GET | `/api/pages/{slug}` | Page detail incl. published webpart JSON |
| GET | `/api/search?q=…&mode=fulltext|semantic` | Search index query |
| GET | `/api/sites` | Team sites |
| GET | `/api/events?from=&to=` | Calendar events in range |
| GET | `/api/calendar/feed.ics` | **Anonymous** ICS feed — subscribe from Outlook/Google Calendar |
| GET | `/api/share/{token}/download` | **Anonymous** (if link is public) shared-file download; honors expiry |
| GET | `/api/files/{key}` | Raw storage access (chat attachments); authenticated |

## GraphQL

Endpoint: `POST /graphql` (Banana Cake Pop IDE available at `/graphql` in a browser during development).

Root query fields: `sites`, `documents(folder)`, `pages`, `lists`, `events(from, to)`, `discussions`.

```graphql
{
  sites { name slug department }
  documents(folder: "/") { name version size updatedAt }
  events { title start end allDay }
}
```

```bash
curl -X POST http://localhost:5112/graphql \
     -H "Content-Type: application/json" \
     -d '{"query":"{ sites { name slug } }"}'
```
