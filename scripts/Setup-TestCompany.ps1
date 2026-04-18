#Requires -Version 5.1
<#
.SYNOPSIS
    Sets up a complete test company with hierarchy, employees, assignments, and request definitions.

.DESCRIPTION
    This script creates:
    - A deep OrgNode hierarchy (4 levels: Division -> Department -> Team)
    - Employees assigned to the company
    - Employee assignments to org nodes with Manager/Member roles
    - Request definitions for all 11 request types with OrgNode-based approval chains

.PARAMETER BaseUrl
    The API base URL. Default: http://localhost:5119

.PARAMETER SuperAdminToken
    JWT token for SuperAdmin authentication. If not provided, script will attempt to login.

.PARAMETER SuperAdminEmail
    SuperAdmin email for login. Default: superadmin@hrms.com

.PARAMETER SuperAdminPassword
    SuperAdmin password for login. Default: SuperAdmin@123

.PARAMETER CompanyId
    Existing company ID to use. If not provided, script will create a new company.

.PARAMETER CompanyName
    Name for new company if CompanyId not provided. Default: "Test Company"

.EXAMPLE
    .\Setup-TestCompany.ps1
    Runs with defaults (creates new company if needed)

.EXAMPLE
    .\Setup-TestCompany.ps1 -BaseUrl "http://localhost:5000" -SuperAdminToken "eyJ..."
    Uses existing token and default base URL
#>

param(
    [string]$BaseUrl = "http://localhost:5119",

    [string]$SuperAdminToken,

    [string]$SuperAdminEmail = "superadmin@hrms.com",

    [string]$SuperAdminPassword = "SuperAdmin@123",

    [string]$CompanyId,

    [string]$CompanyName = "Test Company"
)

$ErrorActionPreference = "Stop"

# JSON Helper
function Invoke-Api {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body,
        [string]$Token
    )

    $headers = @{
        "Content-Type" = "application/json"
    }
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $params = @{
        Method = $Method
        Uri = "$BaseUrl$Endpoint"
        Headers = $headers
    }

    if ($Body) {
        $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
    }

    try {
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        Write-Host "API Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = [System.IO.StreamReader]::new($stream)
            $errorBody = $reader.ReadToEnd()
            $reader.Close()
            Write-Host "Response: $errorBody" -ForegroundColor Red
        }
        throw
    }
}

# Login if token not provided
if (-not $SuperAdminToken) {
    Write-Host "Logging in as SuperAdmin..." -ForegroundColor Cyan
    $loginResult = Invoke-Api -Method "POST" -Endpoint "/api/auth/login" -Body @{
        email = $SuperAdminEmail
        password = $SuperAdminPassword
    }

    if (-not $loginResult.isSuccess) {
        Write-Host "Login failed: $($loginResult.error.message)" -ForegroundColor Red
        exit 1
    }

    $SuperAdminToken = $loginResult.data.token
    Write-Host "Logged in successfully" -ForegroundColor Green
}

# Get or create company
if (-not $CompanyId) {
    Write-Host "Checking for existing company '$CompanyName'..." -ForegroundColor Cyan

    $companiesResult = Invoke-Api -Method "GET" -Endpoint "/api/companies" -Token $SuperAdminToken

    $existingCompany = $companiesResult.data.items | Where-Object { $_.companyName -eq $CompanyName } | Select-Object -First 1

    if ($existingCompany) {
        $CompanyId = $existingCompany.id
        Write-Host "Using existing company: $CompanyName ($CompanyId)" -ForegroundColor Green
    }
    else {
        Write-Host "Company '$CompanyName' not found. Please create it first or provide -CompanyId" -ForegroundColor Yellow
        exit 1
    }
}
else {
    Write-Host "Using provided company ID: $CompanyId" -ForegroundColor Cyan
}

# Get company info
$companyResult = Invoke-Api -Method "GET" -Endpoint "/api/companies/$CompanyId" -Token $SuperAdminToken
Write-Host "Company: $($companyResult.data.companyName)" -ForegroundColor Green

# =============================================================================
# STEP 1: Create Hierarchy
# =============================================================================
Write-Host "`n=== Creating OrgNode Hierarchy ===" -ForegroundColor Cyan

