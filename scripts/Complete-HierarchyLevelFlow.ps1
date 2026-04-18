#Requires -Version 5.1
<#
.SYNOPSIS
    Complete demo flow for HierarchyLevel feature.
    Creates a fresh company, 3-level hierarchy, employees, assignments,
    request definitions (all 3 step types), and test requests.
#>

param(
    [string]$BaseUrl = "http://localhost:5119",
    [string]$SuperAdminEmail = "superadmin@hrms.com",
    [string]$SuperAdminPassword = "SuperAdmin@123",
    [string]$CompanyName = "DemoCo"
)

$ErrorActionPreference = "Continue"

function Invoke-Api {
    param([string]$Method, [string]$Endpoint, [object]$Body, [string]$Token)
    $headers = @{ "Content-Type" = "application/json" }
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $params = @{ Method = $Method; Uri = "$BaseUrl$Endpoint"; Headers = $headers; ErrorAction = "SilentlyContinue" }
    if ($Body) { $params["Body"] = ($Body | ConvertTo-Json -Depth 10) }
    try {
        $r = Invoke-RestMethod @params
        return $r
    } catch {
        return @{ isSuccess = $false; error = @{ message = $_.Exception.Message; code = "HTTP_ERROR" } }
    }
}

# ── Login ─────────────────────────────────────────────────────────────────────
Write-Host "=== LOGIN ===" -ForegroundColor Cyan
$login = Invoke-Api -Method "POST" -Endpoint "/api/auth/login" -Body @{ email = $SuperAdminEmail; password = $SuperAdminPassword }
if (-not $login.isSuccess) { Write-Host "Login failed!" -ForegroundColor Red; exit 1 }
$token = $login.data.token
Write-Host "Logged in as $SuperAdminEmail" -ForegroundColor Green

# ── Create Fresh Company ──────────────────────────────────────────────────────
Write-Host "`n=== CREATE COMPANY: $CompanyName ===" -ForegroundColor Cyan
$ts = [int](Get-Date -UFormat "%s")

$newCo = Invoke-Api -Method "POST" -Endpoint "/api/companies" -Token $token -Body @{
    companyName = $CompanyName
    companyLogoUrl = $null
    yearlyVacationDays = 20
    startTime = "09:00:00"
    endTime = "17:00:00"
    graceMinutes = 15
    timeZoneId = "UTC"
}

if (-not $newCo.isSuccess) {
    Write-Host "Could not create company: $($newCo.error.message)" -ForegroundColor Yellow
    # Try to get existing company by name
    $cos = Invoke-Api -Method "GET" -Endpoint "/api/companies" -Token $token
    $found = $null
    if ($cos.data -and $cos.data.items) {
        $found = $cos.data.items | Where-Object { $_.companyName -eq $CompanyName } | Select-Object -First 1
    }
    if ($found) {
        $companyId = $found.id
        Write-Host "Using existing company: $CompanyName ($companyId)" -ForegroundColor Yellow
    } else {
        Write-Host "Could not find company. Exiting." -ForegroundColor Red; exit 1
    }
} else {
    $companyId = $newCo.data.id
    Write-Host "Created company: $CompanyName ($companyId)" -ForegroundColor Green
}

# Wait for company to propagate
Start-Sleep -Milliseconds 500

# ── Build 3-Level Hierarchy ───────────────────────────────────────────────────
Write-Host "`n=== BUILD HIERARCHY ===" -ForegroundColor Cyan

# Create root divisions first
Write-Host "Creating divisions..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "Engineering"; type = "Division"; parentId = $null; companyId = $companyId
}
if ($r.isSuccess) { $engDivId = $r.data; Write-Host "  Engineering Division: $engDivId" -ForegroundColor Green }
else { Write-Host "  Engineering failed: $($r.error.message)" -ForegroundColor Red }

$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "Human Resources"; type = "Division"; parentId = $null; companyId = $companyId
}
if ($r.isSuccess) { $hrDivId = $r.data; Write-Host "  HR Division: $hrDivId" -ForegroundColor Green }

$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "Finance"; type = "Division"; parentId = $null; companyId = $companyId
}
if ($r.isSuccess) { $finDivId = $r.data; Write-Host "  Finance Division: $finDivId" -ForegroundColor Green }

