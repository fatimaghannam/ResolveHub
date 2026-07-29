# ResolveHub

<div align="center">

**A modern IT Help Desk and Ticket Management System**

![Project Status](https://img.shields.io/badge/Status-In%20Development-2563EB?style=for-the-badge)
![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=black)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-.NET%2010-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-Database-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)

[![JWT](https://img.shields.io/badge/JWT-Authentication-111827?logo=jsonwebtokens)](#authentication-and-security)
[![Swagger](https://img.shields.io/badge/OpenAPI-Swagger-85EA2D?logo=swagger&logoColor=black)](#api)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](#license)

</div>

ResolveHub is a full-stack support platform for recording, assigning, tracking, and resolving internal IT requests. It combines a responsive React interface with an ASP.NET Core Web API, SQL Server, Entity Framework Core, ASP.NET Core Identity, and JWT authentication.

> **Project status:** ResolveHub is under active development. Core authentication, ticket, draft, assignment, dashboard, user-status, and agent workflow APIs are implemented. Some supporting screens—including notification delivery and category editing—currently use demo data or placeholders and are identified below.

## Table of Contents

- [Project Overview](#project-overview)
- [Assignment 4 Deliverables](#assignment-4-deliverables)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Installation](#installation)
- [Demo Accounts](#demo-accounts)
- [API](#api)
- [Screenshots](#screenshots)
- [Responsive Design](#responsive-design)
- [Code Quality](#code-quality)
- [Future Improvements](#future-improvements)
- [License](#license)

## Project Overview

ResolveHub centralizes help-desk work that might otherwise be scattered across email, chat, spreadsheets, and verbal requests. Tickets receive a consistent `RH-YYYY-NNNN` reference and retain their requester, classification, assignment, status, comments, attachments, and history.

The application supports four roles:

| Role | Responsibility |
|---|---|
| **Employee** | Creates and manages personal tickets and drafts, uploads attachments, tracks progress, and reviews ticket details. |
| **IT Support Agent** | Works with tickets assigned to their account and uses protected APIs for status transitions, resolution summaries, comments, internal notes, history, and attachment downloads. |
| **Manager** | Monitors the wider ticket queue, manages personal tickets and drafts, assigns tickets, and reviews dashboards, workload, and activity. |
| **Administrator** | Monitors the system, manages assignments and account status, views users and activity, and accesses administrative ticket and dashboard tools. |

## Assignment 4 Deliverables

### Employee

- Dashboard with personal ticket counts
- Create and submit tickets
- Save, list, reopen, update, submit, and delete personal drafts
- View and edit eligible tickets
- Cancel tickets through the existing delete action
- Upload and manage ticket attachments
- Search and filter by status, category, priority, and date range
- Server-side pagination

### IT Support Agent

- Dashboard using assigned-ticket statistics
- Assigned Tickets list with search, filters, and pagination
- Ticket Details view
- Protected backend workflows for public comments and internal notes
- Protected status workflow, including starting progress and moving tickets to Pending
- Protected ticket resolution with a resolution summary
- Ticket history and authorized attachment downloads

> The Agent list and detail screens are connected to real APIs. The current detail UI still labels status updates, employee comments, and internal notes as “Coming soon”; their backend endpoints and service logic are implemented, but those controls are not yet exposed in that screen.

### Manager

- Dashboard
- Personal **My Tickets** workflow
- Create tickets and manage personal drafts
- View the authorized organization ticket list and ticket details
- Assign tickets to active IT Support Agents
- Ticket Assignments page
- Team Workload page
- Ticket Activity page
- Search, filtering, date ranges, and pagination
- Responsive shared dashboard layout

### Administrator

- Dashboard statistics, charts, workload summary, assignment queue, and quick actions
- Personal **My Tickets** workflow
- Create tickets and manage personal drafts
- View all authorized tickets and ticket details
- Assign and reassign tickets
- User listing, user details, and account activation/deactivation
- Category reference screen
- Activity Logs and demo notification screens
- Search, filtering, date ranges, and pagination
- Responsive shared dashboard layout

## Features

### Authentication and Security

- Email and password login through ASP.NET Core Identity
- JWT access tokens with a configuration-driven 24-hour lifetime
- Role-based frontend routes and backend authorization
- “Remember Me” storage selection between `localStorage` and `sessionStorage`
- Forgot-password and reset-password flows
- Resend password-reset email delivery
- 30-minute password-reset token lifetime
- Password policy and temporary account lockout
- Login and password-reset rate limiting
- Active-account checks at login and on authenticated requests
- Generic forgot-password responses that do not disclose whether an account exists
- HTTPS redirection, configured CORS, and centralized API exception handling

### Ticket Management

- Ticket creation with generated `RH-YYYY-NNNN` references
- Personal ticket listing and details
- Editing and cancellation where current status and ownership permit
- Categories, priorities, and status lookups
- Draft creation, update, submission, and deletion with server-side ownership checks
- File upload, download, and deletion with configurable limits
- Agent assignment and reassignment
- Status transitions and resolution summaries
- Public ticket comments and internal agent notes in the backend
- Ticket history records
- Search, multi-field filters, date ranges, and pagination

### Administration

- Administrator dashboard backed by ticket data
- Ticket-status, monthly created/resolved, and category charts
- Agent workload and unassigned-ticket summaries
- Active-agent assignment dropdown sourced from Identity roles
- User listing and account activation/deactivation
- Administrative activity records
- Category and notification presentation screens currently backed by demo data

### Manager Features

- Manager dashboard and ticket overview
- Organization ticket search and filtering
- Assignment to active IT Support Agents
- Agent workload/capacity overview
- Ticket activity feed
- Personal ticket and draft workflows shared with Employee and Administrator roles

### IT Agent Features

- Agent-specific dashboard counts
- Tickets restricted to the authenticated agent’s assignments
- Search, filtering, and pagination
- Secure ticket details and attachment access
- Status, resolution, comment, internal-note, and history APIs

### Notifications and Activity

- Administrator notification presentation uses the shared Development demo data source.
- Agent notifications currently display an API-pending empty state.
- Administrator and Manager activity views are available; the Manager activity view uses the backend manager API.
- Ticket operations create history/activity data where implemented by their services.

### Dashboards and Charts

- Employee and Agent workload statistics
- Manager operational statistics, priority overview, workload, and activity
- Administrator statistics and responsive Recharts visualizations
- Empty, loading, error, and retry states across API-connected pages

### Responsive Design

- Shared responsive dashboard shell and collapsible sidebar
- Mobile navigation drawer
- Responsive cards, forms, filters, dialogs, tables, and pagination
- Controlled horizontal table scrolling on smaller screens
- Responsive login, forgot-password, and reset-password pages
- Accessible labels, focus states, semantic headings, and reduced-motion support


`Cancelled` is a terminal status for eligible tickets that are no longer required. The backend seeds all seven lookup statuses: **Open**, **Assigned**, **In Progress**, **Pending**, **Resolved**, **Closed**, and **Cancelled**. `Closed` is available as a system status, but a Resolved-to-Closed action is not currently exposed by the frontend or ticket APIs.

## Tech Stack

### Frontend

| Technology | Purpose |
|---|---|
| React 19 | Component-based user interface |
| JavaScript | Frontend application logic |
| React Router 7 | Protected role-based routing |
| Native Fetch API | REST API communication |
| Vite 8 | Development server and production build |
| Recharts | Administrator dashboard charts |
| Lucide React | Interface icons |
| CSS | Shared design system and responsive layouts |

> The current frontend does **not** use Axios, Redux, Tailwind, Bootstrap, or TypeScript.

### Backend

| Technology | Purpose |
|---|---|
| ASP.NET Core Web API (.NET 10) | HTTP API and application host |
| Entity Framework Core 10 | SQL Server data access and migrations |
| ASP.NET Core Identity | Users, roles, passwords, and reset tokens |
| JWT Bearer Authentication | Stateless API authentication |
| Resend | Password-reset email delivery |
| OpenAPI and Swagger UI | Development API documentation |

### Database

- Microsoft SQL Server
- Entity Framework Core migrations
- Environment-aware production and Development/demo seeders

### Development Tools

- .NET CLI
- npm and Vite
- Swagger UI
- Git and GitHub
- Visual Studio, Visual Studio Code, or another compatible editor
- xUnit integration tests
- Oxlint frontend linting

## Project Structure

```text
ResolveHub/
├── backend/
│   ├── ResolveHub.sln
│   ├── src/ResolveHub.Api/
│   │   ├── Constants/
│   │   ├── Controllers/
│   │   ├── Data/
│   │   │   ├── Migrations/
│   │   │   └── Seed/
│   │   ├── DTOs/
│   │   ├── Entities/
│   │   ├── Infrastructure/
│   │   ├── Services/
│   │   │   ├── Implementations/
│   │   │   └── Interfaces/
│   │   ├── Settings/
│   │   └── Program.cs
│   └── tests/ResolveHub.Api.Tests/
├── frontend/
│   ├── public/
│   └── src/
│       ├── assets/
│       ├── components/
│       ├── data/
│       │   ├── demo/
│       │   ├── production/
│       │   └── shared/
│       ├── pages/
│       ├── services/
│       ├── styles/
│       └── utils/
├── database/
├── docs/
└── README.md
```

| Folder | Responsibility |
|---|---|
| `Controllers` | REST endpoints, authorization boundaries, and HTTP responses |
| `Services` | Authentication, ticket, draft, attachment, assignment, dashboard, and user-management logic |
| `DTOs` | Validated request models and API response contracts |
| `Entities` | Identity, ticket, lookup, comment, attachment, history, and activity persistence models |
| `Data` | `ApplicationDbContext`, EF Core migrations, and environment-aware seeders |
| `components` | Shared layouts, states, forms, badges, pagination, and chart components |
| `pages` | Route-level React screens for all four roles |
| `services` | Fetch-based API clients and authentication storage |
| `data` | Shared lookups plus isolated demo and production data sources |

## Installation

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server or SQL Server Express
- Node.js version supported by Vite 8 and npm
- Optional: `dotnet-ef` for manual migration commands

### 1. Clone the repository

```bash
git clone https://github.com/fatimaghannam/ResolveHub.git
cd ResolveHub
```

### 2. Configure backend secrets

The API project uses .NET User Secrets. Run these commands from the repository root:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR_SERVER;Database=ResolveHub;Trusted_Connection=True;TrustServerCertificate=True" --project backend/src/ResolveHub.Api
dotnet user-secrets set "Jwt:Key" "YOUR_BASE64_ENCODED_RANDOM_KEY" --project backend/src/ResolveHub.Api
dotnet user-secrets set "SeedData:DefaultPassword" "YOUR_STRONG_DEVELOPMENT_PASSWORD" --project backend/src/ResolveHub.Api
dotnet user-secrets set "Resend:ApiToken" "YOUR_RESEND_API_TOKEN" --project backend/src/ResolveHub.Api
```

Generate a suitable JWT key in PowerShell:

```powershell
$jwtKey = [Convert]::ToBase64String(
  [Security.Cryptography.RandomNumberGenerator]::GetBytes(64)
)
dotnet user-secrets set "Jwt:Key" $jwtKey --project backend/src/ResolveHub.Api
```

Optional password-reset test account:

```bash
dotnet user-secrets set "SeedData:PasswordResetTestEmail" "your.test.address@example.com" --project backend/src/ResolveHub.Api
```

Safe non-secret defaults—JWT issuer/audience, token lifetimes, frontend URL, Resend sender, CORS origins, and file limits—are already defined in `appsettings.json`.

### 3. Restore and prepare the database

```bash
dotnet restore backend/ResolveHub.sln
dotnet tool install --global dotnet-ef
dotnet ef database update --project backend/src/ResolveHub.Api
```

The application also applies pending migrations when it starts outside the test environment. In Development it then seeds roles, lookups, demo Identity users, assignments, and deterministic tickets. Production seeds only roles and lookup records.

### 4. Run the backend

```bash
dotnet run --project backend/src/ResolveHub.Api
```

Development URLs:

- API: `https://localhost:7188`
- Swagger UI: `https://localhost:7188/swagger`
- OpenAPI document: `https://localhost:7188/openapi/v1.json`

Trust the local HTTPS certificate if required:

```bash
dotnet dev-certs https --trust
```

### 5. Install and run the frontend

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` requests to `https://localhost:7188`.

### 6. Verification commands

```bash
dotnet build backend/ResolveHub.sln -c Release
dotnet test backend/ResolveHub.sln -c Release

cd frontend
npm run lint
npm run build
```

## Demo Accounts

The following real ASP.NET Identity accounts are seeded **only in Development**:

| Role | Display Name | Email |
|---|---|---|
| Administrator | Ryan Whitmore | `ryan.whitmore@resolvehub.test` |
| Manager | Lauren Prescott | `lauren.prescott@resolvehub.test` |
| IT Support Agent | Natalie Hayes | `natalie.hayes@resolvehub.test` |
| IT Support Agent | Emily Carter | `emily.carter@resolvehub.test` |
| IT Support Agent | Michael Thompson | `michael.thompson@resolvehub.test` |
| Employee | Ethan Brooks | `ethan.brooks@resolvehub.test` |

Additional fictional Employee accounts are seeded as ticket requesters for Development workflows. All seeded Development accounts use the password configured in the private `SeedData:DefaultPassword` User Secret; no password is stored in this repository.

## API

ResolveHub uses controller-based REST endpoints, DTO request/response contracts, dependency-injected services, EF Core queries, and role-based authorization attributes.

| Area | Representative endpoints |
|---|---|
| Authentication | `POST /api/auth/login`, `/forgot-password`, `/reset-password` |
| Employee tickets | `GET/POST /api/tickets`, ticket details, update, cancel, and comments |
| Drafts | `GET/POST /api/ticket-drafts`, update, delete, and submit |
| Attachments | Upload, authorized download, and delete under `/api/tickets/{id}/attachments` |
| Agent | `/api/agent/dashboard`, assigned tickets, status, resolution, comments, internal notes, and history |
| Manager | `/api/manager/dashboard`, tickets, assignments, workload, and activity |
| Administrator | `/api/admin/dashboard`, tickets, assignments, agents, and users |
| Lookups | `/api/ticket-categories`, `/api/ticket-priorities`, `/api/ticket-statuses` |

Swagger UI is enabled in Development and supports JWT Bearer authorization. Existing authentication evidence is available in [`docs/api-testing-screenshots`](docs/api-testing-screenshots/).

## Screenshots

The repository currently includes design wireframes and API test evidence. Add final application captures to `docs/screenshots/` using the following checklist:

| Screen | Suggested file |
|---|---|
| Login | `docs/screenshots/login.png` |
| Employee Dashboard | `docs/screenshots/employee-dashboard.png` |
| Employee Tickets | `docs/screenshots/employee-tickets.png` |
| Create Ticket | `docs/screenshots/create-ticket.png` |
| Agent Dashboard | `docs/screenshots/agent-dashboard.png` |
| Administrator Dashboard | `docs/screenshots/admin-dashboard.png` |
| Manager Dashboard | `docs/screenshots/manager-dashboard.png` |
| Ticket Assignments | `docs/screenshots/assignments.png` |
| Categories | `docs/screenshots/categories.png` |
| Users | `docs/screenshots/users.png` |

<!-- Replace this comment with Markdown image links after final screenshots are added. -->

UI wireframes can be reviewed in [`docs/ui-wireframes-draft`](docs/ui-wireframes-draft/).

## Responsive Design

ResolveHub uses fluid grids, flexible widths, responsive breakpoints, viewport-aware navigation, and scrollable table containers. The interface is designed and manually reviewed for:

- Desktop monitors
- Laptop screens
- Tablet layouts, including iPad-sized viewports
- Mobile phones
- Short-height windows and browser zoom

The application preserves readable controls, accessible focus states, touch-friendly actions, and safe overflow behavior across these layouts.

## Code Quality

Verified repository quality controls include:

- Nullable reference types enabled in the backend
- DTO validation and centralized exception handling
- Dependency injection and provider-focused service interfaces
- Role and ownership checks enforced on the server
- xUnit integration tests for authentication, password reset, ticket workflows, filtering, roles, drafts, assignments, and account status
- Frontend linting through Oxlint
- Successful backend and frontend production builds

Current verification commands:

```bash
dotnet build backend/ResolveHub.sln -c Release
dotnet test backend/ResolveHub.sln -c Release
cd frontend
npm run lint
npm run build
```

## Future Improvements

- Connect the remaining demo/placeholder Administrator data screens to backend APIs
- Add Agent detail controls for the already implemented status, comment, and internal-note APIs
- Real-time in-app notifications
- Email-to-ticket ingestion
- Searchable knowledge base
- SLA policies, timers, and escalation monitoring
- Dark mode
- Expanded analytics and exportable reports
- Refresh-token strategy for longer-lived production sessions

## License

ResolveHub is licensed under the [MIT License](LICENSE).

Copyright © 2026 Fatima Ghannam.
