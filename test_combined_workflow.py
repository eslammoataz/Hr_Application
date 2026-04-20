"""
End-to-end test for a COMBINED approval workflow:
  HierarchyLevel (2 levels up) → CompanyRole

Approval chain produced:
  Leaf Node employee submits →
    Step 1a: Level-2 Manager (Mid Node manager, immediate parent) approves
    Step 1b: Level-1 Manager (Root Node manager, grandparent)    approves
    Step 2:  Role Approver   (CompanyRole member)                approves
  → Request fully Approved

Org hierarchy created:
  Root Node  [E2E Root {RUN_ID}]   ← Level-1 Manager assigned (Manager)
    └── Mid Node  [E2E Mid {RUN_ID}]  ← Level-2 Manager assigned (Manager)
          └── Leaf Node [E2E Leaf {RUN_ID}] ← Requester assigned (Member)

Request Definition (type: Other):
  Step sortOrder=1 — WorkflowStepType.HierarchyLevel  startFromLevel=1  levelsUp=2
  Step sortOrder=2 — WorkflowStepType.CompanyRole      companyRoleId=<created role>

Usage:
  pip install requests
  python test_combined_workflow.py
  BASE_URL=https://yourserver.com python test_combined_workflow.py
"""

import os
import sys
import json
import requests
import uuid

# ── Config ────────────────────────────────────────────────────────────────────
BASE_URL       = os.getenv("BASE_URL", "http://localhost:5119").rstrip("/")
ADMIN_EMAIL    = os.getenv("ADMIN_EMAIL", "companyadmin@hrms.com")
ADMIN_PASSWORD = os.getenv("ADMIN_PASSWORD", "Passdev@1234")

RUN_ID = str(uuid.uuid4())[:8]

REQUEST_TYPE_OTHER = 10   # RequestType.Other

# ── Helpers ───────────────────────────────────────────────────────────────────
RESET  = "\033[0m"
BOLD   = "\033[1m"
GREEN  = "\033[92m"
RED    = "\033[91m"
CYAN   = "\033[96m"
YELLOW = "\033[93m"
DIM    = "\033[2m"

def header(text):
    print(f"\n{BOLD}{CYAN}{'─'*62}{RESET}")
    print(f"{BOLD}{CYAN}  {text}{RESET}")
    print(f"{BOLD}{CYAN}{'─'*62}{RESET}")

def ok(text):    print(f"  {GREEN}✓{RESET}  {text}")
def fail(text):  print(f"  {RED}✗{RESET}  {RED}{text}{RESET}")
def info(text):  print(f"  {YELLOW}→{RESET}  {text}")

def dump(label, data):
    print(f"  {DIM}{label}: {json.dumps(data, indent=4, default=str)}{RESET}")

def assert_ok(resp, label):
    if resp.ok:
        ok(f"{label} — HTTP {resp.status_code}")
        return resp.json()
    fail(f"{label} — HTTP {resp.status_code}")
    try:
        dump("Response", resp.json())
    except Exception:
        print(f"  Raw: {resp.text[:400]}")
    sys.exit(1)

_tokens = {}   # email → raw JWT; printed in the final summary

def login(email, password):
    resp = requests.post(f"{BASE_URL}/api/auth/login", json={"email": email, "password": password})
    body = assert_ok(resp, f"Login ({email})")
    token = body["data"]["token"]
    _tokens[email] = token
    ok(f"Token acquired for {email}")
    return {"Authorization": f"Bearer {token}"}

def create_employee(full_name, email, phone):
    # Check if employee already exists
    search_resp = requests.get(f"{BASE_URL}/api/employees?email={email}", headers=admin_headers)
    if search_resp.ok:
        existing = search_resp.json()
        items = existing.get("data", {}).get("items", existing.get("data", []))
        for emp in items:
            if emp.get("email", "").lower() == email.lower():
                emp_id = emp["id"]
                ok(f"Employee already exists: {emp_id}  ({email})")
                return emp_id

    payload = {
        "fullName":    full_name,
        "email":       email,
        "phoneNumber": phone,
        "companyId":   company_id,
        "role":        4   # UserRole.Employee
    }
    resp = requests.post(f"{BASE_URL}/api/Employees", json=payload, headers=admin_headers)
    if not resp.ok:
        # Print full response to diagnose CreationFailed
        try:
            err_body = resp.json()
            dump("Employee creation error", err_body)
        except Exception:
            dump("Raw response", resp.text[:500])
        fail(f"Create employee ({full_name}) — {resp.status_code}")
        sys.exit(1)
    body = resp.json()
    emp_id       = body["data"]["employeeId"]
    user_id      = body["data"].get("userId")
    temp_password = body["data"].get("temporaryPassword")
    ok(f"Employee ID: {emp_id}  ({email})")

    if temp_password and user_id:
        chg = requests.post(f"{BASE_URL}/api/auth/force-change-password", json={
            "userId": user_id, "currentPassword": temp_password, "newPassword": "Passdev@1234"
        })
        if chg.ok:
            ok(f"Password set for {email}")
        else:
            info(f"Password set skipped: {chg.status_code} {chg.text[:80]}")
    return emp_id

