#!/bin/bash
# =============================================================================
# Setup Test Company Script
# =============================================================================
# Creates a complete test company with:
#   - Deep OrgNode hierarchy (Division -> Department -> Team)
#   - Employees assigned to the company
#   - Employee assignments to org nodes
#   - Request definitions for all 11 request types
#
# Usage:
#   ./Setup-TestCompany.sh [BASE_URL] [SUPER_ADMIN_TOKEN] [COMPANY_ID]
#
# Examples:
#   ./Setup-TestCompany.sh                                    # Uses defaults
#   ./Setup-TestCompany.sh http://localhost:5000 "eyJ..."    # With token
#   ./Setup-TestCompany.sh http://localhost:5000 "eyJ..." "company-id-here"
# =============================================================================

set -e

# Defaults
BASE_URL="${1:-http://localhost:5119}"
SUPER_ADMIN_TOKEN="${2:-}"
SUPER_ADMIN_EMAIL="superadmin@hrms.com"
SUPER_ADMIN_PASSWORD="SuperAdmin@123"
COMPANY_ID="${3:-}"
COMPANY_NAME="Test Company"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# API Helper
api() {
    local method="$1"
    local endpoint="$2"
    local body="$3"
    local token="${4:-$SUPER_ADMIN_TOKEN}"

    local auth_header=""
    if [ -n "$token" ]; then
        auth_header="-H \"Authorization: Bearer $token\""
    fi

    local cmd="curl -s -X $method \"${BASE_URL}${endpoint}\" -H \"Content-Type: application/json\" $auth_header"
    if [ -n "$body" ]; then
        cmd="$cmd -d '$body'"
    fi

    eval "$cmd"
}

