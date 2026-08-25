# ResolveHub

### Full-Stack IT Help Desk & Ticket Management System

ResolveHub is a full-stack IT support platform designed to centralize, organize, and track internal technical support requests from submission through resolution.

Instead of relying on scattered requests across email, chat, spreadsheets, or verbal communication, ResolveHub provides a structured and auditable workflow where Employees, IT Support Agents, Managers, and Administrators interact through clearly defined responsibilities and permissions.

The system combines a responsive React frontend, an ASP.NET Core Web API, SQL Server, role-based authentication, reporting and analytics, workflow automation, and an AI-powered support assistant.

---

## Table of Contents

* [Project Overview](#project-overview)
* [Key Features](#key-features)
* [System Architecture](#system-architecture)
* [User Roles](#user-roles)
* [Ticket Lifecycle](#ticket-lifecycle)
* [Assignment Workflow](#assignment-workflow)
* [Duplicate Ticket Workflow](#duplicate-ticket-workflow)
* [Cancellation Workflow](#cancellation-workflow)
* [AI Assistant](#ai-assistant)
* [Reports and Analytics](#reports-and-analytics)
* [Activity Tracking and Audit Logs](#activity-tracking-and-audit-logs)
* [Authentication and Security](#authentication-and-security)
* [Technology Stack](#technology-stack)
* [Database](#database)
* [Project Structure](#project-structure)
* [Testing and Performance Validation](#testing-and-performance-validation)
* [Running the Project Locally](#running-the-project-locally)
* [Deployment](#deployment)
* [Future Enhancements](#future-enhancements)
* [License](#license)

---

# Project Overview

ResolveHub manages the complete lifecycle of an internal IT support request.

Each submitted ticket receives a unique identifier using the following format:

```text
RH-YYYY-NNNN
```

A ticket can contain:

* Requester information
* Department
* Category
* Priority
* Current status
* Assigned IT Support Agent
* Problem description
* Resolution summary
* Ticket attachments
* Public and private comments
* Threaded replies
* Comment attachments
* Ticket history
* Activity timeline
* Work-duration information
* Assignment records
* Duplicate-review records
* Cancellation information

Access to these operations is controlled through both frontend route protection and backend authorization.

The backend remains responsible for enforcing business rules even if a request is sent directly to the API.

---

# Key Features

## Ticket Management

* Create and submit IT support tickets
* Unique ticket reference generation
* Edit eligible tickets
* Search tickets
* Filter by status
* Filter by category
* Filter by priority
* Filter by date range
* Pagination for large ticket collections
* Ticket attachments
* Detailed ticket information
* Ticket history
* Ticket activity timeline
* Resolution summaries
* Controlled cancellation
* Duplicate-ticket handling

## Role-Based Access Control

ResolveHub supports four roles:

* Employee
* IT Support Agent
* Manager
* Administrator

Each role has its own:

* Dashboard
* Navigation
* Available actions
* Ticket permissions
* Workflow responsibilities
* API authorization rules

## Communication

* Public ticket comments
* Privatecomments where authorized
* Threaded replies
* Comment editing
* Comment deletion
* File attachments on comments
* Role-aware comment visibility

## Workflow Management

* Controlled ticket-status transitions
* Administrator direct assignment
* Manager assignment-request workflow
* IT Agent workload limits
* Duplicate review and approval
* Cancellation requests
* Ticket history tracking
* Work-session tracking
* Notifications

## Administration

* User management
* User account creation
* Account invitations
* Invitation resend
* User activation and deactivation
* Ticket-category management
* Category activation and deactivation
* System-wide audit logging

## Reporting

* Dashboard metrics
* Ticket statistics
* Date-range filtering
* Created vs. resolved ticket analysis
* Category distribution
* Priority distribution
* IT Agent workload statistics
* Charts and visual analytics
* PDF report generation
* Excel report generation
* Filter-aware report exports

## User Experience

* Responsive layouts
* Role-specific interfaces
* Dark mode
* Searchable and filterable tables
* Pagination
* Dialog-based workflows
* Loading and empty states
* Notification interfaces
* Consistent date and time formatting

---

# System Architecture

ResolveHub follows a separated frontend/backend architecture.

```text
┌───────────────────────────────┐
│        React Frontend         │
│                               │
│ Dashboards • Forms • Reports  │
│ Tickets • AI Assistant • UI   │
└───────────────┬───────────────┘
                │
                │ HTTPS / REST API
                │ JWT Authentication
                ▼
┌───────────────────────────────┐
│      ASP.NET Core Web API     │
│                               │
│ Controllers                   │
│ Services                      │
│ Authentication                │
│ Authorization                 │
│ Business Rules                │
│ Reporting                     │
│ AI Integration                │
└───────────────┬───────────────┘
                │
                │ Entity Framework Core
                ▼
┌───────────────────────────────┐
│          SQL Server           │
│                               │
│ Users • Tickets • Comments    │
│ History • Audit • Workflows   │
└───────────────────────────────┘
```

External services are used where appropriate for functionality such as email delivery and AI assistance.

### Request Flow

A typical request follows this path:

```text
User Action
    ↓
React Component
    ↓
Frontend API Service
    ↓
HTTP Request + JWT
    ↓
ASP.NET Core Controller
    ↓
Authorization
    ↓
Application Service
    ↓
Business Rule Validation
    ↓
Entity Framework Core
    ↓
SQL Server
    ↓
HTTP Response
    ↓
Updated React Interface
```

This separation keeps presentation logic, application logic, authorization, and persistence responsibilities clearly divided.

---

# User Roles

## Employee

Employees primarily create and monitor their own IT support requests.

Employees can:

* View personal dashboard statistics
* Create tickets
* Save incomplete tickets as drafts
* Continue saved drafts
* Delete eligible drafts
* Submit drafts as tickets
* View personal tickets
* Search personal tickets
* Filter tickets
* Edit eligible tickets
* Cancel eligible tickets
* Upload ticket attachments
* Add public comments
* Reply to comments
* Upload comment attachments
* Edit eligible personal comments
* Delete eligible personal comments
* Review ticket history
* Monitor ticket progress

Employees cannot access administrative or organization-wide management functionality.

---

## IT Support Agent

IT Support Agents are responsible for diagnosing and resolving assigned technical issues.

Agents can:

* View an agent-specific dashboard
* View assigned tickets
* View available unassigned tickets where authorized
* Review ticket details
* Access authorized attachments
* Start working on assigned tickets
* Move tickets to `In Progress`
* Move tickets to `Pending` with a reason
* Resume pending work
* Resolve tickets with a resolution summary
* Close eligible resolved tickets
* Add public comments
* Add authorized private comments
* Reply to comments
* Upload comment attachments
* Review ticket history
* Review ticket activity
* Track work duration
* Search and filter ticket lists
* Submit eligible workflow requests

Work performed by agents is reflected in ticket activity and work-session records.

---

## Manager

Managers oversee ticket operations and IT team workload.

Managers can:

* View management dashboard statistics
* View organization-wide authorized tickets
* Search and filter tickets
* Monitor open and active tickets
* Monitor unassigned tickets
* View IT Agent workload
* Review agent capacity
* Select agents for ticket assignment
* Submit assignment requests
* Monitor assignment-request status
* Report possible duplicate tickets
* Participate in authorized ticket discussions
* Review ticket history
* Review activity timelines
* Monitor ticket work duration
* Review notifications
* Access system audit information
* Access operational reports

A Manager does not directly bypass controlled administrative approval where approval is required by the workflow.

---

## Administrator

Administrators have the highest operational privileges within ResolveHub.

Administrators can:

* View system-wide dashboard statistics
* View charts and reports
* Create tickets
* Manage personal drafts
* View authorized system tickets
* Directly assign tickets
* Reassign tickets
* Review Manager assignment requests
* Approve assignment requests
* Reject assignment requests
* Monitor agent workload
* Review capacity
* Review suspected duplicate tickets
* Approve duplicate reports
* Reject duplicate reports
* Directly mark confirmed duplicates
* Participate in authorized ticket communication
* Manage attachments
* Create users
* Send account invitations
* Resend invitations
* View user information
* Activate users
* Deactivate users
* Create ticket categories
* Update ticket categories
* Activate categories
* Deactivate categories
* Review system notifications
* Review ticket history
* Review activity records
* Access audit logs
* Generate operational reports

---

# Ticket Lifecycle

ResolveHub uses a controlled ticket lifecycle:

```text
Open
  ↓
Assigned
  ↓
In Progress
  ↕
Pending
  ↓
Resolved
  ↓
Closed
```

Additional outcomes include:

```text
Cancelled
Duplicate
```

| Status          | Description                                             |
| --------------- | ------------------------------------------------------- |
| **Open**        | Ticket has been submitted and is waiting for assignment |
| **Assigned**    | An IT Support Agent has been assigned                   |
| **In Progress** | The assigned agent is actively working on the issue     |
| **Pending**     | Work is temporarily paused because of a dependency      |
| **Resolved**    | The issue has been technically resolved                 |
| **Closed**      | The support workflow has been completed                 |
| **Cancelled**   | The ticket is no longer required                        |
| **Duplicate**   | The issue is already represented by another ticket      |

Status changes are validated on the backend.

Users cannot bypass ticket ownership, role permissions, assignment rules, capacity restrictions, or status-transition rules by directly calling the API.

---

# Assignment Workflow

ResolveHub supports controlled ticket assignment.

## Administrator Direct Assignment

```text
Open Ticket
    ↓
Administrator selects IT Agent
    ↓
Backend validates agent
    ↓
Capacity check
    ↓
Ticket assigned
    ↓
History + Activity + Audit updated
```

Administrators can assign eligible tickets directly.

---

## Manager Assignment Request

```text
Open Ticket
    ↓
Manager selects IT Agent
    ↓
Assignment Request created
    ↓
Administrator reviews request
    ↓
Approve / Reject
    ↓
Capacity revalidated
    ↓
Ticket assigned if approved
```

This workflow separates operational recommendations from final administrative approval.

---

## IT Agent Capacity Rule

An IT Support Agent can have a maximum of:

```text
5 active tickets
```

Statuses counted toward active workload:

* Assigned
* In Progress
* Pending

Statuses excluded:

* Resolved
* Closed
* Cancelled
* Duplicate

Capacity is validated against the current database state when assignment occurs.

This prevents users from bypassing the workload limit through stale frontend information or direct API requests.

---

# Duplicate Ticket Workflow

ResolveHub includes a controlled duplicate-review process.

## Manager-Reported Duplicate

```text
Manager identifies suspected duplicate
    ↓
Possible original ticket selected
    ↓
Duplicate review request created
    ↓
Administrator reviews request
    ↓
Approve / Reject
```

If approved:

* The duplicate ticket is linked to the original
* The duplicate is removed from the normal active workflow
* Existing records remain available
* History is preserved
* Activity information is preserved
* Audit information is preserved

If rejected, the ticket continues through its normal lifecycle.

---

## Administrator Direct Action

Administrators can directly mark a confirmed ticket as a duplicate when the relationship is already clear.

This provides a faster path while keeping the action auditable.

---

# Cancellation Workflow

ResolveHub handles cancellation as a controlled workflow rather than simply deleting operational records.

Depending on the ticket state and user role:

* Eligible ticket owners can cancel tickets where permitted
* Authorized workflow participants can request cancellation
* Requests can be reviewed by the responsible role
* Cancellation actions are recorded
* Ticket history remains preserved
* Audit information remains available

Tickets are not silently removed from the database when cancelled.

---

# AI Assistant

ResolveHub includes an integrated AI support assistant designed specifically for the application's help-desk environment.

The assistant is not intended to replace IT Support Agents or make unrestricted operational decisions.

Instead, it provides contextual assistance to users.

## Capabilities

The assistant can help users with:

* Understanding ResolveHub functionality
* Understanding role permissions
* Explaining ticket statuses
* Explaining assignment workflows
* Explaining duplicate workflows
* Explaining cancellation workflows
* Understanding comments and visibility
* Understanding ticket categories
* Understanding priorities
* Navigating role-specific features
* Troubleshooting common IT problems
* Providing structured troubleshooting suggestions
* Explaining what happens after a ticket is submitted

## Role Awareness

Assistant responses take the signed-in user's role into account.

For example, it can distinguish between what an:

* Employee
* IT Support Agent
* Manager
* Administrator

is authorized to perform.

The assistant is instructed not to invent unavailable functionality or tell users to perform actions outside their permissions.

## Reliability

AI integration includes graceful failure handling.

If the AI provider is temporarily unavailable, ResolveHub can return a controlled service response instead of allowing the failure to crash the main ticket-management system.

The AI assistant therefore remains an enhancement to the application rather than a dependency for core ticket operations.

---

# Reports and Analytics

ResolveHub provides operational reporting for authorized management roles.

Reports can be filtered by a selected time period so that statistics represent the same reporting window.

## Reporting Metrics

Reports may include:

* Total tickets
* Created tickets
* Resolved tickets
* Ticket status distribution
* Ticket priority distribution
* Ticket category distribution
* Created vs. resolved trends
* IT Agent workload
* Ticket volume across selected periods

## Data Visualization

Dashboard and report interfaces use charts to make operational information easier to interpret.

Visualizations include:

* Pie charts
* Trend charts
* Comparative charts
* Workload information

## Export

Authorized reports can be exported as:

* PDF
* Excel

Exports respect the selected reporting filters so the downloaded report represents the same dataset shown to the user.

---

# Activity Tracking and Audit Logs

ResolveHub separates ticket-level operational activity from system-level auditing.

## Ticket History

Ticket history records important lifecycle changes such as:

* Ticket creation
* Editing
* Assignment
* Reassignment
* Status transitions
* Pending states
* Resolution
* Closure
* Cancellation
* Duplicate-review outcomes

---

## Activity Timeline

Authorized users can review detailed activity associated with a ticket.

This provides visibility into how the request progressed through the support workflow.

---

## Work Sessions

ResolveHub tracks active support work.

For example:

```text
In Progress
    ↓
Work session starts

In Progress → Pending
    ↓
Current work session ends

Pending → In Progress
    ↓
New work session starts

In Progress → Resolved
    ↓
Current work session ends
```

Total ticket work duration can therefore be calculated from recorded work sessions instead of relying only on ticket creation and resolution timestamps.

---

## System Audit Log

Managers and Administrators can review important system actions.

Audit information can include:

* User
* Role
* Action
* Action category
* Related entity
* Ticket reference
* Previous value
* New value
* Timestamp
* Additional details

The audit interface supports:

* Search
* Date filtering
* Pagination
* Related-record navigation where authorized

This provides accountability for administrative and workflow-sensitive operations.

---

# Authentication and Security

Security is enforced at both frontend and backend levels.

## Authentication

ResolveHub uses:

* ASP.NET Core Identity
* JWT Bearer authentication
* Protected API endpoints
* Role-based authorization

## Account Security

Implemented protections include:

* Password policy enforcement
* Failed-login lockout
* Active-account validation
* Forgot-password workflow
* Reset-password workflow
* Time-limited password-reset tokens
* Generic forgot-password responses
* Rate limiting
* Remember Me support

## Authorization

Authorization checks protect:

* Tickets
* Comments
* Attachments
* Drafts
* User-management operations
* Assignment operations
* Duplicate actions
* Administrative functionality

Frontend route protection improves user experience, but the API independently validates authorization.

Frontend restrictions are therefore **not treated as the application's security boundary**.

## Additional Security Measures

* HTTPS
* CORS configuration
* Centralized exception handling
* Rate limiting on sensitive endpoints
* Authentication response cache controls
* Sensitive development configuration through .NET User Secrets
* No credentials committed intentionally to source control

---

# Technology Stack

## Frontend

| Technology         | Purpose                                     |
| ------------------ | ------------------------------------------- |
| **React 19**       | Component-based frontend                    |
| **JavaScript**     | Application logic                           |
| **React Router 7** | Client-side and protected routing           |
| **Vite 8**         | Development and production build tooling    |
| **Fetch API**      | REST API communication                      |
| **Recharts**       | Reports and dashboard visualizations        |
| **Lucide React**   | Interface icons                             |
| **CSS**            | Responsive layouts and shared visual system |
| **Oxlint**         | Frontend linting                            |

---

## Backend

| Technology                         | Purpose                          |
| ---------------------------------- | -------------------------------- |
| **ASP.NET Core Web API (.NET 10)** | Backend API and application host |
| **C#**                             | Backend programming language     |
| **Entity Framework Core 10**       | ORM and SQL Server integration   |
| **ASP.NET Core Identity**          | Account and role management      |
| **JWT Bearer Authentication**      | Stateless API authentication     |
| **OpenAPI / Swagger**              | API documentation and testing    |
| **xUnit**                          | Backend automated testing        |
| **Resend**                         | Email delivery                   |

---

## Database

| Technology                           | Purpose                      |
| ------------------------------------ | ---------------------------- |
| **Microsoft SQL Server**             | Relational data storage      |
| **Entity Framework Core Migrations** | Database schema evolution    |
| **ASP.NET Core Identity Tables**     | Authentication and user data |

---

## AI

| Component                      | Purpose                                          |
| ------------------------------ | ------------------------------------------------ |
| **LLM-powered assistant**      | Application and IT support assistance            |
| **Ollama integration**         | AI model communication                           |
| **Role-aware prompting**       | Permission-aware responses                       |
| **Graceful fallback handling** | Prevent AI outages from affecting core workflows |

---

## Deployment

| Layer           | Platform                    |
| --------------- | --------------------------- |
| **Frontend**    | Vercel                      |
| **Backend API** | Microsoft Azure App Service |
| **Database**    | Azure SQL Database          |

---

# Database

ResolveHub uses a relational SQL Server database.

Major data areas include:

```text
Users
Roles
Departments
Tickets
Ticket Categories
Ticket Statuses
Ticket Priorities
Ticket Attachments
Ticket Comments
Comment Attachments
Ticket History
Ticket Activity
Work Sessions
Notifications
Drafts
Assignment Requests
Duplicate Reviews
Audit Logs
Identity Data
```

Entity Framework Core is used as the ORM between the application and SQL Server.

This allows application code to work with strongly typed C# entities while EF Core handles SQL generation, relationships, change tracking, and migrations.

---

# Project Structure

```text
ResolveHub/
│
├── backend/
│   ├── ResolveHub.sln
│   │
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
│   │
│   └── tests/
│       └── ResolveHub.Api.Tests/
│
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
│   │
│   ├── package.json
│   └── vite.config.js
│
├── database/
├── docs/
├── LICENSE
└── README.md
```

## Backend Responsibilities

### Controllers

Responsible for:

* REST endpoints
* HTTP request handling
* Authorization boundaries
* HTTP responses

### Services

Responsible for application and business logic including:

* Authentication
* Ticket operations
* Assignments
* Comments
* Drafts
* Categories
* Notifications
* Reporting
* Auditing
* User management
* AI integration

### DTOs

Define validated request and response contracts between the API and clients.

### Entities

Represent persistent application and Identity data.

### Data

Contains:

* EF Core database context
* Entity configuration
* Migrations
* Seed data

---

## Frontend Responsibilities

### Components

Reusable interface elements such as:

* Forms
* Tables
* Dialogs
* Layouts
* Pagination
* Status indicators

### Pages

Role-specific application screens.

### Services

Responsible for communication with backend APIs and authentication storage.

### Utilities

Shared logic for:

* Dates
* Times
* Filters
* Formatting
* Common frontend operations

---

# Testing and Performance Validation

ResolveHub was tested at multiple levels during development.

## Functional Testing

Core workflows were tested across all four user roles:

```text
Employee
IT Support Agent
Manager
Administrator
```

Testing covered areas such as:

* Authentication
* Ticket creation
* Ticket assignment
* Status transitions
* Pending and resume flows
* Ticket resolution
* Ticket closure
* Duplicate handling
* Cancellation
* Comments
* Attachments
* Notifications
* Reporting
* User management
* Category management
* AI assistant behavior

---

## Automated Testing

The backend includes an xUnit test project for automated validation of backend behavior.

```text
backend/tests/ResolveHub.Api.Tests/
```

---

## Large Dataset Testing

ResolveHub was also tested with a database containing more than:

```text
11,000 tickets
```

This was used to validate interfaces involving:

* Pagination
* Search
* Filtering
* Dashboards
* Large ticket collections

---

## Concurrent Load Testing

API load testing was performed using k6.

A representative test included:

```text
100 virtual users
2,325 requests
100% successful checks
0 failed requests
```

The purpose of this testing was to validate API behavior under concurrent access rather than relying only on single-user functional testing.

---

# Running the Project Locally

## Prerequisites

Install:

* .NET 10 SDK
* Node.js
* npm
* SQL Server
* Git

An AI provider and email service configuration are required only for the related optional functionality.

---

## 1. Clone the Repository

```bash
git clone <repository-url>
cd ResolveHub
```

---

## 2. Configure the Backend

Navigate to:

```bash
cd backend/src/ResolveHub.Api
```

Configure the required environment-specific values such as:

* SQL Server connection string
* JWT configuration
* Email configuration
* AI provider configuration

Sensitive values should not be committed to source control.

For local development, .NET User Secrets can be used.

---

## 3. Restore Backend Dependencies

```bash
dotnet restore
```

---

## 4. Apply Database Migrations

```bash
dotnet ef database update
```

---

## 5. Run the Backend

```bash
dotnet run
```

Swagger/OpenAPI can be used during development to inspect and test available API endpoints.

---

## 6. Configure the Frontend

Open a new terminal:

```bash
cd frontend
```

Install dependencies:

```bash
npm install
```

---

## 7. Run the Frontend

```bash
npm run dev
```

The frontend will communicate with the ASP.NET Core API using the configured development API endpoint.

---

# Deployment

ResolveHub uses separate frontend, backend, and database services.

```text
User
  ↓
Vercel
  ↓
React Frontend
  ↓
HTTPS
  ↓
Azure App Service
  ↓
ASP.NET Core API
  ↓
Azure SQL Database
```

### Frontend

The React production build is deployed using **Vercel**.

### Backend

The ASP.NET Core API is deployed using **Microsoft Azure App Service**.

### Database

Production data is stored in **Azure SQL Database**.

This architecture allows each layer to be deployed and scaled independently.

---

# Engineering Decisions

Several design decisions were made to keep the application maintainable and secure.

## Separate Frontend and Backend

The React application and ASP.NET Core API are independent layers.

Benefits include:

* Clear separation of concerns
* Independent deployment
* API reuse
* Easier testing
* Stronger security boundaries
* Easier future mobile-client integration

---

## Backend-Enforced Business Rules

Important workflow rules are validated by the API rather than trusting frontend state.

Examples include:

* Role permissions
* Ticket ownership
* Assignment authorization
* Agent capacity
* Status transitions
* Comment visibility
* Attachment access

---

## Controlled Administrative Workflows

Sensitive actions are represented as workflows instead of unrestricted direct actions.

Examples include:

* Manager assignment request → Administrator approval
* Manager duplicate report → Administrator review

This provides better accountability and clearer responsibility separation.

---

## Auditability

Tickets are not treated only as mutable database rows.

ResolveHub records history, activities, work sessions, workflow actions, and audit information so operational decisions remain traceable.

---

## AI as an Assistant, Not an Authority

The AI assistant supports users with information and troubleshooting.

Core business operations remain controlled by deterministic application logic and role permissions.

An unavailable AI service therefore does not prevent users from accessing the main ticket-management functionality.

---

# Future Enhancements

Potential future development could include:

* AI-assisted duplicate-ticket detection
* AI ticket category suggestions
* AI priority recommendations
* Knowledge-base article recommendations
* SLA policies and escalation rules
* Email-to-ticket creation
* Real-time updates using SignalR
* Advanced notification preferences
* Additional report customization
* Organization-level analytics
* Expanded automated test coverage
* Mobile application support

### AI-Assisted Duplicate Detection

A future version could compare newly submitted tickets with existing tickets and suggest likely matches based on semantic similarity.

The AI would only provide a recommendation.

The existing duplicate-review workflow would remain responsible for the final operational decision.

---

# Project Goals

ResolveHub was developed to demonstrate practical full-stack software engineering concepts including:

* Full-stack application development
* REST API design
* Relational database design
* Authentication
* Authorization
* Role-based access control
* Business-rule enforcement
* Workflow modeling
* Audit logging
* File handling
* Reporting
* Data visualization
* AI integration
* Cloud deployment
* Performance testing
* Responsive frontend development

---

# License

ResolveHub is licensed under the **MIT License**.

Copyright © 2026 Fatima Ghannam.
