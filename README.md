# 🏢 HRMS – Human Resource Management System

A scalable, multi-tenant HR Management System designed to support structured company hierarchies, approval workflows, payroll management, attendance tracking, and enterprise-level employee operations.

---

## 🚀 Overview

HRMS is being built as a complete HR ecosystem that supports:

- Multi-company architecture
- Configurable approval hierarchy
- Role-based access control
- Leave & attendance management
- Payroll & salary packages
- Requests workflow engine
- Surveys & complaints management
- Asset & loan management
- Mobile-first architecture (Flutter)

---

# 🧩 Roles

### System Role
- **Super Admin**

### Company Roles
- Employee  
- HR  
- IT / Asset Custodian *(Optional)*  
- Team Leader  
- Unit Leader  
- Department Manager  
- Vice President *(Optional)*  
- CEO  

### Approval Flow

Approval routing is configurable per company.

---

# 🗂 Core Modules

## 👤 Employee Profile
- Personal & contact information
- Department / team structure
- Employment status
- Medical class (A/B/C)
- Insurance & salary package reference

---

## 🕒 Attendance
- Clock in / clock out
- Attendance status (present / late / absent)
- Device / location tracking (optional)
- Monthly history review

---

## 🔔 Notifications
- Global announcements
- Targeted notifications
- Request & survey updates
- Read/unread tracking

---

## 📝 Surveys
- Employee submissions
- Multi-step routing chain
- Comments per approval level
- Status tracking (draft → completed/rejected)

---

## ⚠ Complaints
- Employee complaint submission
- Attachments support
- HR assignment & resolution
- Status tracking

---

## 🔄 Requests Engine

All requests follow a structured approval chain.

### Supported Request Types
- Leave / Permission
- Salary Slip
- HR Letter
- Resignation / End of Service
- Purchase Order
- Asset Request (issue / return / replace / damage)
- Loan (with repayment schedule)
- Assignment (انتداب)
- Others

Each request includes:
- Approval chain
- Attachments
- Status tracking
- Escalation support

---

## 💰 Salary Package
- Basic salary
- Allowances / deductions / tax
- Effective date ranges
- Payroll calculation basis
- Insurance mapping

---

## 🏢 Company Configuration
- Company info & logo
- Yearly vacation days
- Configurable hierarchy levels
- Ordered approval structure

---

# 🎯 Key Features by Role

### 👑 Super Admin
- Manage companies
- Configure hierarchy
- Manage roles & permissions
- Monitor system activity

### 👨‍💼 Employee
- View & update profile
- Clock in/out
- Submit requests
- Submit surveys
- Submit complaints
- Track approvals
- View leave balance & payslips

### 🧑‍💼 HR
- Employee management
- Leave balance control
- Attendance corrections
- Salary package management
- Complaint handling
- Announcements
- Survey monitoring

### 👨‍👩‍👧 Leadership Roles
- Approve/reject requests
- Review & forward surveys
- View dashboards
- Escalate exceptions to HR

### 💻 Assets Admin
- Maintain asset inventory
- Process issue/return/damage
- Categorize assets
- Assign approved asset requests

---

# 📱 Mobile Application

Built with:

- **Flutter**
- Bloc (State Management)
- MVVM + Repository Pattern
- Hive CE (Local Storage)
- Dio (Networking)
- GoRouter (Navigation)

### Architecture

---

# 🏗 Backend Architecture

- .NET 8 Web API
- Clean Architecture
- JWT Authentication
- Role-Based Authorization
- Multi-Tenant Support
- Configurable Approval Chains

---

# 📌 Project Status

🚧 Currently in Active Development  

Planned improvements:
- Real-time notifications
- Advanced reporting
- KPI dashboards
- Payroll automation
- Multi-language support

---

# 📜 License

This project is under development and not yet licensed for production distribution.

---

# 🛡️ Security & Configuration

### Hardened Identity Policy
The system enforces strict password requirements for all accounts:
- **Min Length**: 12 characters
- **Complexity**: Must include Digit, Lowercase, Uppercase, and Non-Alphanumeric character.

### Seeding Credentials
To eliminate hardcoded secrets, administrative and default account passwords are managed via configuration:
- `SeedPasswordSettings`: Used for all default seeded accounts (CEO, VP, managers, etc).
- `SuperAdminSettings:Password`: Specific password for the Super Admin account.

**Local Development**: Configure these in `appsettings.json` or user-secrets.
**Production**: Always use Environment Variables or a Secrets Manager.

### JWT Claims
Application claim types are centralized in `HrSystemApp.Domain.Constants.AppClaimTypes` to avoid string literal drift across the API and Mobile clients.