# Login if token not provided
if [ -z "$SUPER_ADMIN_TOKEN" ]; then
    echo -e "${CYAN}Logging in as SuperAdmin...${NC}"
    LOGIN_RESULT=$(api "POST" "/api/auth/login" "{\"email\":\"$SUPER_ADMIN_EMAIL\",\"password\":\"$SUPER_ADMIN_PASSWORD\"}" "")

    if echo "$LOGIN_RESULT" | grep -q '"isSuccess":true'; then
        SUPER_ADMIN_TOKEN=$(echo "$LOGIN_RESULT" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
        echo -e "${GREEN}Logged in successfully${NC}"
    else
        echo -e "${RED}Login failed: $LOGIN_RESULT${NC}"
        exit 1
    fi
fi

# Get or validate company
if [ -z "$COMPANY_ID" ]; then
    echo -e "${CYAN}Checking for existing company '$COMPANY_NAME'...${NC}"

    COMPANIES_RESULT=$(api "GET" "/api/companies" "" "$SUPER_ADMIN_TOKEN")
    COMPANY_ID=$(echo "$COMPANIES_RESULT" | grep -o "\"id\":\"[^\"]*\",\"companyName\":\"$COMPANY_NAME\"" | head -1 | grep -o '"id":"[^"]*"' | cut -d'"' -f4)

    if [ -z "$COMPANY_ID" ]; then
        echo -e "${YELLOW}Company '$COMPANY_NAME' not found. Please create it first or provide -CompanyId${NC}"
        exit 1
    fi

    echo -e "${GREEN}Using existing company: $COMPANY_NAME ($COMPANY_ID)${NC}"
else
    echo -e "${CYAN}Using provided company ID: $COMPANY_ID${NC}"
fi

# Get company info
COMPANY_RESULT=$(api "GET" "/api/companies/$COMPANY_ID" "" "$SUPER_ADMIN_TOKEN")
echo -e "${GREEN}Company: $(echo "$COMPANY_RESULT" | grep -o '"companyName":"[^"]*"' | cut -d'"' -f4)${NC}"

# =============================================================================
# STEP 1: Create Hierarchy
# =============================================================================
echo -e "\n${CYAN}=== Creating OrgNode Hierarchy ===${NC}"

HIERARCHY_JSON='{
    "request": {
        "companyId": "'$COMPANY_ID'",
        "nodes": [
            {"tempId": "exec", "name": "Executive", "type": "Division", "parentTempId": null},
            {"tempId": "ceo-office", "name": "CEO Office", "type": "Department", "parentTempId": "exec"},
            {"tempId": "strategy", "name": "Strategy", "type": "Department", "parentTempId": "exec"},
            {"tempId": "fin", "name": "Finance", "type": "Division", "parentTempId": null},
            {"tempId": "accounting", "name": "Accounting", "type": "Department", "parentTempId": "fin"},
            {"tempId": "payables", "name": "Payables", "type": "Team", "parentTempId": "accounting"},
            {"tempId": "receivables", "name": "Receivables", "type": "Team", "parentTempId": "accounting"},
            {"tempId": "treasury", "name": "Treasury", "type": "Department", "parentTempId": "fin"},
            {"tempId": "hr", "name": "Human Resources", "type": "Division", "parentTempId": null},
            {"tempId": "talent-acq", "name": "Talent Acquisition", "type": "Department", "parentTempId": "hr"},
            {"tempId": "campus-recruit", "name": "Campus Recruitment", "type": "Team", "parentTempId": "talent-acq"},
            {"tempId": "exec-search", "name": "Executive Search", "type": "Team", "parentTempId": "talent-acq"},
            {"tempId": "emp-relations", "name": "Employee Relations", "type": "Department", "parentTempId": "hr"},
            {"tempId": "training-dev", "name": "Training & Development", "type": "Department", "parentTempId": "hr"},
            {"tempId": "eng", "name": "Engineering", "type": "Division", "parentTempId": null},
            {"tempId": "frontend", "name": "Frontend", "type": "Department", "parentTempId": "eng"},
            {"tempId": "web", "name": "Web", "type": "Team", "parentTempId": "frontend"},
            {"tempId": "mobile", "name": "Mobile", "type": "Team", "parentTempId": "frontend"},
            {"tempId": "backend", "name": "Backend", "type": "Department", "parentTempId": "eng"},
            {"tempId": "api", "name": "API", "type": "Team", "parentTempId": "backend"},
            {"tempId": "infra", "name": "Infrastructure", "type": "Team", "parentTempId": "backend"},
            {"tempId": "qa", "name": "QA", "type": "Department", "parentTempId": "eng"}
        ]
    }
}'

HIERARCHY_RESULT=$(api "POST" "/api/orgnodes/bulk-setup" "$HIERARCHY_JSON" "$SUPER_ADMIN_TOKEN")

if echo "$HIERARCHY_RESULT" | grep -q '"isSuccess":true'; then
    NODE_COUNT=$(echo "$HIERARCHY_RESULT" | grep -o '"nodes":\[[^]]*\]' | grep -o '"tempId":"[^"]*"' | wc -l)
    echo -e "${GREEN}Created $NODE_COUNT nodes${NC}"

    # Extract node IDs into associative array
    declare -A NODE_MAP
    while IFS=: read -r tempId nodeId; do
        tempId=$(echo "$tempId" | grep -o '"tempId":"[^"]*"' | cut -d'"' -f4)
        nodeId=$(echo "$nodeId" | grep -o '"realId":"[^"]*"' | cut -d'"' -f4)
        NODE_MAP[$tempId]=$nodeId
    done < <(echo "$HIERARCHY_RESULT" | grep -o '"tempId":"[^"]*","realId":"[^"]*"' | tr ',' ':')
else
    echo -e "${RED}Failed to create hierarchy: $HIERARCHY_RESULT${NC}"
    exit 1
fi

# =============================================================================
# STEP 2: Create Employees
# =============================================================================
echo -e "\n${CYAN}=== Creating Employees ===${NC}"

EMPLOYEES='[
    {"fullName": "John CEO", "email": "ceo@testcompany.com", "phoneNumber": "01111111111", "role": 2},
    {"fullName": "Jane CFO", "email": "cfo@testcompany.com", "phoneNumber": "02222222222", "role": 2},
    {"fullName": "Bob HR Director", "email": "hr-dir@testcompany.com", "phoneNumber": "03333333333", "role": 3},
    {"fullName": "Alice CTO", "email": "cto@testcompany.com", "phoneNumber": "04444444444", "role": 2},
    {"fullName": "Charlie Developer", "email": "dev1@testcompany.com", "phoneNumber": "05555555555", "role": 4},
    {"fullName": "Diana Developer", "email": "dev2@testcompany.com", "phoneNumber": "06666666666", "role": 4},
    {"fullName": "Eve QA", "email": "qa1@testcompany.com", "phoneNumber": "07777777777", "role": 4},
    {"fullName": "Frank Finance", "email": "fin1@testcompany.com", "phoneNumber": "08888888888", "role": 4},
    {"fullName": "Grace HR", "email": "hr1@testcompany.com", "phoneNumber": "09999999999", "role": 4}
]'

