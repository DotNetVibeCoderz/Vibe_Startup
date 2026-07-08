# PadelHub - Arsitektur Aplikasi

## Overview

PadelHub dibangun dengan arsitektur **Blazor Server** menggunakan pola **N-Tier** yang terstruktur.

```
┌─────────────────────────────────────────────┐
│              Browser (Client)               │
│          SignalR WebSocket Connection        │
├─────────────────────────────────────────────┤
│           Blazor Server (.NET 10)            │
│  ┌─────────┐ ┌──────────┐ ┌──────────────┐ │
│  │ Razor   │ │ MudBlazor│ │  Components  │ │
│  │ Pages   │ │   UI     │ │   (Shared)   │ │
│  └────┬────┘ └──────────┘ └──────────────┘ │
│       │                                      │
│  ┌────┴───────────────────────────────────┐ │
│  │         Services Layer                 │ │
│  │  ┌──────────┐ ┌────────┐ ┌─────────┐  │ │
│  │  │ Business │ │Export  │ │ Payment │  │ │
│  │  │ Logic    │ │Service │ │Service  │  │ │
│  │  └──────────┘ └────────┘ └─────────┘  │ │
│  └────┬───────────────────────────────────┘ │
│       │                                      │
│  ┌────┴───────────────────────────────────┐ │
│  │         Data Access Layer              │ │
│  │    AppDbContext (EF Core)              │ │
│  │    SQLite / SQLServer / MySQL / PG     │ │
│  └────────────────────────────────────────┘ │
│                                              │
│  ┌────────────────────────────────────────┐ │
│  │         Minimal API Layer              │ │
│  │    /api/players, /api/clubs, etc.      │ │
│  │    Swagger Documentation               │ │
│  └────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
```

## Database Schema

### Core Tables
- **AspNetUsers** (Identity) + ApplicationUser
- **Clubs** - Club information
- **Courts** - Court/padel field data
- **Facilities** - Club facilities
- **OperatingHours** - Club operating hours

### Business Tables
- **Reservations** - Court bookings
- **Payments** - Payment transactions
- **MembershipPackages** - Membership plans
- **UserMemberships** - User membership subscriptions
- **LoyaltyPoints** - Loyalty point tracking

### Player & Coach
- **PlayerProfiles** - Player data
- **PlayerStats** - Match statistics
- **PlayerAchievements** - Player awards
- **Coaches** - Coach profiles
- **TrainingSessions** - Training bookings
- **CourseMaterials** - Training materials

### Tournament
- **Tournaments** - Tournament data
- **TournamentRegistrations** - Registrations
- **Matches** - Match data
- **MatchPlayers** - Player/match relationship

### Social
- **TimelinePosts** - Social posts
- **TimelineComments** - Post comments
- **TimelineLikes** - Post likes/reactions
- **ChatMessages** - Chat messages
- **ChatGroups** - Chat groups
- **ForumTopics/Posts** - Forum discussions
- **SocialEvents** - Community events

### System
- **AuditLogs** - Activity tracking
- **Badges/UserBadges** - Gamification
- **SensorData** - IoT data
- **IoTSimulators** - IoT simulation config
- **SystemConfigs** - System settings

## Design Patterns

- **Repository Pattern**: EF Core DbContext sebagai repository
- **Dependency Injection**: Semua services di-inject via DI container
- **Service Layer**: Business logic terpisah dari UI
- **Fluent API**: Konfigurasi model via Fluent API di DbContext
- **Cascading Parameters**: Untuk dialog dan state management