# Create departments under Engineering
Write-Host "Creating departments..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "Backend"; type = "Department"; parentId = $engDivId; companyId = $companyId
}
if ($r.isSuccess) { $backendId = $r.data; Write-Host "  Backend Dept: $backendId" -ForegroundColor Green }

$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "Frontend"; type = "Department"; parentId = $engDivId; companyId = $companyId
}
if ($r.isSuccess) { $frontendId = $r.data; Write-Host "  Frontend Dept: $frontendId" -ForegroundColor Green }

$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "Talent"; type = "Department"; parentId = $hrDivId; companyId = $companyId
}
if ($r.isSuccess) { $talentId = $r.data; Write-Host "  Talent Dept: $talentId" -ForegroundColor Green }

$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "Accounting"; type = "Department"; parentId = $finDivId; companyId = $companyId
}
if ($r.isSuccess) { $accountingId = $r.data; Write-Host "  Accounting Dept: $accountingId" -ForegroundColor Green }

# Create teams
Write-Host "Creating teams..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "API Team"; type = "Team"; parentId = $backendId; companyId = $companyId
}
if ($r.isSuccess) { $apiTeamId = $r.data; Write-Host "  API Team: $apiTeamId" -ForegroundColor Green }

$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "Web Team"; type = "Team"; parentId = $frontendId; companyId = $companyId
}
if ($r.isSuccess) { $webTeamId = $r.data; Write-Host "  Web Team: $webTeamId" -ForegroundColor Green }

$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "Recruitment"; type = "Team"; parentId = $talentId; companyId = $companyId
}
if ($r.isSuccess) { $recruitId = $r.data; Write-Host "  Recruitment Team: $recruitId" -ForegroundColor Green }

$r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes" -Token $token -Body @{
    name = "Payables"; type = "Team"; parentId = $accountingId; companyId = $companyId
}
if ($r.isSuccess) { $payablesId = $r.data; Write-Host "  Payables Team: $payablesId" -ForegroundColor Green }

# ── Create Employees ──────────────────────────────────────────────────────────
Write-Host "`n=== CREATE EMPLOYEES ===" -ForegroundColor Cyan

$employees = @(
    @{ fullName = "Mike CEO";       email = "ceo_$ts@demo.com";  phoneNumber = "22000000001"; role = 2; nodeId = $engDivId;    nodeName = "Engineering Division"; isManager = $true  },
    @{ fullName = "Sarah HR Dir";   email = "shr_$ts@demo.com";  phoneNumber = "22000000002"; role = 3; nodeId = $hrDivId;     nodeName = "HR Division";          isManager = $true  },
    @{ fullName = "Frank CFO";      email = "cfo_$ts@demo.com";  phoneNumber = "22000000003"; role = 2; nodeId = $finDivId;    nodeName = "Finance Division";      isManager = $true  },
    @{ fullName = "Tom Backend";     email = "tom_$ts@demo.com";  phoneNumber = "22000000004"; role = 4; nodeId = $backendId;   nodeName = "Backend";              isManager = $true  },
    @{ fullName = "Alice Frontend"; email = "ali_$ts@demo.com";  phoneNumber = "22000000005"; role = 4; nodeId = $frontendId;  nodeName = "Frontend";             isManager = $true  },
    @{ fullName = "Grace Talent";   email = "gra_$ts@demo.com";  phoneNumber = "22000000006"; role = 4; nodeId = $talentId;    nodeName = "Talent";               isManager = $true  },
    @{ fullName = "Bob Accounting"; email = "bob_$ts@demo.com";  phoneNumber = "22000000007"; role = 4; nodeId = $accountingId; nodeName = "Accounting";           isManager = $true  },
    @{ fullName = "Charlie Dev";    email = "chl_$ts@demo.com";  phoneNumber = "22000000008"; role = 4; nodeId = $apiTeamId;    nodeName = "API Team";             isManager = $false },
    @{ fullName = "Diana QA";      email = "dia_$ts@demo.com";  phoneNumber = "22000000009"; role = 4; nodeId = $webTeamId;    nodeName = "Web Team";            isManager = $false },
    @{ fullName = "Evan Recruit";  email = "eva_$ts@demo.com";  phoneNumber = "22000000010"; role = 4; nodeId = $recruitId;    nodeName = "Recruitment";          isManager = $false },
    @{ fullName = "Fiona Pay";     email = "fio_$ts@demo.com";  phoneNumber = "22000000011"; role = 4; nodeId = $payablesId;   nodeName = "Payables";              isManager = $false }
)

