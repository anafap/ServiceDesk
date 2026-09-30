# Service Desk and Maintenance Management System

A portfolio project for managing maintenance requests across a company with many stores. Store managers report problems, the central service desk reviews and approves them, and approved requests are assigned to external technicians.

## Project goals

- Support multiple stores and store managers.
- Track assets such as POS terminals, printers, routers, CCTV equipment, and air conditioners.
- Manage the complete maintenance-request lifecycle.
- Record assignments, status history, decisions, and resolution details.
- Notify store managers and external technicians through integrations such as WhatsApp.
- Demonstrate clean architecture, domain-driven business rules, SQL Server persistence, testing, and API design.

## Business workflow

```text
Store manager reports a problem
        ↓
Ticket enters AwaitingApproval
        ↓
Admin reviews the request
        ├── Rejects it
        └── Approves it
                ↓
        Admin assigns an external technician
                ↓
        Technician visit is scheduled
                ↓
        Ticket moves to InProgress
                ↓
        Technician resolves the issue
                ↓
        Ticket is closed after confirmation
```

Technicians are third-party contacts and do not log in to this application. They receive assignment details by phone, WhatsApp, or email. Application users are administrators and store managers.

## Ticket statuses

```text
AwaitingApproval → Approved → Assigned → InProgress → Resolved → Closed
                         └──→ Rejected
```

Status changes are controlled by domain methods and should be recorded in ticket status history.

## Architecture

The solution follows a clean-architecture style:

```text
ServiceDesk.Api
        ↓
ServiceDesk.Application
        ↓
ServiceDesk.Domain

ServiceDesk.Infrastructure
        ↓
ServiceDesk.Application and ServiceDesk.Domain
```

### Domain

Contains business entities, enums, and business rules. It must not depend on ASP.NET Core, Entity Framework, SQL Server, WhatsApp, or other technical concerns.

### Application

Contains use cases and orchestration, such as approving tickets, assigning technicians, creating history records, and requesting notifications. It defines interfaces for infrastructure services.

### Infrastructure

Contains technical implementations: Entity Framework Core, SQL Server, repositories, migrations, notification providers, and file storage.

### API

Contains HTTP endpoints, authentication configuration, request/response models, dependency injection, and HTTP status-code handling. API endpoints should remain thin and call Application use cases.

### Tests

Contains unit tests for domain rules and application tests for use cases. Integration tests will later verify the API and database together.

## Current domain model

- `Store`: physical company location.
- `Asset`: equipment located at a store.
- `User`: application user with an Admin or StoreManager role.
- `ExternalTechnician`: third-party technician contact without login access.
- `SupportTicket`: maintenance request and controlled status workflow.
- `TechnicianAssignment`: assignment, schedule, notes, and assigning admin.
- `TicketStatusHistory`: audit record for every status change.

## Planned milestones

1. Complete and test the domain model.
2. Add Application use-case interfaces and services.
3. Add Entity Framework Core and SQL Server persistence.
4. Add repositories and database migrations.
5. Build ticket, store, asset, and assignment API endpoints.
6. Add authentication and role-based access for admins and store managers.
7. Add WhatsApp/email notification abstractions and provider integration.
8. Add preventive-maintenance scheduling, dashboards, reports, and audit views.
9. Add API integration tests, documentation, seed data, and deployment instructions.

## Development commands

```bash
dotnet build
dotnet test
dotnet run --project ServiceDesk.Api
```

## Status

🚧 In development