declare -A EMPLOYEE_MAP
CREATED_EMAILS=""

for emp in $(echo "$EMPLOYEES" | jq -c '.[]'); do
    FULL_NAME=$(echo "$emp" | jq -r '.fullName')
    EMAIL=$(echo "$emp" | jq -r '.email')

    echo -n "  Creating $FULL_NAME... "

    RESULT=$(api "POST" "/api/employees" "{\"fullName\":\"$FULL_NAME\",\"email\":\"$EMAIL\",\"phoneNumber\":$(echo $emp | jq -r '.phoneNumber'),\"companyId\":\"$COMPANY_ID\",\"role\":$(echo $emp | jq -r '.role')}" "$SUPER_ADMIN_TOKEN")

    if echo "$RESULT" | grep -q '"isSuccess":true'; then
        EMP_ID=$(echo "$RESULT" | grep -o '"employeeId":"[^"]*"' | cut -d'"' -f4)
        TEMP_PWD=$(echo "$RESULT" | grep -o '"temporaryPassword":"[^"]*"' | cut -d'"' -f4)
        EMPLOYEE_MAP[$EMAIL]=$EMP_ID
        echo -e "${GREEN}$EMP_ID${NC}"
        echo "    Password: $TEMP_PWD"
        CREATED_EMAILS="$CREATED_EMAILS\n  $EMAIL - $TEMP_PWD"
    else
        echo -e "${RED}FAILED${NC}"
    fi
done

echo -e "${GREEN}Employees created${NC}"

# =============================================================================
# STEP 3: Assign Employees to Nodes
# =============================================================================
echo -e "\n${CYAN}=== Assigning Employees to Nodes ===${NC}"

ASSIGNMENTS='[
    {"nodeTempId": "exec", "empEmail": "ceo@testcompany.com", "role": 0},
    {"nodeTempId": "fin", "empEmail": "cfo@testcompany.com", "role": 0},
    {"nodeTempId": "hr", "empEmail": "hr-dir@testcompany.com", "role": 0},
    {"nodeTempId": "eng", "empEmail": "cto@testcompany.com", "role": 0},
    {"nodeTempId": "accounting", "empEmail": "fin1@testcompany.com", "role": 0},
    {"nodeTempId": "payables", "empEmail": "fin1@testcompany.com", "role": 1},
    {"nodeTempId": "talent-acq", "empEmail": "hr1@testcompany.com", "role": 0},
    {"nodeTempId": "campus-recruit", "empEmail": "hr1@testcompany.com", "role": 1},
    {"nodeTempId": "web", "empEmail": "dev1@testcompany.com", "role": 0},
    {"nodeTempId": "qa", "empEmail": "qa1@testcompany.com", "role": 0}
]'

ROLE_NAMES=("Manager" "Member")

for assign in $(echo "$ASSIGNMENTS" | jq -c '.[]'); do
    NODE_TEMP_ID=$(echo "$assign" | jq -r '.nodeTempId')
    EMP_EMAIL=$(echo "$assign" | jq -r '.empEmail')
    ROLE=$(echo "$assign" | jq -r '.role')

    NODE_ID=${NODE_MAP[$NODE_TEMP_ID]}
    EMP_ID=${EMPLOYEE_MAP[$EMP_EMAIL]}

    if [ -z "$EMP_ID" ] || [ -z "$NODE_ID" ]; then
        echo -e "  ${YELLOW}Skipping $EMP_EMAIL -> $NODE_TEMP_ID (not found)${NC}"
        continue
    fi

    echo -n "  Assigning $EMP_EMAIL to $NODE_TEMP_ID as ${ROLE_NAMES[$ROLE]}... "

    RESULT=$(api "POST" "/api/orgnodes/$NODE_ID/assignments" "{\"employeeId\":\"$EMP_ID\",\"role\":$ROLE}" "$SUPER_ADMIN_TOKEN")

    if echo "$RESULT" | grep -q '"isSuccess":true'; then
        echo -e "${GREEN}OK${NC}"
    else
        echo -e "${RED}FAILED${NC}"
    fi