$created = @()
foreach ($emp in $employees) {
    Write-Host "  $($emp.email)..." -NoNewline
    $r = Invoke-Api -Method "POST" -Endpoint "/api/employees" -Token $token -Body @{
        fullName = $emp.fullName; email = $emp.email; phoneNumber = $emp.phoneNumber
        companyId = $companyId; role = $emp.role
    }
    if ($r.isSuccess) {
        $pwd = $r.data.temporaryPassword
        $created += @{ id = $r.data.employeeId; email = $emp.email; fullName = $emp.fullName;
                       nodeId = $emp.nodeId; nodeName = $emp.nodeName; isManager = $emp.isManager; tempPassword = $pwd }
        Write-Host " $pwd" -ForegroundColor Green
    } else {
        Write-Host " FAILED: $($r.error.message)" -ForegroundColor Red
    }
}

# ── Assign to Nodes ────────────────────────────────────────────────────────────
Write-Host "`n=== ASSIGN TO NODES ===" -ForegroundColor Cyan
foreach ($emp in $created) {
    $role = if ($emp.isManager) { 0 } else { 1 }
    $roleName = if ($role -eq 0) { "Manager" } else { "Member" }
    Write-Host "  $($emp.fullName) [$roleName] → $($emp.nodeName)..." -NoNewline
    $r = Invoke-Api -Method "POST" -Endpoint "/api/orgnodes/$($emp.nodeId)/assignments" -Token $token -Body @{
        employeeId = $emp.id; role = $role
    }
    if ($r.isSuccess) {
        Write-Host " OK" -ForegroundColor Green
    } elseif ($r.error.code -eq "OrgNode.DuplicateAssignment") {
        Write-Host " Already assigned" -ForegroundColor Yellow
    } else {
        Write-Host " $($r.error.code)" -ForegroundColor Yellow
    }
}

# ── Create Request Definitions ─────────────────────────────────────────────────
Write-Host "`n=== CREATE REQUEST DEFINITIONS ===" -ForegroundColor Cyan

# Helper to find employee ID by created record
function Get-EmpId($email) {
    $e = $created | Where-Object { $_.email -eq $email } | Select-Object -First 1
    return $e.id
}

$mikeId  = Get-EmpId "ceo_$ts@demo.com"
$sarahId = Get-EmpId "shr_$ts@demo.com"
$frankId = Get-EmpId "cfo_$ts@demo.com"
$tomId   = Get-EmpId "tom_$ts@demo.com"

$definitions = @()