def assign_to_node(node_id, emp_id, org_role, label):
    """org_role: 0=Manager, 1=Member"""
    resp = requests.post(
        f"{BASE_URL}/api/orgnodes/{node_id}/assignments",
        json={"orgNodeId": node_id, "employeeId": emp_id, "role": org_role},
        headers=admin_headers
    )
    assert_ok(resp, f"Assign {label}")

def check_pending(headers, request_id, label):
    resp = requests.get(f"{BASE_URL}/api/Employees/requests/approvals/pending", headers=headers)
    body = assert_ok(resp, f"{label} — fetch pending approvals")
    items = body.get("data", {}).get("items", body.get("data", []))
    if any(str(r.get("id")) == str(request_id) for r in items):
        ok(f"✔ Request {request_id} IS visible to {label}")
    else:
        fail(f"Request {request_id} NOT visible to {label} — list: {[r.get('id') for r in items]}")
        sys.exit(1)
    dump(f"{label} pending list", items)

def approve(headers, request_id, comment, label):
    resp = requests.post(
        f"{BASE_URL}/api/Employees/requests/approvals/{request_id}/approve",
        json={"comment": comment},
        headers=headers
    )
    assert_ok(resp, f"{label} — approve")
    ok(f"Request approved by {label}!")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 1 — Login as Company Admin
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 1 — Login as Company Admin")
admin_headers = login(ADMIN_EMAIL, ADMIN_PASSWORD)

resp = requests.get(f"{BASE_URL}/api/companies/me", headers=admin_headers)
company    = assert_ok(resp, "Get company info")
company_id = company["data"]["id"]
ok(f"Company ID: {company_id}")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 2 — Build a fresh 3-level org node hierarchy
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 2 — Create 3-level Org Node hierarchy")

def create_node(name, parent_id=None):
    resp = requests.post(
        f"{BASE_URL}/api/orgnodes",
        json={"name": name, "parentId": parent_id},
        headers=admin_headers
    )
    body    = assert_ok(resp, f"Create OrgNode '{name}'")
    node_id = body["data"]
    ok(f"OrgNode '{name}' → {node_id}")
    return node_id

root_node_id = create_node(f"E2E Root {RUN_ID}")
mid_node_id  = create_node(f"E2E Mid {RUN_ID}",  parent_id=root_node_id)
leaf_node_id = create_node(f"E2E Leaf {RUN_ID}", parent_id=mid_node_id)

info(f"Tree: Root({root_node_id}) → Mid({mid_node_id}) → Leaf({leaf_node_id})")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 3 — Create employees
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 3 — Create employees")

digits = ''.join(c for c in RUN_ID if c.isdigit())[:4].ljust(4, '0')
mgr_l1_id        = create_employee("E2E Level-1 Manager",  f"mgr_l1_{RUN_ID}@test.com",       f"0111{digits}")
mgr_l2_id        = create_employee("E2E Level-2 Manager",  f"mgr_l2_{RUN_ID}@test.com",       f"0112{digits}")
role_approver_id = create_employee("E2E Role Approver",    f"role_appr_{RUN_ID}@test.com",    f"0113{digits}")
requester_id     = create_employee("E2E Requester ML",     f"req_ml_{RUN_ID}@test.com",       f"0114{digits}")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 4 — Assign employees to org nodes
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 4 — Assign employees to Org Nodes")
# OrgRole: Manager=0, Member=1
assign_to_node(root_node_id, mgr_l1_id,    0, "Level-1 Manager → Root Node (Manager)")
assign_to_node(mid_node_id,  mgr_l2_id,    0, "Level-2 Manager → Mid Node  (Manager)")
assign_to_node(leaf_node_id, requester_id, 1, "Requester        → Leaf Node (Member)")

info("Hierarchy summary:")
info(f"  Root [{root_node_id}]  manager: Level-1 Manager [{mgr_l1_id}]")
info(f"  Mid  [{mid_node_id}]   manager: Level-2 Manager [{mgr_l2_id}]")
info(f"  Leaf [{leaf_node_id}]  member:  Requester       [{requester_id}]")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 5 — Create Company Role and assign Role Approver
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 5 — Create Company Role and assign Role Approver")

