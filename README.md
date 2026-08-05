# ResolveHub

<div align="center">

**A full-stack IT Help Desk and Ticket Management System**

![Project Status](https://img.shields.io/badge/Status-In%20Development-2563EB?style=for-the-badge)
![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=black)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-.NET%2010-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-Database-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)

[![JWT](https://img.shields.io/badge/JWT-Authentication-111827?logo=jsonwebtokens)](#authentication-and-security)
[![Swagger](https://img.shields.io/badge/OpenAPI-Swagger-85EA2D?logo=swagger&logoColor=black)](#api-overview)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](#license)

</div>

ResolveHub is a role-based platform for recording, assigning, tracking, and resolving internal IT support requests. It replaces scattered requests from email, chat, spreadsheets, and verbal communication with a centralized and auditable ticket workflow.

The application combines a responsive React frontend with an ASP.NET Core Web API, SQL Server, Entity Framework Core, ASP.NET Core Identity, and JWT authentication.

> **Project status:** ResolveHub is under active development. Its core authentication, ticket management, assignment approval, commenting, attachment, category management, activity tracking, audit logging, and user-management workflows are implemented.

## Table of Contents

- [Project Overview](#project-overview)
- [Core Features](#core-features)
- [User Roles](#user-roles)
- [Ticket Workflow](#ticket-workflow)
- [Assignment Workflow](#assignment-workflow)
- [Duplicate Review Workflow](#duplicate-review-workflow)
- [Activity Tracking and Audit Logs](#activity-tracking-and-audit-logs)
- [Authentication and Security](#authentication-and-security)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [License](#license)

## Project Overview

ResolveHub provides a structured workflow for managing IT support requests from submission to closure.

Each ticket receives a unique reference in the following format:

```text
RH-YYYY-NNNN
```

A ticket retains its:

- Requester and department
- Category and priority
- Current status
- Assigned IT Support Agent
- Description and resolution summary
- Ticket attachments
- Public/Private comments and replies 
- Ticket history
- Activity timeline
- Work-duration information
- Assignment and duplicate-review records

Access is controlled by role at both the frontend routing level and the backend API level.

## Core Features

- Role-based dashboards for Employees, IT Support Agents, Managers, and Administrators
- Ticket creation, editing, cancellation, filtering, searching, and pagination
- Personal ticket drafts for Employees and Administrators
- File attachments for tickets and comments
- Threaded comments and replies
- Public and Private comments
- Controlled ticket-status transitions
- Direct Administrator assignment and Manager assignment approval workflow
- Maximum IT Support Agent capacity of five active tickets
- Duplicate-ticket reporting and review
- Ticket history and detailed activity timelines
- Work-session and total work-duration tracking
- System audit log shared by Administrators and Managers
- User creation, invitation, activation, and deactivation
- Ticket-category creation, editing, activation, and deactivation
- Administrator and Manager notifications
- Responsive layouts, tables, forms, dialogs, filters, and pagination


## User Roles

### Employee

Employees create and track their own IT support requests.

- View personal dashboard statistics
- Create and submit tickets
- Save incomplete tickets as drafts
- Continue, update, submit, or delete personal drafts
- View and search personal tickets
- Filter by status, category, priority, and date range
- Edit eligible tickets while their workflow state permits changes
- Cancel an eligible owned ticket before assignment
- Upload and manage ticket attachments
- Add public comments and replies
- Upload attachments to comments
- Edit or delete eligible personal comments
- Review ticket history and status progress

### IT Support Agent

IT Support Agents work on tickets assigned to their accounts.

- View agent-specific dashboard statistics
- View assigned tickets
- View the open unassigned-ticket queue
- View previously assigned ticket history through the backend workflow
- Access authorized ticket details and attachments
- Start work on assigned tickets
- Move tickets to Pending with a reason
- Resume work on Pending tickets
- Resolve tickets with a resolution summary
- Close eligible resolved tickets
- Add public and private (The one who created the ticket with the IT agent working on it) comments 
- Reply to, edit, and delete eligible comments
- Upload and download comment attachments
- Review ticket history and activity records
- Track active and completed work duration
- Search, filter, and paginate ticket lists

IT Support Agents cannot assign tickets to themselves or create assignment approval requests.

### Manager

Managers oversee organization-wide ticket operations and team workload.

- View operational dashboard statistics
- View authorized organization tickets and ticket details
- Search and filter tickets
- Monitor unassigned and active tickets
- Select an IT Support Agent for a ticket
- Submit assignment requests for Administrator approval
- Review submitted assignment-request statuses
- Monitor agent workload, maximum capacity, and remaining capacity
- Report possible duplicate tickets for Administrator review
- Add comments and replies to authorized tickets
- Upload and access authorized comment attachments
- View ticket history and activity information
- Monitor ticket work duration
- Receive and manage notifications
- Access the same system audit log available to Administrators

Managers cannot directly complete an assignment. The ticket is assigned only after an Administrator approves the request.

### Administrator

Administrators have full operational and account-management access.

- View system-wide dashboard statistics and charts
- Create and manage personal tickets and drafts
- View all authorized tickets and ticket details
- Assign and reassign tickets directly
- Review Manager assignment requests
- Approve or reject assignment requests
- Monitor IT Support Agent workload and capacity
- Review possible duplicate-ticket reports
- Approve or reject duplicate reviews
- Mark a ticket as a duplicate directly
- Add comments, replies, and authorized internal communication
- Manage ticket and comment attachments
- Create user accounts and send account invitations
- Resend account invitations
- View user details
- Activate or deactivate user accounts
- Create and edit ticket categories
- Activate or deactivate ticket categories
- View notifications
- Review ticket history and activity records
- Access the complete system audit log

## Ticket Workflow

ResolveHub uses the following primary ticket lifecycle:

```text
Open → Assigned → In Progress ⇄ Pending → Resolved → Closed
```

Additional terminal or review outcomes include:

- **Cancelled:** An eligible ticket is no longer required and is cancelled by its owner.


### Status meanings

| Status | Meaning |
|---|---|
| **Open** | The ticket has been submitted and is waiting for assignment. |
| **Assigned** | The ticket has been assigned to an IT Support Agent. |
| **In Progress** | The assigned agent is actively working on the ticket. |
| **Pending** | Work is temporarily paused while waiting for information, access, approval, or another dependency. |
| **Resolved** | The technical issue has been resolved and a resolution summary has been recorded. |
| **Closed** | The ticket workflow has been completed. |
| **Cancelled** | The ticket was withdrawn before completion. |

Ticket operations are validated by the backend so users cannot bypass ownership, assignment, capacity, or workflow rules through direct API requests.

## Assignment Workflow

ResolveHub supports two controlled assignment paths.

### Administrator assignment

1. The Administrator opens the assignment interface.
2. The Administrator selects an active IT Support Agent.
3. The backend checks the agent's current active-ticket capacity.
4. The ticket is assigned immediately when the request is valid.
5. Ticket history, activity, notifications, and audit records are updated.

### Manager assignment request

1. The Manager opens the same assignment interface and selects an IT Support Agent.
2. ResolveHub creates an assignment request instead of assigning the ticket immediately.
3. The ticket remains pending Administrator review.
4. The Administrator approves or rejects the request.
5. On approval, the backend checks capacity again and assigns the ticket to the selected agent.
6. On rejection, the ticket returns to the appropriate unassigned workflow.

### Agent capacity rule

An IT Support Agent may have a maximum of **five active tickets**.

Counted as active:

- Assigned
- In Progress
- Pending

Excluded from active capacity:

- Resolved
- Closed
- Cancelled
- Duplicate

Capacity is validated from the current database state during assignment and reassignment. Full agents are unavailable for additional assignments, and invalid direct requests receive a conflict response.

## Duplicate Review Workflow

ResolveHub provides a controlled process for handling suspected duplicate tickets.

### Manager-reported duplicate

1. The Manager selects the suspected duplicate ticket.
2. The Manager identifies the possible original ticket and may provide a reason.
3. ResolveHub creates a duplicate-review request.
4. The Administrator reviews the request.
5. The Administrator approves or rejects it.
6. If approved, the duplicate is linked to the original ticket and removed from the active workflow while preserving its records.
7. If rejected, the ticket continues through its normal workflow.

### Administrator direct action

An Administrator may directly mark a confirmed ticket as a duplicate by referencing the original ticket.

Duplicate actions preserve relevant ticket history, activity, and audit information.

## Activity Tracking and Audit Logs

ResolveHub separates ticket-level operational history from system-level auditing.

### Ticket history

Ticket history records important changes such as:

- Ticket creation
- Assignment and reassignment
- Status changes
- Cancellation
- Resolution and closure
- Duplicate-review outcomes

### Ticket activity timeline

Authorized users can view a dedicated activity timeline for each ticket. Activity summaries include ticket context and total recorded work duration.

Work sessions are managed as agents move tickets through active workflow states:

- Moving into **In Progress** starts a work session.
- Moving from **In Progress** to **Pending** closes the current session.
- Resuming work starts a new session.
- Resolving, closing, or reassigning a ticket closes an active session when required.
- Total work duration includes completed sessions and the currently open session.

### System audit log

Administrators and Managers can access the same System Audit Log.

The log supports:

- Search by action, user, ticket, category, or details
- Standard and custom date ranges
- Pagination
- Performer name and role
- Action and action category
- Related entity information
- Previous and new values where available
- Links to authorized related records

The audit log records important administrative, security, and system-level actions across ResolveHub.

## Authentication and Security

- ASP.NET Core Identity user and role management
- JWT Bearer authentication
- Configuration-driven 24-hour access-token lifetime
- Role-based frontend route protection
- Role-based backend authorization
- Active-account validation during authentication and protected requests
- Remember Me support using `localStorage` or `sessionStorage`
- Forgot-password and reset-password workflows
- Password-reset email delivery through Resend
- 30-minute password-reset token lifetime
- Generic forgot-password response to reduce account enumeration
- Password policy enforcement
- Temporary account lockout after repeated failed login attempts
- Login, forgot-password, and reset-password rate limiting
- No-cache authentication responses
- HTTPS redirection
- Configured CORS origins
- Centralized API exception handling
- Authorization checks for ticket, comment, draft, and attachment access
- Sensitive configuration stored through .NET User Secrets during development

## Technology Stack

### Frontend

| Technology | Purpose |
|---|---|
| React 19 | Component-based user interface |
| JavaScript | Frontend application logic |
| React Router 7 | Protected role-based routing |
| Native Fetch API | REST API communication |
| Vite 8 | Development server and production build |
| Recharts | Dashboard charts and data visualization |
| Lucide React | Interface icons |
| CSS | Shared design system and responsive layouts |
| Oxlint | Frontend linting |

### Backend

| Technology | Purpose |
|---|---|
| ASP.NET Core Web API (.NET 10) | HTTP API and application host |
| Entity Framework Core 10 | SQL Server data access and migrations |
| ASP.NET Core Identity | Users, roles, passwords, and account security |
| JWT Bearer Authentication | Stateless API authentication |
| Resend | Password-reset and account-invitation email delivery |
| OpenAPI and Swagger UI | API documentation and testing |
| xUnit | Backend automated testing |

### Database

- Microsoft SQL Server
- Entity Framework Core migrations
- ASP.NET Core Identity tables
- Ticket, comment, attachment, history, activity, notification, assignment-request, duplicate-review, category, department, and work-session data

## Project Structure

```text
ResolveHub/
├── backend/
│   ├── ResolveHub.sln
│   ├── src/
│   │   └── ResolveHub.Api/
│   │       ├── Constants/
│   │       ├── Controllers/
│   │       ├── Data/
│   │       │   ├── Migrations/
│   │       │   └── Seed/
│   │       ├── DTOs/
│   │       ├── Entities/
│   │       ├── Infrastructure/
│   │       ├── Services/
│   │       │   ├── Implementations/
│   │       │   └── Interfaces/
│   │       ├── Settings/
│   │       ├── Program.cs
│   │       └── appsettings.json
│   └── tests/
│       └── ResolveHub.Api.Tests/
├── frontend/
│   ├── public/
│   ├── src/
│   │   ├── assets/
│   │   ├── components/
│   │   ├── data/
│   │   ├── pages/
│   │   ├── services/
│   │   ├── styles/
│   │   ├── utils/
│   │   ├── App.jsx
│   │   └── main.jsx
│   ├── package.json
│   └── vite.config.js
├── database/
├── docs/
├── LICENSE
└── README.md
```

| Folder | Responsibility |
|---|---|
| `Controllers` | REST endpoints, authorization boundaries, and HTTP responses |
| `Services` | Authentication, tickets, drafts, assignments, comments, categories, notifications, auditing, and user-management logic |
| `DTOs` | Validated API request and response contracts |
| `Entities` | Identity and application persistence models |
| `Data` | Database context, migrations, configuration, and seed data |
| `Infrastructure` | Cross-cutting backend infrastructure |
| `components` | Reusable layouts, forms, states, tables, badges, dialogs, and pagination |
| `pages` | Route-level pages for all four roles |
| `services` | Fetch-based frontend API clients and authentication storage |
| `utils` | Date, time, filtering, and shared frontend utilities |

## License

ResolveHub is licensed under the [MIT License](LICENSE).

Copyright © 2026 Fatima Ghannam.