$hierarchyResult = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes/bulk-setup" -Token $SuperAdminToken -Body @{
    request = @{
        companyId = $CompanyId
        nodes = @(
            @{ tempId = "exec"; name = "Executive"; type = "Division"; parentTempId = $null },
            @{ tempId = "ceo-office"; name = "CEO Office"; type = "Department"; parentTempId = "exec" },
            @{ tempId = "strategy"; name = "Strategy"; type = "Department"; parentTempId = "exec" },
            @{ tempId = "fin"; name = "Finance"; type = "Division"; parentTempId = $null },
            @{ tempId = "accounting"; name = "Accounting"; type = "Department"; parentTempId = "fin" },
            @{ tempId = "payables"; name = "Payables"; type = "Team"; parentTempId = "accounting" },
            @{ tempId = "receivables"; name = "Receivables"; type = "Team"; parentTempId = "accounting" },
            @{ tempId = "treasury"; name = "Treasury"; type = "Department"; parentTempId = "fin" },
            @{ tempId = "hr"; name = "Human Resources"; type = "Division"; parentTempId = $null },
            @{ tempId = "talent-acq"; name = "Talent Acquisition"; type = "Department"; parentTempId = "hr" },
            @{ tempId = "campus-recruit"; name = "Campus Recruitment"; type = "Team"; parentTempId = "talent-acq" },
            @{ tempId = "exec-search"; name = "Executive Search"; type = "Team"; parentTempId = "talent-acq" },
            @{ tempId = "emp-relations"; name = "Employee Relations"; type = "Department"; parentTempId = "hr" },
            @{ tempId = "training-dev"; name = "Training & Development"; type = "Department"; parentTempId = "hr" },
            @{ tempId = "eng"; name = "Engineering"; type = "Division"; parentTempId = $null },
            @{ tempId = "frontend"; name = "Frontend"; type = "Department"; parentTempId = "eng" },
            @{ tempId = "web"; name = "Web"; type = "Team"; parentTempId = "frontend" },
            @{ tempId = "mobile"; name = "Mobile"; type = "Team"; parentTempId = "frontend" },
            @{ tempId = "backend"; name = "Backend"; type = "Department"; parentTempId = "eng" },
            @{ tempId = "api"; name = "API"; type = "Team"; parentTempId = "backend" },
            @{ tempId = "infra"; name = "Infrastructure"; type = "Team"; parentTempId = "backend" },
            @{ tempId = "qa"; name = "QA"; type = "Department"; parentTempId = "eng" }
        )
    }
}

if (-not $hierarchyResult.isSuccess) {
    Write-Host "Failed to create hierarchy: $($hierarchyResult.error.message)" -ForegroundColor Red
    exit 1
}

# Extract node IDs
$nodeMap = @{}
$hierarchyResult.data.nodes | ForEach-Object { $nodeMap[$_.tempId] = $_.realId }

Write-Host "Created $($hierarchyResult.data.nodes.Count) nodes" -ForegroundColor Green

# =============================================================================
# STEP 2: Create Employees
# =============================================================================
Write-Host "`n=== Creating Employees ===" -ForegroundColor Cyan

$employees = @(
    @{ fullName = "John CEO"; email = "ceo@democompany.com"; phoneNumber = "11111111111"; role = 2 },
    @{ fullName = "Jane CFO"; email = "cfo@democompany.com"; phoneNumber = "22222222222"; role = 2 },
    @{ fullName = "Bob HR Director"; email = "hr-dir@democompany.com"; phoneNumber = "33333333333"; role = 3 },
    @{ fullName = "Alice CTO"; email = "cto@democompany.com"; phoneNumber = "44444444444"; role = 2 },
    @{ fullName = "Charlie Developer"; email = "dev1@democompany.com"; phoneNumber = "55555555555"; role = 4 },
    @{ fullName = "Diana Developer"; email = "dev2@democompany.com"; phoneNumber = "66666666666"; role = 4 },
    @{ fullName = "Eve QA"; email = "qa1@democompany.com"; phoneNumber = "77777777777"; role = 4 },
    @{ fullName = "Frank Finance"; email = "fin1@democompany.com"; phoneNumber = "88888888888"; role = 4 },
    @{ fullName = "Grace HR"; email = "hr1@democompany.com"; phoneNumber = "99999999999"; role = 4 }
)

$createdEmployees = @()

foreach ($emp in $employees) {
    Write-Host "  Creating $($emp.fullName)..." -NoNewline

    $result = Invoke-Api -Method "POST" -Endpoint "/api/employees" -Token $SuperAdminToken -Body @{
        fullName = $emp.fullName
        email = $emp.email
        phoneNumber = $emp.phoneNumber
        companyId = $CompanyId
        role = $emp.role
    }

    if ($result.isSuccess) {
        $empData = @{
            id = $result.data.employeeId
            fullName = $result.data.fullName
            email = $result.data.email
            role = $result.data.role
            tempPassword = $result.data.temporaryPassword
        }
        $createdEmployees += $empData
        Write-Host " $($empData.id)" -ForegroundColor Green
    }
    else {
        Write-Host " FAILED: $($result.error.message)" -ForegroundColor Red
    }
}

Write-Host "Created $($createdEmployees.Count) employees" -ForegroundColor Green

# =============================================================================
# STEP 3: Assign Employees to Nodes
# =============================================================================
Write-Host "`n=== Assigning Employees to Nodes ===" -ForegroundColor Cyan