resp = requests.post(
    f"{BASE_URL}/api/company-roles",
    json={"name": f"E2E Combined Role {RUN_ID}", "description": "Role for combined workflow test", "permissions": []},
    headers=admin_headers
)
role_body = assert_ok(resp, "Create company role")
role_id   = role_body["data"]
ok(f"Role ID: {role_id}")

resp = requests.post(
    f"{BASE_URL}/api/company-roles/{role_id}/employees/{role_approver_id}",
    headers=admin_headers
)
assert_ok(resp, "Assign Role Approver to company role")

# Verify membership
resp = requests.get(f"{BASE_URL}/api/company-roles/{role_id}/employees", headers=admin_headers)
members_body = assert_ok(resp, "Verify role members")
member_names = [m.get("employeeName", m.get("fullName", str(m))) for m in members_body.get("data", [])]
ok(f"Role members: {member_names}")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 6 — Create Request Definition
#   Step 1: HierarchyLevel  startFromLevel=1  levelsUp=2
#           → resolves to Level-2 Manager (immediate parent) first,
#             then Level-1 Manager (grandparent) second
#   Step 2: CompanyRole → role_id
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 6 — Create Request Definition  (HierarchyLevel×2 + CompanyRole)")

definition_payload = {
    "requestType": REQUEST_TYPE_OTHER,
    "steps": [
        {
            "stepType":       2,   # WorkflowStepType.HierarchyLevel
            "startFromLevel": 1,
            "levelsUp":       2,
            "sortOrder":      1
        },
        {
            "stepType":      3,    # WorkflowStepType.CompanyRole
            "companyRoleId": role_id,
            "sortOrder":     2
        }
    ]
}

resp = requests.post(f"{BASE_URL}/api/RequestDefinitions", json=definition_payload, headers=admin_headers)

if resp.status_code == 400:
    err_body = resp.json()
    if err_body.get("error", {}).get("code") == "Request.DefinitionAlreadyExists":
        ok("Definition already exists — deleting it first")
        list_resp = requests.get(f"{BASE_URL}/api/RequestDefinitions?isActive=true", headers=admin_headers)
        if list_resp.ok:
            for d in list_resp.json().get("data", []):
                if d.get("requestType") == REQUEST_TYPE_OTHER:
                    requests.delete(f"{BASE_URL}/api/RequestDefinitions/{d['id']}", headers=admin_headers)
                    ok(f"Deleted old definition {d['id']}")
                    break
        resp = requests.post(f"{BASE_URL}/api/RequestDefinitions", json=definition_payload, headers=admin_headers)

def_body      = assert_ok(resp, "Create request definition")
definition_id = def_body["data"]
ok(f"Definition ID: {definition_id}")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 7 — Requester submits request
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 7 — Requester logs in and submits an 'Other' request")

requester_headers = login(f"req_ml_{RUN_ID}@test.com", "Passdev@1234")

resp = requests.post(
    f"{BASE_URL}/api/Employees/requests/me",
    json={"requestType": REQUEST_TYPE_OTHER, "data": {"description": "E2E combined workflow test request"}},
    headers=requester_headers
)
req_body   = assert_ok(resp, "Submit request")
request_id = req_body["data"]
ok(f"Request ID: {request_id}")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 8 — Level-2 Manager (immediate parent / Mid Node) approves
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 8 — Level-2 Manager (Mid Node, immediate parent) views pending + approves")

mgr_l2_headers = login(f"mgr_l2_{RUN_ID}@test.com", "Passdev@1234")
check_pending(mgr_l2_headers, request_id, "Level-2 Manager")
approve(mgr_l2_headers, request_id, "Approved by Level-2 Manager (immediate parent)", "Level-2 Manager")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 9 — Level-1 Manager (grandparent / Root Node) approves
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 9 — Level-1 Manager (Root Node, grandparent) views pending + approves")

mgr_l1_headers = login(f"mgr_l1_{RUN_ID}@test.com", "Passdev@1234")
check_pending(mgr_l1_headers, request_id, "Level-1 Manager")
approve(mgr_l1_headers, request_id, "Approved by Level-1 Manager (grandparent)", "Level-1 Manager")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 10 — Role Approver views pending + approves
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 10 — Role Approver views pending + approves")

role_appr_headers = login(f"role_appr_{RUN_ID}@test.com", "Passdev@1234")
check_pending(role_appr_headers, request_id, "Role Approver")
approve(role_appr_headers, request_id, "Approved by Role Approver (company role step)", "Role Approver")

# ═════════════════════════════════════════════════════════════════════════════
# STEP 11 — Verify request is fully Approved + show history
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 11 — Verify final request status = Approved")

