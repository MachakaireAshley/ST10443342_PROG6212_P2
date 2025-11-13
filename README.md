# ST10443342_PROG6212_P2
# Claims Management System (CMCS)

A comprehensive ASP.NET Core MVC web application for managing academic claims with multi-level approval workflows.

## Features

- **Multi-role System**: Lecturer, Coordinator, Manager, and Administrator roles
- **Claims Workflow**: Submit → Coordinator Review → Manager Approval
- **Document Management**: Upload supporting files (PDF, Word, Excel, Images)
- **Real-time Dashboard**: Role-specific dashboards with statistics
- **Secure Authentication**: ASP.NET Core Identity with role-based authorization

##  User Roles

- **Lecturer**: Submit claims, upload documents, view personal history
- **Coordinator**: Review and approve/reject claims, filter by status
- **Manager**: Final approval authority, comprehensive oversight
- **Administrator**: User management, system configuration, adds users

##  Workflow

1. **Lecturers** submit claims with workload hours and optional documents
2. **Coordinators** review and approve claims for manager review or reject claims
3. **Managers** provide final approval or rejection
4. **Administrators** manage users and system settings

## Tech Stack

- **Backend**: ASP.NET Core MVC, Entity Framework Core
- **Frontend**: Bootstrap, Razor Pages
- **Authentication**: ASP.NET Core Identity
- **Database**: SQL Server
- **File Storage**: Local file system with secure uploads