# Type 0: HIERARCHY LEVEL - 3 levels up (own node + 2 ancestors)
Write-Host "  Type 0 Leave (HierarchyLevel 3 levels)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 0
    steps = @(@{ stepType = 2; startFromLevel = 1; levelsUp = 3; sortOrder = 1 })
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 0; name = "Leave"; desc = "HIERARCHY LEVEL - 3 levels (own + 2 ancestors)"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# Type 1: DIRECT EMPLOYEE - Fixed HR Director
Write-Host "  Type 1 Permission (DirectEmployee - Sarah)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 1
    steps = @(@{ stepType = 1; directEmployeeId = $sarahId; sortOrder = 1 })
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 1; name = "Permission"; desc = "DIRECT EMPLOYEE - Fixed HR approver"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# Type 2: ORG NODE - Fixed Finance division
Write-Host "  Type 2 SalarySlip (OrgNode - Finance Division)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 2
    steps = @(@{ stepType = 0; orgNodeId = $finDivId; sortOrder = 1 })
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 2; name = "SalarySlip"; desc = "ORG NODE - Finance Division node"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# Type 3: MIXED - HierarchyLevel + DirectEmployee
Write-Host "  Type 3 HRLetter (Mixed: HL-1-2 + DirectEmployee + HL-3)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 3
    steps = @(
        @{ stepType = 2; startFromLevel = 1; levelsUp = 2; sortOrder = 1 },
        @{ stepType = 1; directEmployeeId = $sarahId; sortOrder = 2 },
        @{ stepType = 2; startFromLevel = 3; levelsUp = 1; sortOrder = 3 }
    )
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 3; name = "HRLetter"; desc = "MIXED: HL-1-2 + DirectEmployee + HL-3"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# Type 4: MIXED - OrgNode + HierarchyLevel
Write-Host "  Type 4 Resignation (Mixed: OrgNode(HR) + HL-2+)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 4
    steps = @(
        @{ stepType = 0; orgNodeId = $hrDivId; sortOrder = 1 },
        @{ stepType = 2; startFromLevel = 2; levelsUp = 2; sortOrder = 2 }
    )
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 4; name = "Resignation"; desc = "MIXED: OrgNode(HR) + HierarchyLevel(2+)"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# Type 5: DIRECT EMPLOYEE - Fixed CEO
Write-Host "  Type 5 EndOfService (DirectEmployee - Mike CEO)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 5
    steps = @(@{ stepType = 1; directEmployeeId = $mikeId; sortOrder = 1 })
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 5; name = "EndOfService"; desc = "DIRECT EMPLOYEE - Fixed CEO approver"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# Type 6: MIXED - Two OrgNode steps
Write-Host "  Type 6 PurchaseOrder (Mixed: Backend + Finance)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 6
    steps = @(
        @{ stepType = 0; orgNodeId = $backendId; sortOrder = 1 },
        @{ stepType = 0; orgNodeId = $finDivId; sortOrder = 2 }
    )
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 6; name = "PurchaseOrder"; desc = "MIXED: OrgNode(Backend) + OrgNode(Finance)"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# Type 7: HIERARCHY LEVEL - 2 levels
Write-Host "  Type 7 Asset (HierarchyLevel 2 levels)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 7
    steps = @(@{ stepType = 2; startFromLevel = 1; levelsUp = 2; sortOrder = 1 })
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 7; name = "Asset"; desc = "HIERARCHY LEVEL - 2 levels (own + parent)"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# Type 8: DIRECT EMPLOYEE - Fixed CFO
Write-Host "  Type 8 Loan (DirectEmployee - Frank CFO)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 8
    steps = @(@{ stepType = 1; directEmployeeId = $frankId; sortOrder = 1 })
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 8; name = "Loan"; desc = "DIRECT EMPLOYEE - Fixed CFO approver"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# Type 9: MIXED - HL(1-2) + DirectEmployee + HL(3)
Write-Host "  Type 9 Assignment (Mixed: HL-1-2 + DirectEmployee + HL-3)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 9
    steps = @(
        @{ stepType = 2; startFromLevel = 1; levelsUp = 2; sortOrder = 1 },
        @{ stepType = 1; directEmployeeId = $sarahId; sortOrder = 2 },
        @{ stepType = 2; startFromLevel = 3; levelsUp = 1; sortOrder = 3 }
    )
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 9; name = "Assignment"; desc = "MIXED: HL(1-2) + DirectEmployee + HL(3)"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# Type 10: HIERARCHY LEVEL - 1 level (own node only)
Write-Host "  Type 10 Other (HierarchyLevel 1 level)..." -ForegroundColor White
$r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions" -Token $token -Body @{
    companyId = $companyId; requestType = 10
    steps = @(@{ stepType = 2; startFromLevel = 1; levelsUp = 1; sortOrder = 1 })
}
if ($r.isSuccess) {
    Write-Host "    OK: $($r.data)" -ForegroundColor Green; $definitions += @{ type = 10; name = "Other"; desc = "HIERARCHY LEVEL - 1 level (own node manager)"; id = $r.data }
} else { Write-Host "    $($r.error.message)" -ForegroundColor Red }

# ── Submit Test Requests ───────────────────────────────────────────────────────
Write-Host "`n=== SUBMIT TEST REQUESTS ===" -ForegroundColor Cyan

