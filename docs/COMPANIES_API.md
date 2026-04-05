# Companies API Documentation

## Base URL
```
/api/companies
```

## Authentication
All endpoints require JWT Bearer authentication unless otherwise specified.

---

## Endpoints

### 1. Create Company
**`POST /api/companies`**

Creates a new company in the system.

| Property | Description |
|----------|-------------|
| **Authorization** | SuperAdmin only |
| **Request Body** | `CreateCompanyRequest` |
| **Response** | `CompanyResponse` |

**Request Body:**
```json
{
  "companyName": "string",
  "companyLogoUrl": "string (nullable)",
  "yearlyVacationDays": "integer"
}
```

---

### 2. Get All Companies
**`GET /api/companies`**

Retrieves all companies with optional filtering and pagination.

| Property | Description |
|----------|-------------|
| **Authorization** | SuperAdmin only |
| **Query Parameters** | `searchTerm`, `status`, `pageNumber`, `pageSize`, `includeLocations`, `includeDepartments` |
| **Response** | `PagedResult<CompanyResponse>` |

---

### 3. Get Company By ID
**`GET /api/companies/{id:guid}`**

Retrieves a specific company by its ID.

| Property | Description |
|----------|-------------|
| **Authorization** | SuperAdmin only |
| **Path Parameters** | `id` (Company GUID) |
| **Query Parameters** | `includeLocations`, `includeDepartments` |
| **Response** | `CompanyResponse` |

---

### 4. Update Company
**`PUT /api/companies/{id:guid}`**

Updates an existing company's information.

| Property | Description |
|----------|-------------|
| **Authorization** | CompanyAdmins (can only update their own company) |
| **Path Parameters** | `id` (Company GUID) |
| **Request Body** | `UpdateCompanyRequest` |
| **Response** | `CompanyResponse` |

**Request Body:**
```json
{
  "companyName": "string",
  "companyLogoUrl": "string (nullable)",
  "yearlyVacationDays": "integer"
}
```

**Notes:**
- CompanyAdmins can only update their own company
- SuperAdmin can update any company

---

### 5. Get My Company
**`GET /api/companies/me`**

Retrieves the company associated with the currently logged-in user.

| Property | Description |
|----------|-------------|
| **Authorization** | CompanyAdmins |
| **Query Parameters** | `includeLocations`, `includeDepartments` |
| **Response** | `CompanyResponse` |

---

### 6. Update My Company
**`PUT /api/companies/me`**

Updates the company of the currently logged-in user.

| Property | Description |
|----------|-------------|
| **Authorization** | CompanyAdmins |
| **Request Body** | `UpdateCompanyRequest` |
| **Response** | `CompanyResponse` |

---

### 7. Change Company Status
**`PATCH /api/companies/{id:guid}/status`**

Changes the status of a company (Active/Inactive).

| Property | Description |
|----------|-------------|
| **Authorization** | SuperAdmin only |
| **Path Parameters** | `id` (Company GUID) |
| **Request Body** | `ChangeCompanyStatusRequest` |
| **Response** | `CompanyResponse` |

**Request Body:**
```json
{
  "status": "Active | Inactive"
}
```

---

### 8. Get Company Locations
**`GET /api/companies/{companyId:guid}/locations`**

Retrieves all locations for a specific company.

| Property | Description |
|----------|-------------|
| **Authorization** | HrOrAbove (SuperAdmin, CEO, VP, DepartmentManager, UnitLeader, TeamLeader, HR, CompanyAdmin) |
| **Path Parameters** | `companyId` (Company GUID) |
| **Response** | `IReadOnlyList<CompanyLocationResponse>` |

**Notes:**
- SuperAdmin can specify any companyId
- Other users can only view locations of their own company

---

### 9. Create Location
**`POST /api/companies/{companyId:guid}/locations`**

Adds a new location to a company.

| Property | Description |
|----------|-------------|
| **Authorization** | CompanyAdmins |
| **Path Parameters** | `companyId` (Company GUID) |
| **Request Body** | `CreateCompanyLocationRequest` |
| **Response** | `CompanyLocationResponse` |

**Request Body:**
```json
{
  "locationName": "string",
  "address": "string (nullable)",
  "latitude": "double (nullable)",
  "longitude": "double (nullable)"
}
```

**Notes:**
- CompanyAdmins can only create locations for their own company
- SuperAdmin can create locations for any company

---

### 10. Delete Location
**`DELETE /api/companies/locations/{id:guid}`**

Deletes a company location.

| Property | Description |
|----------|-------------|
| **Authorization** | CompanyAdmins |
| **Path Parameters** | `id` (Location GUID) |
| **Response** | `Guid` (Deleted location ID) |

**Notes:**
- CompanyAdmins can only delete locations from their own company
- SuperAdmin can delete any location

---

### 11. Get Company Hierarchy
**`GET /api/companies/hierarchy`**

Gets the full organizational hierarchy for the current user's company.

| Property | Description |
|----------|-------------|
| **Authorization** | Viewers (SuperAdmin, CEO, VP, DepartmentManager, UnitLeader, TeamLeader, HR) |
| **Response** | Hierarchy response |

---

### 12. Configure Hierarchy Positions
**`POST /api/companies/hierarchy/positions`**

Configures the allowed roles and their order in the company hierarchy.

| Property | Description |
|----------|-------------|
| **Authorization** | CompanyAdmins |
| **Request Body** | `List<HierarchyPositionInputDto>` |
| **Response** | Command result |

---

## Authorization Roles Summary

| Role | Access Level |
|------|--------------|
| **SuperAdmin** | Full access to all companies and locations |
| **CompanyAdmin** | Access to their own company only |
| **HR** | Can view locations, access to their own company |
| **CEO, VP, DepartmentManager, UnitLeader, TeamLeader** | Can view hierarchy and locations of their company |

---

## Common Response Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 400 | Bad Request |
| 401 | Unauthorized |
| 403 | Forbidden (not authorized for this resource) |
| 404 | Not Found |
