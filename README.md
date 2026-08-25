<p align="center">
  <img src="frontend/public/favicon.png" alt="ResolveHub Logo" width="110">
</p>

<h1 align="center">ResolveHub</h1>

<p align="center">
  <strong>Full-Stack IT Help Desk &amp; Ticket Management System</strong>
</p>

ResolveHub is a full-stack IT support platform designed to centralize and manage internal technical support requests from submission through resolution.

The platform provides structured workflows for **Employees, IT Support Agents, Managers, and Administrators**, combining ticket management, role-based access control, team workload management, reporting, auditability, notifications, and an AI-powered support assistant.

Built with **React, ASP.NET Core, SQL Server, Entity Framework Core, and JWT authentication**, ResolveHub follows a separated frontend/backend architecture and is deployed using **Vercel, Microsoft Azure App Service, and Azure SQL Database**.

---

## Table of Contents

* [Key Features](#key-features)
* [System Architecture](#system-architecture)
* [User Roles](#user-roles)
* [Ticket Workflow](#ticket-workflow)
* [Assignment and Workload Management](#assignment-and-workload-management)
* [Duplicate and Cancellation Workflows](#duplicate-and-cancellation-workflows)
* [AI Assistant](#ai-assistant)
* [Reports and Analytics](#reports-and-analytics)
* [Security](#security)
* [Technology Stack](#technology-stack)
* [Testing](#testing)
* [Project Structure](#project-structure)
* [Running Locally](#running-locally)
* [Deployment](#deployment)
* [Future Enhancements](#future-enhancements)
* [License](#license)

---

## Key Features

### Ticket Management

* Create, edit, search, filter, and track IT support tickets
* Unique ticket references using `RH-YYYY-NNNN`
* Ticket categories, priorities, and statuses
* Draft ticket support
* Ticket and comment attachments
* Public and private comments
* Threaded replies
* Resolution summaries
* Ticket history and activity timelines
* Work-duration tracking
* Pagination for large ticket collections

### Role-Based Access Control

ResolveHub supports four application roles:

* Employee
* IT Support Agent
* Manager
* Administrator

Each role has dedicated dashboards, navigation, permissions, and workflow responsibilities.

### Workflow Management

* Controlled ticket status transitions
* Administrator direct assignment
* Manager assignment requests
* IT Agent workload limits
* Duplicate-ticket review
* Ticket cancellation workflow
* Notifications
* Activity tracking
* System audit logging

### Administration

* User management
* Account invitations
* User activation and deactivation
* Ticket-category management
* Assignment-request approval
* Duplicate-review approval
* System-wide operational oversight

### Reports and Analytics

* Dashboard statistics
* Date-range filtering
* Created vs. resolved ticket trends
* Ticket distribution by category and priority
* IT Agent workload analysis
* Visual charts
* PDF report export
* Excel report export

### User Experience

* Responsive interface
* Role-specific dashboards
* Dark mode
* Search and filtering
* Pagination
* Dialog-based workflows
* Notification center
* Consistent date and time formatting

---

## System Architecture

ResolveHub follows a separated frontend/backend architecture:

```text
┌─────────────────────────────┐
│       React Frontend        │
│                             │
│ Dashboards • Tickets • AI   │
│ Reports • Forms • UI        │
└──────────────┬──────────────┘
               │
               │ HTTPS / REST API
               │ JWT Authentication
               ▼
┌─────────────────────────────┐
│    ASP.NET Core Web API     │
│                             │
│ Controllers • Services      │
│ Authorization • Business    │
│ Rules • Reporting • AI      │
└──────────────┬──────────────┘
               │
               │ Entity Framework Core
               ▼
┌─────────────────────────────┐
│         SQL Server          │
│                             │
│ Users • Tickets • Comments  │
│ History • Audit • Workflow  │
└─────────────────────────────┘
```

### Request Flow

```text
User Action
    ↓
React Frontend
    ↓
REST API Request + JWT
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
API Response
    ↓
Updated User Interface
```

Important business rules are enforced by the backend rather than relying only on frontend restrictions.

---

## User Roles

| Role                 | Main Responsibilities                                                                                                    |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| **Employee**         | Create and track tickets, manage drafts, add comments and attachments, and monitor request progress                      |
| **IT Support Agent** | Work assigned tickets, troubleshoot issues, update statuses, communicate with users, and resolve requests                |
| **Manager**          | Monitor support operations, review team workload, request assignments, report duplicates, and access operational reports |
| **Administrator**    | Manage users and categories, assign tickets, approve workflows, review audits, and oversee the complete system           |

This separation ensures that users only access functionality appropriate to their responsibilities.

---

## Ticket Workflow

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

| Status          | Description                                         |
| --------------- | --------------------------------------------------- |
| **Open**        | Submitted and waiting for assignment                |
| **Assigned**    | An IT Support Agent has been assigned               |
| **In Progress** | The assigned agent is actively working on the issue |
| **Pending**     | Work is temporarily paused because of a dependency  |
| **Resolved**    | The technical issue has been resolved               |
| **Closed**      | The support workflow has been completed             |
| **Cancelled**   | The request is no longer required                   |
| **Duplicate**   | The issue is already represented by another ticket  |

Status transitions are validated by the backend according to the user's role and the current state of the ticket.

---

## Assignment and Workload Management

ResolveHub provides two controlled assignment paths.

### Administrator Assignment

Administrators can directly assign eligible tickets to IT Support Agents.

```text
Open Ticket
    ↓
Administrator selects Agent
    ↓
Capacity Validation
    ↓
Ticket Assigned
```

### Manager Assignment Request

Managers can recommend an IT Support Agent, but the request is reviewed by an Administrator.

```text
Manager selects Agent
    ↓
Assignment Request
    ↓
Administrator Review
    ↓
Approve / Reject
    ↓
Ticket Assigned if Approved
```

### Agent Capacity

Each IT Support Agent can have a maximum of:

```text
5 active tickets
```

Active workload includes:

* Assigned
* In Progress
* Pending

Capacity is revalidated against the current database state when an assignment is processed.

---

## Duplicate and Cancellation Workflows

### Duplicate Tickets

Managers can report a suspected duplicate and identify the possible original ticket.

```text
Suspected Duplicate
    ↓
Manager Report
    ↓
Administrator Review
    ↓
Approve / Reject
```

If approved, the ticket is linked to the original and marked as a duplicate while its history and audit information remain preserved.

Administrators can also directly mark a confirmed duplicate when appropriate.

### Ticket Cancellation

ResolveHub uses controlled cancellation rather than deleting operational records.

Cancellation actions and requests remain traceable through ticket history and audit records.

---

## AI Assistant

ResolveHub includes an integrated **LLM-powered AI assistant** designed for the application's IT help-desk environment.

The assistant can help users:

* Troubleshoot common IT issues
* Understand ResolveHub features
* Understand role permissions
* Explain ticket statuses
* Explain assignment workflows
* Explain duplicate and cancellation processes
* Understand categories and priorities
* Navigate role-specific functionality
* Receive structured troubleshooting suggestions

### Role Awareness

Responses are adapted to the signed-in user's role so that the assistant does not recommend functionality the user is not authorized to perform.

### Graceful Failure Handling

The AI assistant is an enhancement rather than a dependency of the main system.

If the AI service becomes unavailable, the core ticket-management functionality continues operating and a controlled fallback response is returned.

---

## Reports and Analytics

Authorized management roles can analyze ticket activity over selected reporting periods.

Reports include:

* Total tickets
* Created tickets
* Resolved tickets
* Ticket status distribution
* Category distribution
* Priority distribution
* Created vs. resolved trends
* IT Agent workload

Visual dashboards use charts to make operational data easier to interpret.

Reports can also be exported as:

* **PDF**
* **Excel**

Exported reports respect the selected filters and reporting period.

---

## Activity Tracking and Auditability

ResolveHub maintains several layers of operational traceability.

### Ticket History

Records important ticket changes including:

* Creation
* Editing
* Assignment
* Reassignment
* Status transitions
* Resolution
* Closure
* Cancellation
* Duplicate decisions

### Work Sessions

Agent work sessions are tracked when tickets move between active work states, allowing actual work duration to be calculated.

### Audit Log

Important system actions are recorded with information such as:

* User
* Role
* Action
* Related entity
* Previous and new values
* Timestamp
* Additional details

This provides accountability for sensitive administrative and workflow operations.

---

## Security

ResolveHub applies security at both the frontend and backend levels.

### Authentication

* ASP.NET Core Identity
* JWT Bearer authentication
* Protected API endpoints
* Role-based authorization

### Account Protection

* Password policy enforcement
* Failed-login lockout
* Active-account validation
* Forgot-password workflow
* Reset-password workflow
* Time-limited reset tokens
* Rate limiting
* Remember Me support

### Authorization

Backend authorization protects operations involving:

* Tickets
* Comments
* Attachments
* Drafts
* Assignments
* Duplicate workflows
* User management
* Administrative functionality

Frontend route protection improves the user experience, but the backend remains the application's security boundary.

### Additional Measures

* HTTPS
* CORS configuration
* Centralized exception handling
* Rate limiting on sensitive endpoints
* Secure configuration using environment settings and .NET User Secrets
* Sensitive credentials excluded from source control

---

## Technology Stack

### Frontend

| Technology         | Purpose                            |
| ------------------ | ---------------------------------- |
| **React 19**       | Component-based user interface     |
| **JavaScript**     | Frontend application logic         |
| **React Router 7** | Client-side and protected routing  |
| **Vite**           | Development and production builds  |
| **Fetch API**      | REST API communication             |
| **Recharts**       | Dashboard and report visualization |
| **Lucide React**   | Interface icons                    |
| **CSS**            | Responsive styling and layouts     |

### Backend

| Technology                         | Purpose                          |
| ---------------------------------- | -------------------------------- |
| **ASP.NET Core Web API (.NET 10)** | REST API and backend application |
| **C#**                             | Backend programming language     |
| **Entity Framework Core**          | Object-relational mapping        |
| **ASP.NET Core Identity**          | User and role management         |
| **JWT Authentication**             | API authentication               |
| **Swagger / OpenAPI**              | API documentation and testing    |
| **xUnit**                          | Automated backend testing        |
| **Resend**                         | Email delivery                   |

### Database

| Technology               | Purpose                     |
| ------------------------ | --------------------------- |
| **Microsoft SQL Server** | Relational data storage     |
| **EF Core Migrations**   | Database schema management  |
| **Azure SQL Database**   | Production database hosting |

### AI

| Technology               | Purpose                                   |
| ------------------------ | ----------------------------------------- |
| **LLM Integration**      | AI-powered assistance and troubleshooting |
| **Ollama**               | AI model communication                    |
| **Role-Aware Prompting** | Permission-aware assistant responses      |

### Deployment

| Layer           | Platform                    |
| --------------- | --------------------------- |
| **Frontend**    | Vercel                      |
| **Backend API** | Microsoft Azure App Service |
| **Database**    | Azure SQL Database          |

---

## Testing

ResolveHub was tested across the complete application workflow.

### Functional Testing

Testing covered:

* Authentication
* Ticket creation and editing
* Drafts
* Assignment workflows
* Status transitions
* Pending and resume workflows
* Resolution and closure
* Duplicate handling
* Cancellation
* Comments and attachments
* Notifications
* Reporting
* User and category management
* AI assistant behavior

### Automated Testing

Backend automated tests are implemented using **xUnit**.

```text
backend/tests/ResolveHub.Api.Tests/
```

### Large Dataset Testing

The application was tested with more than:

```text
11,000 tickets
```

to validate:

* Pagination
* Search
* Filtering
* Dashboards
* Large ticket collections

### Load Testing

Concurrent API testing was performed using **k6**.

A representative test achieved:

```text
100 virtual users
2,325 requests
100% successful checks
0 failed requests
```

---

## Project Structure

```text
ResolveHub/
│
├── backend/
│   ├── src/
│   │   └── ResolveHub.Api/
│   │       ├── Controllers/
│   │       ├── Data/
│   │       ├── DTOs/
│   │       ├── Entities/
│   │       ├── Infrastructure/
│   │       ├── Services/
│   │       ├── Settings/
│   │       └── Program.cs
│   │
│   └── tests/
│       └── ResolveHub.Api.Tests/
│
├── frontend/
│   ├── public/
│   └── src/
│       ├── assets/
│       ├── components/
│       ├── pages/
│       ├── services/
│       ├── styles/
│       ├── utils/
│       ├── App.jsx
│       └── main.jsx
│
├── database/
├── docs/
├── LICENSE
└── README.md
```

The backend separates HTTP handling, business logic, persistence, entities, and request/response contracts.

The frontend separates reusable components, role-specific pages, API services, styles, and shared utilities.

---

## Running Locally

### Prerequisites

Install:

* .NET 10 SDK
* Node.js and npm
* SQL Server
* Git

---

### 1. Clone the Repository

```bash
git clone https://github.com/fatimaghannam/ResolveHub.git
cd ResolveHub
```

### 2. Configure the Backend

```bash
cd backend/src/ResolveHub.Api
```

Configure the required environment values for:

* SQL Server connection
* JWT authentication
* Email service
* AI provider

Sensitive development values should be stored using environment variables or .NET User Secrets.

### 3. Restore Dependencies

```bash
dotnet restore
```

### 4. Apply Database Migrations

```bash
dotnet ef database update
```

### 5. Run the Backend

```bash
dotnet run
```

### 6. Run the Frontend

Open another terminal:

```bash
cd frontend
npm install
npm run dev
```

The frontend will communicate with the ASP.NET Core API using the configured development endpoint.

---

## Deployment

ResolveHub is deployed using a cloud-based three-tier architecture.

```text
User
  ↓
Vercel
  ↓
React Frontend
  ↓
HTTPS / REST API
  ↓
Azure App Service
  ↓
ASP.NET Core API
  ↓
Azure SQL Database
```

* **Frontend:** Vercel
* **Backend:** Microsoft Azure App Service
* **Database:** Azure SQL Database

The frontend, backend, and database are deployed independently while communicating through secured API requests.

---

## Future Enhancements

Potential future enhancements include:

* AI-assisted duplicate-ticket detection
* Knowledge-base recommendations
* SLA policies and automatic escalation
* Email-to-ticket functionality
* Real-time updates using SignalR
* Advanced notification preferences
* Mobile application support

### AI-Assisted Duplicate Detection

A future enhancement could compare new tickets with existing requests using semantic similarity and suggest possible duplicates before submission.

AI would provide the recommendation, while the existing controlled duplicate-review workflow would remain responsible for the final decision.

---

## License

ResolveHub is licensed under the **MIT License**.

Copyright © 2026 Fatima Ghannam.