done

# =============================================================================
# STEP 4: Create Request Definitions
# =============================================================================
echo -e "\n${CYAN}=== Creating Request Definitions ===${NC}"

REQUEST_DEFS='[
    {"type": 0, "name": "Leave", "steps": [{"nodeTempId": "hr", "sortOrder": 0}]},
    {"type": 1, "name": "Permission", "steps": [{"nodeTempId": "hr", "sortOrder": 0}]},
    {"type": 2, "name": "SalarySlip", "steps": [{"nodeTempId": "fin", "sortOrder": 0}]},
    {"type": 3, "name": "HRLetter", "steps": [{"nodeTempId": "hr", "sortOrder": 0}]},
    {"type": 4, "name": "Resignation", "steps": [{"nodeTempId": "exec", "sortOrder": 0}]},
    {"type": 5, "name": "EndOfService", "steps": [{"nodeTempId": "exec", "sortOrder": 0}]},
    {"type": 6, "name": "PurchaseOrder", "steps": [
        {"nodeTempId": "accounting", "sortOrder": 0},
        {"nodeTempId": "fin", "sortOrder": 1}
    ]},
    {"type": 7, "name": "Asset", "steps": [{"nodeTempId": "fin", "sortOrder": 0}]},
    {"type": 8, "name": "Loan", "steps": [
        {"nodeTempId": "hr", "sortOrder": 0},
        {"nodeTempId": "exec", "sortOrder": 1}
    ]},
    {"type": 9, "name": "Assignment", "steps": [{"nodeTempId": "eng", "sortOrder": 0}]},
    {"type": 10, "name": "Other", "steps": [{"nodeTempId": "hr", "sortOrder": 0}]}
]'

for def in $(echo "$REQUEST_DEFS" | jq -c '.[]'); do
    DEF_NAME=$(echo "$def" | jq -r '.name')
    DEF_TYPE=$(echo "$def" | jq -r '.type')

    echo -n "  Creating $DEF_NAME request definition... "

    # Build steps JSON
    STEPS_JSON=$(echo "$def" | jq '.steps | [.[] | {"orgNodeId": .nodeTempId as $t | "'$COMPANY_ID'" + .nodeTempId, "sortOrder"}]')
    # Fix: use actual node IDs from NODE_MAP
    STEPS='[]'
    for step in $(echo "$def" | jq -c '.steps[]'); do
        NODE_TEMP_ID=$(echo "$step" | jq -r '.nodeTempId')
        SORT_ORDER=$(echo "$step" | jq -r '.sortOrder')
        NODE_ID=${NODE_MAP[$NODE_TEMP_ID]}
        STEPS=$(echo "$STEPS" | jq ". + [{\"orgNodeId\": \"$NODE_ID\", \"sortOrder\": $SORT_ORDER}]")
    done

    RESULT=$(api "POST" "/api/requestdefinitions" "{\"companyId\":\"$COMPANY_ID\",\"requestType\":$DEF_TYPE,\"steps\":$STEPS}" "$SUPER_ADMIN_TOKEN")

    if echo "$RESULT" | grep -q '"isSuccess":true'; then
        DEF_ID=$(echo "$RESULT" | grep -o '"data":"[^"]*"' | cut -d'"' -f4)
        echo -e "${GREEN}$DEF_ID${NC}"
    else
        echo -e "${RED}FAILED${NC}"
    fi
done

# =============================================================================
# Summary
# =============================================================================
echo -e "\n${GREEN}=== Setup Complete ===${NC}"
echo "Company ID: $COMPANY_ID"
echo ""
echo -e "${YELLOW}Employee Credentials:${NC}$CREATED_EMAILS"