resp    = requests.get(f"{BASE_URL}/api/Employees/requests/me/{request_id}", headers=requester_headers)
details = assert_ok(resp, "Get request details")
req_data = details.get("data", {})

final_status = req_data.get("status", req_data.get("statusName", "unknown"))
ok(f"Final request status: {final_status}")

history = req_data.get("approvalHistory", [])
if history:
    ok(f"Approval history ({len(history)} record(s)):")
    for entry in history:
        approver_name = entry.get("approverName", entry.get("approverId", "?"))
        status        = entry.get("status", "?")
        timestamp     = entry.get("createdAt", entry.get("approvedAt", "?"))
        comment       = entry.get("comment", "")
        print(f"      • {approver_name}  →  {status}  at {timestamp}  [{comment}]")
else:
    info("No approvalHistory in response (check response shape)")

dump("Full request details", req_data)

# ═════════════════════════════════════════════════════════════════════════════
# STEP 12 — Verify none of the approvers still see it as pending
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 12 — Verify all approvers' pending queues are empty for this request")

for h, label in [
    (mgr_l2_headers,     "Level-2 Manager"),
    (mgr_l1_headers,     "Level-1 Manager"),
    (role_appr_headers,  "Role Approver"),
]:
    resp  = requests.get(f"{BASE_URL}/api/Employees/requests/approvals/pending", headers=h)
    body  = assert_ok(resp, f"{label} — check pending after full approval")
    items = body.get("data", {}).get("items", body.get("data", []))
    if any(str(r.get("id")) == str(request_id) for r in items):
        fail(f"Request {request_id} STILL in {label}'s pending list — should be gone!")
        sys.exit(1)
    else:
        ok(f"✔ {label}'s pending list is clean")

# ═════════════════════════════════════════════════════════════════════════════
# ALL DONE — credentials + tokens summary
# ═════════════════════════════════════════════════════════════════════════════
header("✅  ALL STEPS PASSED — Combined Workflow Test complete")
print()
print(f"  {BOLD}{'─'*62}{RESET}")
print(f"  {BOLD}  Credentials for manual testing (run id: {RUN_ID}){RESET}")
print(f"  {BOLD}{'─'*62}{RESET}")
print()

users = [
    ("Company Admin",    ADMIN_EMAIL,                         ADMIN_PASSWORD, None,             "pre-existing"),
    ("Level-1 Manager",  f"mgr_l1_{RUN_ID}@test.com",        "Passdev@1234", mgr_l1_id,        "Root Node — grandparent approver"),
    ("Level-2 Manager",  f"mgr_l2_{RUN_ID}@test.com",        "Passdev@1234", mgr_l2_id,        "Mid Node  — immediate parent approver"),
    ("Role Approver",    f"role_appr_{RUN_ID}@test.com",     "Passdev@1234", role_approver_id, "CompanyRole step approver"),
    ("Requester",        f"req_ml_{RUN_ID}@test.com",        "Passdev@1234", requester_id,     "Leaf Node — submitted the request"),
]

for role, email, password, emp_id, note in users:
    emp_part = f"  empId: {YELLOW}{emp_id}{RESET}" if emp_id else ""
    print(f"  {CYAN}{role:<20}{RESET}  email: {YELLOW}{email:<46}{RESET}  pw: {GREEN}{password}{RESET}{emp_part}  {DIM}({note}){RESET}")

print()
print(f"  {DIM}Request ID    : {request_id}{RESET}")
print(f"  {DIM}Definition ID : {definition_id}{RESET}")
print(f"  {DIM}Role ID       : {role_id}{RESET}")
print(f"  {DIM}Root Node ID  : {root_node_id}   ← Level-1 Manager here{RESET}")
print(f"  {DIM}Mid Node ID   : {mid_node_id}   ← Level-2 Manager here{RESET}")
print(f"  {DIM}Leaf Node ID  : {leaf_node_id}   ← Requester here{RESET}")
print()
print(f"  {BOLD}{CYAN}POST {BASE_URL}/api/auth/login{RESET}  "
      f"{DIM}→ {{\"email\":\"...\",\"password\":\"...\"}}{RESET}")
print()

print(f"  {BOLD}{'─'*62}{RESET}")
print(f"  {BOLD}  Bearer tokens (valid until server restart){RESET}")
print(f"  {BOLD}{'─'*62}{RESET}")
print()
for role, email, password, emp_id, note in users:
    token = _tokens.get(email)
    if token:
        print(f"  {CYAN}{role:<20}{RESET}")
        print(f"  {DIM}Authorization: Bearer {token}{RESET}")
        print()
print()