$assignments = @(
    @{ nodeTempId = "exec"; empEmail = "ceo@democompany.com"; role = 0 },        # Manager
    @{ nodeTempId = "fin"; empEmail = "cfo@democompany.com"; role = 0 },         # Manager
    @{ nodeTempId = "hr"; empEmail = "hr-dir@democompany.com"; role = 0 },       # Manager
    @{ nodeTempId = "eng"; empEmail = "cto@democompany.com"; role = 0 },         # Manager
    @{ nodeTempId = "accounting"; empEmail = "fin1@democompany.com"; role = 0 }, # Manager
    @{ nodeTempId = "payables"; empEmail = "dev2@democompany.com"; role = 1 },    # Member (Diana)
    @{ nodeTempId = "talent-acq"; empEmail = "hr1@democompany.com"; role = 0 },   # Manager
    @{ nodeTempId = "campus-recruit"; empEmail = "dev1@democompany.com"; role = 1 }, # Member (Charlie)
    @{ nodeTempId = "web"; empEmail = "dev1@democompany.com"; role = 0 },        # Manager
    @{ nodeTempId = "qa"; empEmail = "qa1@democompany.com"; role = 0 }          # Manager
)

$roleNames = @("Manager", "Member")

foreach ($assign in $assignments) {
    $emp = $createdEmployees | Where-Object { $_.email -eq $assign.empEmail } | Select-Object -First 1
    if (-not $emp) {
        Write-Host "  Employee not found: $($assign.empEmail)" -ForegroundColor Yellow
        continue
    }

    $nodeId = $nodeMap[$assign.nodeTempId]
    Write-Host "  Assigning $($emp.fullName) to $($assign.nodeTempId) as $($roleNames[$assign.role])..." -NoNewline

    $result = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes/$nodeId/assignments" -Token $SuperAdminToken -Body @{
        employeeId = $emp.id
        role = $assign.role
    }

    if ($result.isSuccess) {
        Write-Host " OK" -ForegroundColor Green
    }
    else {
        Write-Host " FAILED: $($result.error.message)" -ForegroundColor Red
    }
}

# =============================================================================
# STEP 4: Create Request Definitions
# =============================================================================
Write-Host "`n=== Creating Request Definitions ===" -ForegroundColor Cyan

$requestDefs = @(
    @{ type = 0; name = "Leave"; steps = @(@{ nodeTempId = "hr"; sortOrder = 0 }) },
    @{ type = 1; name = "Permission"; steps = @(@{ nodeTempId = "hr"; sortOrder = 0 }) },
    @{ type = 2; name = "SalarySlip"; steps = @(@{ nodeTempId = "fin"; sortOrder = 0 }) },
    @{ type = 3; name = "HRLetter"; steps = @(@{ nodeTempId = "hr"; sortOrder = 0 }) },
    @{ type = 4; name = "Resignation"; steps = @(@{ nodeTempId = "exec"; sortOrder = 0 }) },
    @{ type = 5; name = "EndOfService"; steps = @(@{ nodeTempId = "exec"; sortOrder = 0 }) },
    @{ type = 6; name = "PurchaseOrder"; steps = @(
        @{ nodeTempId = "accounting"; sortOrder = 0 },
        @{ nodeTempId = "fin"; sortOrder = 1 }
    ) },
    @{ type = 7; name = "Asset"; steps = @(@{ nodeTempId = "fin"; sortOrder = 0 }) },
    @{ type = 8; name = "Loan"; steps = @(
        @{ nodeTempId = "hr"; sortOrder = 0 },
        @{ nodeTempId = "exec"; sortOrder = 1 }
    ) },
    @{ type = 9; name = "Assignment"; steps = @(@{ nodeTempId = "eng"; sortOrder = 0 }) },
    @{ type = 10; name = "Other"; steps = @(@{ nodeTempId = "hr"; sortOrder = 0 }) }
)

foreach ($def in $requestDefs) {
    Write-Host "  Creating $($def.name) request definition..." -NoNewline

    $steps = $def.steps | ForEach-Object {
        @{
            stepType = 0  # OrgNode
            orgNodeId = $nodeMap[$_.nodeTempId]
            sortOrder = $_.sortOrder
        }
    }

    $result = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $SuperAdminToken -Body @{
        companyId = $CompanyId
        requestType = $def.type
        steps = $steps
    }

    if ($result.isSuccess) {
        Write-Host " $($result.data)" -ForegroundColor Green
    }
    else {
        Write-Host " FAILED: $($result.error.message)" -ForegroundColor Red
    }
}

# =============================================================================
# Summary
# =============================================================================
Write-Host "`n=== Setup Complete ===" -ForegroundColor Green
Write-Host "Company ID: $CompanyId" -ForegroundColor White
Write-Host "Nodes Created: $($hierarchyResult.data.nodes.Count)" -ForegroundColor White
Write-Host "Employees Created: $($createdEmployees.Count)" -ForegroundColor White
Write-Host "Request Definitions Created: $($requestDefs.Count)" -ForegroundColor White

Write-Host "`nEmployee Credentials (temporary passwords):" -ForegroundColor Yellow
$createdEmployees | ForEach-Object {
    Write-Host "  $($_.email) - $($_.tempPassword)" -ForegroundColor White
}

Write-Host "`nNode Hierarchy:" -ForegroundColor Yellow
$hierarchyResult.data.nodes | Sort-Object depth | ForEach-Object {
    $indent = "  " * $_.depth
    Write-Host "  $($indent)$($_.name)" -ForegroundColor White
}