# Charlie Dev (API Team, Member) submits Leave
$charlie = $created | Where-Object { $_.fullName -eq "Charlie Dev" } | Select-Object -First 1
if ($charlie) {
    Write-Host "  Charlie (API Team) → Leave (Type 0, HierarchyLevel 3)..." -NoNewline
    $r = Invoke-Api -Method "POST" -Endpoint "/api/employees/requests/me" -Token $token -Body @{
        requestType = 0
        data = @{ startDate = "2026-04-20"; duration = 3; reason = "Annual vacation" }
    }
    if ($r.isSuccess) {
        Write-Host " RequestID=$($r.data.id)" -ForegroundColor Green
        Write-Host "    Status=$($r.data.status) StepOrder=$($r.data.currentStepOrder)"
    } else {
        Write-Host " $($r.error.code): $($r.error.message)" -ForegroundColor Red
    }

    # Also preview the chain
    Write-Host "  Preview for Charlie (API Team, HL-3)..." -NoNewline
    $r = Invoke-Api -Method "POST" -Endpoint "/api/requestdefinitions/preview" -Token $token -Body @{
        steps = @(@{ stepType = 2; startFromLevel = 1; levelsUp = 3; sortOrder = 1 })
        requesterEmployeeId = $charlie.id
    }
    if ($r.isSuccess) {
        Write-Host " $($r.data.Count) steps" -ForegroundColor Green
        foreach ($s in $r.data) {
            Write-Host "    Step $($s.sortOrder): $($s.nodeName) ($($s.stepType)) approvers: $($s.approvers.Count)" -ForegroundColor Gray
            foreach ($a in $s.approvers) { Write-Host "      - $($a.employeeName)" -ForegroundColor DarkGray }
        }
    } else {
        Write-Host " $($r.error.message)" -ForegroundColor Red
    }
}

# Diana QA (Web Team) submits Permission
$diana = $created | Where-Object { $_.fullName -eq "Diana QA" } | Select-Object -First 1
if ($diana) {
    Write-Host "  Diana (Web Team) → Permission (Type 1, DirectEmployee)..." -NoNewline
    $r = Invoke-Api -Method "POST" -Endpoint "/api/employees/requests/me" -Token $token -Body @{
        requestType = 1
        data = @{ reason = "Work from home" }
    }
    if ($r.isSuccess) {
        Write-Host " RequestID=$($r.data.id)" -ForegroundColor Green
    } else {
        Write-Host " $($r.error.code)" -ForegroundColor Red
    }
}

# Evan (Recruitment) submits Asset
$evan = $created | Where-Object { $_.fullName -eq "Evan Recruit" } | Select-Object -First 1
if ($evan) {
    Write-Host "  Evan (Recruitment) → Asset (Type 7, HierarchyLevel 2)..." -NoNewline
    $r = Invoke-Api -Method "POST" -Endpoint "/api/employees/requests/me" -Token $token -Body @{
        requestType = 7
        data = @{ assetType = "Laptop"; reason = "New laptop" }
    }
    if ($r.isSuccess) {
        Write-Host " RequestID=$($r.data.id)" -ForegroundColor Green
    } else {
        Write-Host " $($r.error.code)" -ForegroundColor Red
    }
}

# ── Final Summary ──────────────────────────────────────────────────────────────
Write-Host "`n`n=======================================================================" -ForegroundColor Magenta
Write-Host "  DEMO COMPLETE" -ForegroundColor Magenta
Write-Host "=======================================================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "  COMPANY: $CompanyName ($companyId)" -ForegroundColor White
Write-Host ""
Write-Host "  EMPLOYEE CREDENTIALS:" -ForegroundColor Yellow
Write-Host "  ---------------------------------------------------" -ForegroundColor Gray
foreach ($emp in $created) {
    $tag = if ($emp.isManager) { "[MGR]" } else { "[MEM]" }
    Write-Host "  $($emp.email) / $($emp.tempPassword)  $tag $($emp.fullName) @ $($emp.nodeName)" -ForegroundColor White
}
Write-Host ""
Write-Host "  HIERARCHY:" -ForegroundColor Yellow
Write-Host "    Engineering Division" -ForegroundColor White
Write-Host "      Backend Dept" -ForegroundColor Gray
Write-Host "        API Team  (Tom=Manager, Charlie=Member)" -ForegroundColor Gray
Write-Host "      Frontend Dept" -ForegroundColor Gray
Write-Host "        Web Team (Alice=Manager, Diana=Member)" -ForegroundColor Gray
Write-Host "    HR Division" -ForegroundColor White
Write-Host "      Talent Dept" -ForegroundColor Gray
Write-Host "        Recruitment (Grace=Manager, Evan=Member)" -ForegroundColor Gray
Write-Host "    Finance Division" -ForegroundColor White
Write-Host "      Accounting Dept" -ForegroundColor Gray
Write-Host "        Payables (Bob=Manager, Fiona=Member)" -ForegroundColor Gray
Write-Host ""
Write-Host "  REQUEST DEFINITIONS:" -ForegroundColor Yellow
foreach ($def in $definitions) {
    Write-Host "    Type $($def.type) $($def.name.PadRight(15)) $($def.desc)" -ForegroundColor White
}
Write-Host ""
