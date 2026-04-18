# HR System API Documentation

## Table of Contents
- [Auth](#auth)
- [Companies](#companies)
- [Employees](#employees)
- [OrgNodes](#orgnodes)
- [Requests](#requests)
- [RequestDefinitions](#requestdefinitions)
- [Attendance](#attendance)
- [Notifications](#notifications)
- [Admin](#admin)
- [Storage](#storage)

---

## Auth

### Login
```
POST /api/auth/login
```
**Description:** Authenticate user and receive JWT tokens.

**Request Body:**
```json
{
  "email": "john.doe@company.com",
  "password": "SecurePassword123!",
  "fcmToken": "firebase-token-optional",
  "deviceType": "iOS",
  "language": "en"
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| email | string | Yes | User email address |
| password | string | Yes | User password |
| fcmToken | string | No | Firebase Cloud Messaging token for push notifications |
| deviceType | enum | No | `Android`, `iOS`, or `Web` |
| language | string | No | Language code (e.g., "en", "ar") |

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g...",
  "userId": "user-123",
  "email": "john.doe@company.com",
  "name": "John Doe",
  "role": "Employee",
  "employeeId": "emp-456",
  "mustChangePassword": false,
  "expiresAt": "2026-04-18T12:00:00Z",
  "phoneNumber": "+1234567890",
  "language": "en"
}
```

---

### Logout
```
POST /api/auth/logout
```
**Description:** Logout and invalidate current refresh token.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g..."
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| refreshToken | string | Yes | The refresh token to revoke |

---

### Refresh Token
```
POST /api/auth/refresh
```
**Description:** Get new access token using refresh token.

**Request Body:**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g..."
}
```

---

### Get Current User
```
GET /api/auth/me
```
**Description:** Get current authenticated user info from token claims.

**Headers:** `Authorization: Bearer {token}`

---

### Change Password
```
POST /api/auth/change-password
```
**Description:** Change password for logged-in user.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewSecurePassword456!"
}
```

---

### Force Change Password (First Login)
```
POST /api/auth/force-change-password
```
**Description:** Required password change for users with default password.

**Request Body:**
```json
{
  "userId": "user-123",
  "currentPassword": "DefaultPassword!",
  "newPassword": "NewSecurePassword456!"
}
```

---

### Forgot Password
```
POST /api/auth/forgot-password
```
**Description:** Request password reset OTP.

**Request Body:**
```json
{
  "email": "john.doe@company.com",
  "channel": "Email"
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| email | string | Yes | User email |
| channel | enum | Yes | `Email` or `Sms` |

---

### Verify OTP
```
POST /api/auth/verify-otp
```
**Description:** Verify OTP without changing password.

**Request Body:**
```json
{
  "email": "john.doe@company.com",
  "otp": "123456"
}
```

---

### Reset Password
```
POST /api/auth/reset-password
```
**Description:** Reset password using verified OTP.

**Request Body:**
```json
{
  "email": "john.doe@company.com",
  "otp": "123456",
  "newPassword": "NewSecurePassword456!"
}
```

---

### Update FCM Token
```
POST /api/auth/update-fcm-token
```
**Description:** Update Firebase Cloud Messaging token for push notifications.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "fcmToken": "new-firebase-token",
  "deviceType": "iOS"
}
```

---

### Update Language
```
POST /api/auth/update-language
```
**Description:** Update preferred language.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "language": "ar"
}
```

---

### Revoke Token
```
POST /api/auth/revoke
```
**Description:** Revoke a specific refresh token.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "refreshToken": "token-to-revoke"
}
```

---

### Revoke All Tokens
```
POST /api/auth/revoke-all
```
**Description:** Revoke all refresh tokens for current user.

**Headers:** `Authorization: Bearer {token}`

---

### Get User Tokens
```
GET /api/auth/tokens
```
**Description:** Get all active refresh tokens for current user.

**Headers:** `Authorization: Bearer {token}`

---

## Companies

### Create Company (SuperAdmin)
```
POST /api/companies
```
**Description:** Create a new company. SuperAdmin only.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "companyName": "Acme Corporation",
  "companyLogoUrl": "https://example.com/logo.png",
  "yearlyVacationDays": 21,
  "startTime": "09:00:00",
  "endTime": "17:00:00",
  "graceMinutes": 15,
  "timeZoneId": "UTC"
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| companyName | string | Yes | Company name |
| companyLogoUrl | string | No | URL to company logo |
| yearlyVacationDays | int | Yes | Annual vacation allowance |
| startTime | timespan | Yes | Work start time (HH:mm:ss) |
| endTime | timespan | Yes | Work end time (HH:mm:ss) |
| graceMinutes | int | Yes | Late arrival tolerance in minutes |
| timeZoneId | string | Yes | Timezone (e.g., "UTC", "America/New_York") |

---

### Get All Companies (SuperAdmin)
```
GET /api/companies
```
**Description:** Get paginated list of all companies.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| searchTerm | string | null | Filter by company name |
| status | enum | null | `Active`, `Inactive`, `Suspended` |
| pageNumber | int | 1 | Page number |
| pageSize | int | 20 | Items per page |
| includeLocations | bool | false | Include company locations |

---

### Get Company by ID (SuperAdmin)
```
GET /api/companies/{id}
```
**Description:** Get company details by ID.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| includeLocations | bool | false | Include company locations |

---

### Update Company
```
PUT /api/companies/{id}
```
**Description:** Update company details. CompanyAdmins only.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "companyName": "Acme Corp Updated",
  "companyLogoUrl": "https://example.com/new-logo.png",
  "yearlyVacationDays": 24,
  "startTime": "08:30:00",
  "endTime": "17:30:00",
  "graceMinutes": 10,
  "timeZoneId": "UTC"
}
```

---

### Get My Company
```
GET /api/companies/me
```
**Description:** Get current user's company details.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| includeLocations | bool | false | Include locations |

---

### Update My Company
```
PUT /api/companies/me
```
**Description:** Update current user's company.

**Headers:** `Authorization: Bearer {token}`

**Request Body:** (same as Update Company)

---

### Change Company Status (SuperAdmin)
```
PATCH /api/companies/{id}/status
```
**Description:** Change company status.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "status": "Active"
}
```

---

### Get Company Locations
```
GET /api/companies/{companyId}/locations
```
**Description:** Get all locations for a company.

**Headers:** `Authorization: Bearer {token}`

---

### Create Company Location
```
POST /api/companies/{companyId}/locations
```
**Description:** Add a location to a company.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "locationName": "New York Office",
  "address": "123 Main St, New York, NY 10001",
  "latitude": 40.7128,
  "longitude": -74.0060
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| locationName | string | Yes | Name of the location |
| address | string | Yes | Full address |
| latitude | decimal | No | GPS latitude |
| longitude | decimal | No | GPS longitude |

---

### Delete Company Location
```
DELETE /api/companies/locations/{id}
```
**Description:** Delete a company location.

**Headers:** `Authorization: Bearer {token}`

---

## Employees

### Get All Employees
```
GET /api/employees
```
**Description:** Get paginated list of employees.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| companyId | guid | null | Filter by company |
| role | enum | null | `SuperAdmin`, `CompanyAdmin`, `Executive`, `HR`, `Employee` |
| status | enum | null | `Active`, `Inactive`, `Suspended`, `Terminated`, `OnLeave`, `Probation` |
| search | string | null | Search by name/email |
| page | int | 1 | Page number |
| pageSize | int | 20 | Items per page |

---

### Get Employee by ID
```
GET /api/employees/{id}
```
**Description:** Get employee details.

**Headers:** `Authorization: Bearer {token}`

---

### Get My Profile
```
GET /api/employees/me/profile
```
**Description:** Get current user's employee profile.

**Headers:** `Authorization: Bearer {token}`

---

### Get My Leave Balances
```
GET /api/employees/me/balances
```
**Description:** Get current user's leave balance summary.

**Headers:** `Authorization: Bearer {token}`

---

### Create Employee
```
POST /api/employees
```
**Description:** Create new employee and login account.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "fullName": "Jane Smith",
  "email": "jane.smith@company.com",
  "phoneNumber": "+1234567890",
  "companyId": "company-guid-here",
  "role": "Employee"
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| fullName | string | Yes | Employee full name |
| email | string | Yes | Unique email address |
| phoneNumber | string | No | Phone number |
| companyId | guid | Yes | Company to assign employee to |
| role | enum | Yes | `SuperAdmin`, `CompanyAdmin`, `Executive`, `HR`, `Employee` |

---

### Update Employee
```
PUT /api/employees/{id}
```
**Description:** Update employee details.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "fullName": "Jane Smith Updated",
  "phoneNumber": "+1987654321",
  "address": "456 New Address St",
  "managerId": "manager-guid-here",
  "medicalClass": "A",
  "contractEndDate": "2027-04-18"
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| fullName | string | No | Updated name |
| phoneNumber | string | No | Updated phone |
| address | string | No | Home address |
| managerId | guid | No | Direct manager |
| medicalClass | string | No | Medical insurance class |
| contractEndDate | date | No | Contract expiration |

---

### Change Employee Status
```
PATCH /api/employees/{id}/status
```
**Description:** Change employee employment status.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "status": "Active"
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| status | enum | Yes | `Active`, `Inactive`, `Suspended`, `Terminated`, `OnLeave`, `Probation` |

---

## OrgNodes

### Get Org Node Tree
```
GET /api/orgnodes
```
**Description:** Get org node tree (root nodes or children of parent).

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| parentId | guid | null | Get children of this node |
| includeAssignments | bool | false | Include employee assignments |
| depth | int | 10 | Max depth to retrieve |

---

### Get Org Node Details
```
GET /api/orgnodes/{id}
```
**Description:** Get full details of a specific node.

**Headers:** `Authorization: Bearer {token}`

---

### Get My Company Hierarchy
```
GET /api/orgnodes/my-company
```
**Description:** Get full hierarchy tree for logged-in user's company.

**Headers:** `Authorization: Bearer {token}`

---

### Create Org Node
```
POST /api/orgnodes
```
**Description:** Create a new org node.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "name": "Engineering Department",
  "parentId": "parent-node-guid",
  "code": "ENG-001"
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | Yes | Node name |
| parentId | guid | No | Parent node (null for root) |
| code | string | No | Optional code |

---

### Update Org Node
```
PUT /api/orgnodes/{id}
```
**Description:** Update org node.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "name": "Engineering Division",
  "code": "ENG-UPD"
}
```

---

### Delete Org Node
```
DELETE /api/orgnodes/{id}
```
**Description:** Delete org node. If leaf, hard delete. If has children/assignments, reparent.

**Headers:** `Authorization: Bearer {token}`

---

### Assign Employee to Node
```
POST /api/orgnodes/{id}/assignments
```
**Description:** Assign an employee to an org node.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "employeeId": "employee-guid-here"
}
```

---

### Unassign Employee from Node
```
DELETE /api/orgnodes/{id}/assignments/{employeeId}
```
**Description:** Unassign an employee from an org node.

**Headers:** `Authorization: Bearer {token}`

---

### Bulk Setup Org Nodes
```
POST /api/orgnodes/bulk-setup
```
**Description:** Bulk create org nodes and assignments in one transaction.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "nodes": [
    { "name": "CEO Office", "code": "CEO", "parentName": null },
    { "name": "Engineering", "code": "ENG", "parentName": "CEO Office" }
  ],
  "assignments": [
    { "employeeId": "emp-guid-1", "nodeName": "Engineering" },
    { "employeeId": "emp-guid-2", "nodeName": "CEO Office" }
  ]
}
```

---

## Requests

### Create Request (Self Service)
```
POST /api/employees/requests/me
```
**Description:** Create a new request (leave, permission, etc.).

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "requestType": "Leave",
  "data": {
    "leaveType": "Annual",
    "startDate": "2026-05-01",
    "endDate": "2026-05-05",
    "reason": "Family vacation"
  },
  "details": "Optional additional details"
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| requestType | enum | Yes | `Leave`, `Permission`, `SalarySlip`, `HRLetter`, `Resignation`, `EndOfService`, `PurchaseOrder`, `Asset`, `Loan`, `Assignment`, `Other` |
| data | object | Yes | JSON object matching request type schema |
| details | string | No | Additional notes |

**Request Types & Data Schemas:**

#### Leave Request
```json
{
  "leaveType": "Annual",
  "startDate": "2026-05-01",
  "endDate": "2026-05-05",
  "reason": "Family vacation"
}
```
`leaveType`: `Annual`, `Emergency`, `Unpaid`, `Permission`, `Sick`, `Maternity`, `Paternity`, `Other`

#### Permission Request
```json
{
  "permissionType": "Hourly",
  "date": "2026-05-15",
  "startTime": "14:00:00",
  "endTime": "15:00:00",
  "reason": "Doctor appointment"
}
```

#### Salary Slip Request
```json
{
  "month": 5,
  "year": 2026
}
```

#### HR Letter Request
```json
{
  "letterType": "Employment",
  "purpose": "Bank loan application"
}
```

#### Resignation Request
```json
{
  "lastWorkingDay": "2026-06-01",
  "reason": "Career growth opportunity"
}
```

#### Purchase Order Request
```json
{
  "itemDescription": "Office chair",
  "quantity": 2,
  "estimatedCost": 500.00,
  "budgetCode": "OFFICE-001"
}
```

#### Asset Request
```json
{
  "assetType": "Laptop",
  "specifications": "MacBook Pro 14-inch",
  "reason": "Current device is slow"
}
```

#### Loan Request
```json
{
  "loanType": "Personal",
  "amount": 10000.00,
  "reason": "Home renovation",
  "repaymentMonths": 12
}
```

---

### Get My Requests
```
GET /api/employees/requests/me
```
**Description:** Get current user's request history.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| requestType | enum | null | Filter by type |
| status | enum | null | `Pending`, `Approved`, `Rejected`, `Cancelled` |
| page | int | 1 | Page number |
| pageSize | int | 20 | Items per page |

---

### Get My Request by ID
```
GET /api/employees/requests/me/{id}
```
**Description:** Get details of a specific request.

**Headers:** `Authorization: Bearer {token}`

---

### Update Pending Request
```
PUT /api/employees/requests/me/{id}
```
**Description:** Update a pending request (only if no approvals taken).

**Headers:** `Authorization: Bearer {token}`

**Request Body:** (same as Create Request)

---

### Delete Pending Request
```
DELETE /api/employees/requests/me/{id}
```
**Description:** Delete a pending request (only if no approvals yet).

**Headers:** `Authorization: Bearer {token}`

---

### Get Pending Approvals
```
GET /api/employees/requests/approvals/pending
```
**Description:** Get requests pending for current user's approval.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| requestType | enum | null | Filter by type |
| page | int | 1 | Page number |
| pageSize | int | 20 | Items per page |

---

### Approve Request
```
POST /api/employees/requests/approvals/{id}/approve
```
**Description:** Approve a request.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "comment": "Approved. Enjoy your leave!"
}
```

---

### Reject Request
```
POST /api/employees/requests/approvals/{id}/reject
```
**Description:** Reject a request.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "comment": "Rejected due to insufficient notice period."
}
```

---

### Get Company-Wide Requests (Admin)
```
GET /api/employees/requests/admin/company-wide
```
**Description:** Get all requests for the company (Admins/Oversight).

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| requestType | enum | null | Filter by type |
| status | enum | null | Filter by status |
| employeeId | guid | null | Filter by employee |
| page | int | 1 | Page number |
| pageSize | int | 20 | Items per page |

---

## RequestDefinitions

### Create Request Definition
```
POST /api/request-definitions
```
**Description:** Create workflow definition for a request type.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "requestType": "Leave",
  "steps": [
    {
      "stepType": "HierarchyLevel",
      "startFromLevel": 1,
      "levelsUp": 2,
      "sortOrder": 1
    },
    {
      "stepType": "DirectEmployee",
      "directEmployeeId": "special-approver-guid",
      "sortOrder": 2
    }
  ]
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| requestType | enum | Yes | Request type this definition applies to |
| steps | array | Yes | Ordered list of approval steps |

**Step Types:**

| StepType | Required Fields | Description |
|----------|----------------|-------------|
| `OrgNode` | `orgNodeId` | Approves by all managers in an org node |
| `DirectEmployee` | `directEmployeeId` | Approves by a specific employee |
| `HierarchyLevel` | `levelsUp` (required), `startFromLevel` (optional, default 1) | Approves by N levels up in hierarchy |

**Validation Rules:**
- `HierarchyLevel` `levelsUp` must be >= 1
- `HierarchyLevel` `startFromLevel` must be >= 1 if set
- HierarchyLevel steps cannot have `orgNodeId`, `directEmployeeId`, or `bypassHierarchyCheck`
- OrgNode/DirectEmployee steps cannot have `startFromLevel` or `levelsUp`
- No overlapping HierarchyLevel ranges
- DirectEmployee cannot be a manager at any OrgNode step in the same definition

**Mixed Types Example:**
```json
{
  "requestType": "Leave",
  "steps": [
    { "stepType": "HierarchyLevel", "startFromLevel": 1, "levelsUp": 2, "sortOrder": 1 },
    { "stepType": "OrgNode", "orgNodeId": "hr-node-guid", "bypassHierarchyCheck": false, "sortOrder": 2 },
    { "stepType": "DirectEmployee", "directEmployeeId": "ceo-guid", "sortOrder": 3 }
  ]
}
```

---

### Update Request Definition
```
PUT /api/request-definitions/{id}
```
**Description:** Update existing workflow definition.

**Headers:** `Authorization: Bearer {token}`

**Request Body:** (same as Create)

---

### Delete Request Definition
```
DELETE /api/request-definitions/{id}
```
**Description:** Delete a workflow definition.

**Headers:** `Authorization: Bearer {token}`

---

### Get Request Definitions
```
GET /api/request-definitions
```
**Description:** Get all workflow definitions for the company.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| isActive | bool | null | Filter by active status |

---

### Get Request Types
```
GET /api/request-definitions/types
```
**Description:** Get all available request types.

---

### Get Request Schemas
```
GET /api/request-definitions/schemas
```
**Description:** Get schema definitions for all request types.

---

### Preview Approval Chain
```
POST /api/request-definitions/preview
```
**Description:** Preview resolved approval chain for an employee.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "requestType": "Leave",
  "employeeId": "employee-guid-here"
}
```

---

## Attendance

### Clock In
```
POST /api/attendance/clock-in
```
**Description:** Record employee clock-in.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "timestampUtc": "2026-04-18T09:00:00Z"
}
```
*If timestamp not provided, uses server current time.*

---

### Clock Out
```
POST /api/attendance/clock-out
```
**Description:** Record employee clock-out.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "timestampUtc": "2026-04-18T17:30:00Z"
}
```

---

### Get My Attendance
```
GET /api/attendance/me
```
**Description:** Get current user's attendance records.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| fromDate | date | null | Start date filter |
| toDate | date | null | End date filter |
| pageNumber | int | 1 | Page number |
| pageSize | int | 20 | Items per page |

---

### Get Company Attendance (HR+)
```
GET /api/attendance
```
**Description:** Get company-wide attendance records.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| fromDate | date | null | Start date filter |
| toDate | date | null | End date filter |
| employeeId | guid | null | Filter by employee |
| status | enum | null | Filter by status |
| isLate | bool | null | Filter late arrivals |
| isEarlyLeave | bool | null | Filter early leaves |
| pageNumber | int | 1 | Page number |
| pageSize | int | 20 | Items per page |

---

### Override Clock Out (Admin)
```
POST /api/attendance/admin/override-clock-out
```
**Description:** Manually set clock-out time for an employee.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "employeeId": "employee-guid",
  "date": "2026-04-15",
  "clockOutUtc": "2026-04-15T18:00:00Z",
  "reason": "Required to work overtime due to urgent deadline"
}
```

---

### Batch Override Clock Out (Admin)
```
POST /api/attendance/admin/override-clock-out/batch
```
**Description:** Batch override clock-out times.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "items": [
    {
      "employeeId": "emp-guid-1",
      "date": "2026-04-15",
      "clockOutUtc": "2026-04-15T18:00:00Z",
      "reason": "Team event"
    },
    {
      "employeeId": "emp-guid-2",
      "date": "2026-04-15",
      "clockOutUtc": "2026-04-15T19:00:00Z",
      "reason": "Client meeting ran late"
    }
  ]
}
```

---

## Notifications

### Get My Notifications
```
GET /api/notifications/me
```
**Description:** Get current user's notifications.

**Headers:** `Authorization: Bearer {token}`

---

### Mark Notification as Read
```
PATCH /api/notifications/{id}/read
```
**Description:** Mark a notification as read.

**Headers:** `Authorization: Bearer {token}`

---

### Send Notification to Employee (HR+)
```
POST /api/notifications/send
```
**Description:** Send notification to specific employee.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "employeeId": "employee-guid",
  "title": "Reminder: Submit Timesheet",
  "message": "Please submit your weekly timesheet by Friday.",
  "type": "Reminder"
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| employeeId | guid | Yes | Target employee |
| title | string | Yes | Notification title |
| message | string | Yes | Notification body |
| type | enum | No | `Info`, `Reminder`, `Approval`, `Rejection`, `Alert` |

---

### Broadcast Notification (HR+)
```
POST /api/notifications/broadcast
```
**Description:** Send notification to all company employees.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "title": "Company Holiday",
  "message": "Office will be closed on April 20th for team building event.",
  "type": "Info"
}
```

---

## Admin

### Update Employee Leave Balance
```
PUT /api/admin/employees/{employeeId}/leave-balances
```
**Description:** Manually update or initialize employee leave balance.

**Headers:** `Authorization: Bearer {token}`

**Request Body:**
```json
{
  "leaveType": "Annual",
  "year": 2026,
  "totalDays": 21.0
}
```
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| leaveType | enum | Yes | `Annual`, `Emergency`, `Unpaid`, `Permission`, `Sick`, `Maternity`, `Paternity`, `Other` |
| year | int | Yes | Leave year |
| totalDays | decimal | Yes | Total days allocated |

---

### Initialize Yearly Balances
```
POST /api/admin/initialize-leave-year/{year}
```
**Description:** Initialize leave balances for all active employees for a year.

**Headers:** `Authorization: Bearer {token}`

---

## Storage (MinIO)

### Upload File
```
POST /api/minio/upload
```
**Description:** Upload a file to MinIO storage.

**Headers:** `Authorization: Bearer {token}`

**Form Data:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| file | file | Yes | File to upload |
| bucketName | string | Yes | Target bucket name |
| objectName | string | Yes | Object name in bucket |
| prefix | string | No | Folder prefix |

---

### Get Presigned URL
```
GET /api/minio/get-url
```
**Description:** Get temporary download URL for an object.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| bucketName | string | Yes | Bucket name |
| objectName | string | Yes | Object name |
| expirySeconds | int | 86400 | URL expiration (default 24 hours) |

---

### Delete Object
```
DELETE /api/minio/delete
```
**Description:** Delete an object from MinIO.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| bucketName | string | Yes | Bucket name |
| objectName | string | Yes | Object name |

---

### List Objects
```
GET /api/minio/list-objects
```
**Description:** List objects under a prefix.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| bucketName | string | Yes | Bucket name |
| prefix | string | null | Folder prefix |
| recursive | bool | true | Search recursively |
| versions | bool | false | Include version history |

---

### Check Bucket Exists
```
GET /api/minio/bucket-exists
```
**Description:** Check if a bucket exists.

**Headers:** `Authorization: Bearer {token}`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| bucketName | string | Yes | Bucket name |

---

## Enums Reference

### RequestType
- `Leave` (0)
- `Permission` (1)
- `SalarySlip` (2)
- `HRLetter` (3)
- `Resignation` (4)
- `EndOfService` (5)
- `PurchaseOrder` (6)
- `Asset` (7)
- `Loan` (8)
- `Assignment` (9)
- `Other` (10)

### WorkflowStepType
- `OrgNode` (0)
- `DirectEmployee` (1)
- `HierarchyLevel` (2)

### UserRole
- `SuperAdmin` (0)
- `CompanyAdmin` (1)
- `Executive` (2)
- `HR` (3)
- `Employee` (4)

### EmploymentStatus
- `Active` (0)
- `Inactive` (1)
- `Suspended` (2)
- `Terminated` (3)
- `OnLeave` (4)
- `Probation` (5)

### LeaveType
- `Annual` (0)
- `Emergency` (1)
- `Unpaid` (2)
- `Permission` (3)
- `Sick` (4)
- `Maternity` (5)
- `Paternity` (6)
- `Other` (7)

### DeviceType
- `Android` (0)
- `iOS` (1)
- `Web` (2)

### OtpChannel
- `Email` (0)
- `Sms` (1)

### CompanyStatus
- `Active` (0)
- `Inactive` (1)
- `Suspended` (2)

### AttendanceStatus
- `Present` (0)
- `Absent` (1)
- `Late` (2)
- `EarlyLeave` (3)
- `OnLeave` (4)

### NotificationType
- `Info` (0)
- `Reminder` (1)
- `Approval` (2)
- `Rejection` (3)
- `Alert` (4)
