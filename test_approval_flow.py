"""
End-to-end test for the CompanyRole approval workflow.

What this script does:
  1.  Login as Company Admin
  2.  Create a new Company Role  ("E2E Approver Role")
  3.  Create Approver-1 employee  (emp_approver1@test.com)
  4.  Create Approver-2 employee  (emp_approver2@test.com)
  5.  Assign both to the new role
  6.  Create a Request Definition  (type: Other) with one CompanyRole step
  7.  Create Requester employee    (emp_requester@test.com)
  8.  Assign the Requester to an existing OrgNode (so they can submit requests)
  9.  Login as Requester → submit a request
  10. Login as Approver-1 → view pending approvals  (request must appear)
  11. Login as Approver-2 → view pending approvals  (same request must appear)
  12. Login as Approver-1 → approve the request
  13. View request details → show approval history (who approved + timestamp)
  14. Login as Approver-2 → view pending approvals  (must be EMPTY now)

Usage:
  pip install requests
  python test_approval_flow.py
  # or override the base URL:
  BASE_URL=https://yourserver.com python test_approval_flow.py
"""

import os
import sys
import json
import requests
import uuid
from datetime import datetime

# ── Config ────────────────────────────────────────────────────────────────────
BASE_URL        = os.getenv("BASE_URL", "http://localhost:5119").rstrip("/")
ADMIN_EMAIL     = os.getenv("ADMIN_EMAIL", "companyadmin@hrms.com")
ADMIN_PASSWORD  = os.getenv("ADMIN_PASSWORD", "Passdev@1234")

RUN_ID = str(uuid.uuid4())[:8]  # unique suffix per run

REQUEST_TYPE_OTHER = 10   # RequestType.Other — no extra business-rule validation

# ── Helpers ───────────────────────────────────────────────────────────────────
RESET  = "\033[0m"
BOLD   = "\033[1m"
GREEN  = "\033[92m"
RED    = "\033[91m"
CYAN   = "\033[96m"
YELLOW = "\033[93m"
DIM    = "\033[2m"

def header(text):
    print(f"\n{BOLD}{CYAN}{'─'*60}{RESET}")
    print(f"{BOLD}{CYAN}  {text}{RESET}")
    print(f"{BOLD}{CYAN}{'─'*60}{RESET}")

def ok(text):
    print(f"  {GREEN}✓{RESET}  {text}")

def fail(text):
    print(f"  {RED}✗{RESET}  {RED}{text}{RESET}")

def info(text):
    print(f"  {YELLOW}→{RESET}  {text}")

def dump(label, data):
    print(f"  {DIM}{label}: {json.dumps(data, indent=4, default=str)}{RESET}")

def assert_ok(resp, label):
    if resp.ok:
        ok(f"{label} — HTTP {resp.status_code}")
        return resp.json()
    else:
        fail(f"{label} — HTTP {resp.status_code}")
        try:
            dump("Response", resp.json())
        except Exception:
            print(f"  Raw: {resp.text[:300]}")
        sys.exit(1)

_tokens = {}   # email → raw JWT, populated by login(); printed in the final summary

def login(email, password):
    resp = requests.post(f"{BASE_URL}/api/auth/login", json={"email": email, "password": password})
    body = assert_ok(resp, f"Login ({email})")
    token = body["data"]["token"]
    _tokens[email] = token
    ok(f"Token acquired for {email}")
    return {"Authorization": f"Bearer {token}"}

# ── Step 1 — Login as Company Admin ──────────────────────────────────────────
header("STEP 1 — Login as Company Admin")
admin_headers = login(ADMIN_EMAIL, ADMIN_PASSWORD)

# Get company info (we need the companyId to create employees)
resp = requests.get(f"{BASE_URL}/api/companies/me", headers=admin_headers)
company = assert_ok(resp, "Get company info")
company_id = company["data"]["id"]
ok(f"Company ID: {company_id}")

# ── Step 2 — Create the Company Role ─────────────────────────────────────────
header("STEP 2 — Create Company Role  'E2E Approver Role'")
role_payload = {
    "name": f"E2E Approver Role {RUN_ID}",
    "description": "Created by automated test script",
    "permissions": []
}
resp = requests.post(f"{BASE_URL}/api/company-roles", json=role_payload, headers=admin_headers)
role_body = assert_ok(resp, "Create company role")
role_id = role_body["data"]
ok(f"Role ID: {role_id}")

# ── Step 3 & 4 — Create the two Approver employees ───────────────────────────
header("STEP 3 & 4 — Create Approver-1 and Approver-2")

def create_employee(full_name, email, phone):
    payload = {
        "fullName":    full_name,
        "email":       email,
        "phoneNumber": phone,
        "companyId":   company_id,
        "role":        4   # UserRole.Employee = 4
    }
    resp = requests.post(f"{BASE_URL}/api/Employees", json=payload, headers=admin_headers)
    body = assert_ok(resp, f"Create employee ({full_name})")
    emp_id = body["data"]["employeeId"]
    user_id = body["data"].get("userId")
    temp_password = body["data"].get("temporaryPassword")
    ok(f"Employee ID: {emp_id}  ({email})")

    # Set a known password via force-change-password so we can login
    if temp_password and user_id:
        change_payload = {
            "userId":        user_id,
            "currentPassword": temp_password,
            "newPassword":   "Passdev@1234"
        }
        chg_resp = requests.post(f"{BASE_URL}/api/auth/force-change-password", json=change_payload)
        if chg_resp.ok:
            ok(f"Password set for {email}")
        else:
            info(f"Could not set password (may not need change): {chg_resp.status_code} {chg_resp.text[:100]}")

    return emp_id, "Passdev@1234"

approver1_id, approver1_pwd = create_employee("E2E Approver One", f"emp_approver1_{RUN_ID}@test.com", f"0110000{''.join(c for c in RUN_ID if c.isdigit())[:5]}")
approver2_id, approver2_pwd = create_employee("E2E Approver Two", f"emp_approver2_{RUN_ID}@test.com", f"0110001{''.join(c for c in RUN_ID if c.isdigit())[:5]}")

# ── Step 5 — Assign both employees to the role ───────────────────────────────
header("STEP 5 — Assign Approver-1 and Approver-2 to the role")

for emp_id, label in [(approver1_id, "Approver-1"), (approver2_id, "Approver-2")]:
    resp = requests.post(
        f"{BASE_URL}/api/company-roles/{role_id}/employees/{emp_id}",
        headers=admin_headers
    )
    assert_ok(resp, f"Assign {label} to role")

# Verify: list employees in role
resp = requests.get(f"{BASE_URL}/api/company-roles/{role_id}/employees", headers=admin_headers)
role_members = assert_ok(resp, "Verify role members")
member_names = [m.get("employeeName", m.get("fullName", str(m))) for m in role_members.get("data", [])]
ok(f"Role now has members: {member_names}")

# ── Step 6 — Create Request Definition with CompanyRole step ─────────────────
header("STEP 6 — Create Request Definition  (type: Other, step: CompanyRole)")
definition_payload = {
    "requestType": REQUEST_TYPE_OTHER,
    "steps": [
        {
            "stepType":      3,          # WorkflowStepType.CompanyRole
            "companyRoleId": role_id,
            "sortOrder":     1
        }
    ]
}
resp = requests.post(f"{BASE_URL}/api/RequestDefinitions", json=definition_payload, headers=admin_headers)
if resp.status_code == 400:
    err_body = resp.json()
    if err_body.get("error", {}).get("code") == "Request.DefinitionAlreadyExists":
        ok("Definition already exists — deleting it first")
        # Find and delete the existing definition
        list_resp = requests.get(f"{BASE_URL}/api/RequestDefinitions?isActive=true", headers=admin_headers)
        if list_resp.ok:
            list_body = list_resp.json()
            for d in list_body.get("data", []):
                if d.get("requestType") == REQUEST_TYPE_OTHER:
                    del_resp = requests.delete(f"{BASE_URL}/api/RequestDefinitions/{d['id']}", headers=admin_headers)
                    ok(f"Deleted old definition {d['id']}")
                    break
        # Now try creating again
        resp = requests.post(f"{BASE_URL}/api/RequestDefinitions", json=definition_payload, headers=admin_headers)

def_body = assert_ok(resp, "Create request definition")
definition_id = def_body["data"]
ok(f"Definition ID: {definition_id}")

# ── Step 7 — Create the Requester employee ───────────────────────────────────
header("STEP 7 — Create Requester employee")
requester_id, requester_pwd = create_employee("E2E Requester", f"emp_requester_{RUN_ID}@test.com", f"0110002{''.join(c for c in RUN_ID if c.isdigit())[:5]}")

# ── Step 8 — Assign Requester to an OrgNode ──────────────────────────────────
header("STEP 8 — Assign Requester to an OrgNode")

# Get the company's org node tree to find a usable node
resp = requests.get(f"{BASE_URL}/api/orgnodes/my-company", headers=admin_headers)
tree = assert_ok(resp, "Get org node tree")

# Walk the tree to find any node (prefer 'Backend Team', fall back to first leaf)
def find_node(nodes, preferred_name="Backend Team"):
    for node in (nodes or []):
        if node.get("name", "").lower() == preferred_name.lower():
            return node["id"]
    # fallback: deepest node in first branch
    for node in (nodes or []):
        children = node.get("nodes") or node.get("children") or []
        found = find_node(children, preferred_name)
        if found:
            return found
    return nodes[0]["id"] if nodes else None

tree_nodes = tree.get("data", [])
dump("tree raw", tree)
if isinstance(tree_nodes, dict):
    dump("tree.data is dict, keys", list(tree_nodes.keys()))
    tree_nodes = tree_nodes.get("nodes", tree_nodes.get("children", []))
target_node_id = find_node(tree_nodes)

if not target_node_id:
    ok("No org nodes found — creating a root node")
    node_payload = {"name": f"E2E Test Node {RUN_ID}", "parentId": None}
    node_resp = requests.post(f"{BASE_URL}/api/orgnodes", json=node_payload, headers=admin_headers)
    node_body = assert_ok(node_resp, "Create org node")
    target_node_id = node_body["data"]
    ok(f"Created OrgNode: {target_node_id}")
if not target_node_id and tree_nodes:
    target_node_id = tree_nodes[0]["id"]

info(f"Target OrgNode ID: {target_node_id}")

# Assign requester as Member
assign_payload = {
    "orgNodeId":  target_node_id,
    "employeeId": requester_id,
    "role":       1   # OrgRole.Member = 1
}
resp = requests.post(
    f"{BASE_URL}/api/orgnodes/{target_node_id}/assignments",
    json=assign_payload,
    headers=admin_headers
)
assert_ok(resp, "Assign Requester to OrgNode")

# ── Step 9 — Requester submits a request ─────────────────────────────────────
header("STEP 9 — Requester logs in and submits an 'Other' request")

requester_headers = login(f"emp_requester_{RUN_ID}@test.com", requester_pwd)

request_payload = {
    "requestType": REQUEST_TYPE_OTHER,
    "data":        {"description": "E2E test request — please approve!"}
}
resp = requests.post(
    f"{BASE_URL}/api/Employees/requests/me",
    json=request_payload,
    headers=requester_headers
)
req_body = assert_ok(resp, "Submit request")
request_id = req_body["data"]
ok(f"Request ID: {request_id}")

# ── Step 10 — Approver-1 views pending approvals ─────────────────────────────
header("STEP 10 — Approver-1 views pending approvals")
approver1_headers = login(f"emp_approver1_{RUN_ID}@test.com", approver1_pwd)

resp = requests.get(f"{BASE_URL}/api/Employees/requests/approvals/pending", headers=approver1_headers)
pending1 = assert_ok(resp, "Approver-1 pending approvals")
items1 = pending1.get("data", {}).get("items", pending1.get("data", []))
if any(str(r.get("id")) == str(request_id) for r in items1):
    ok(f"✔ Request {request_id} IS visible to Approver-1")
else:
    fail(f"Request {request_id} NOT visible to Approver-1 — pending list: {[r.get('id') for r in items1]}")
    sys.exit(1)
dump("Approver-1 pending list", items1)

# ── Step 11 — Approver-2 views pending approvals ─────────────────────────────
header("STEP 11 — Approver-2 views pending approvals")
approver2_headers = login(f"emp_approver2_{RUN_ID}@test.com", approver2_pwd)

resp = requests.get(f"{BASE_URL}/api/Employees/requests/approvals/pending", headers=approver2_headers)
pending2 = assert_ok(resp, "Approver-2 pending approvals")
items2 = pending2.get("data", {}).get("items", pending2.get("data", []))
if any(str(r.get("id")) == str(request_id) for r in items2):
    ok(f"✔ Request {request_id} IS visible to Approver-2")
else:
    fail(f"Request {request_id} NOT visible to Approver-2 — pending list: {[r.get('id') for r in items2]}")
    sys.exit(1)
dump("Approver-2 pending list", items2)

# ── Step 12 — Approver-1 approves the request ────────────────────────────────
header("STEP 12 — Approver-1 approves the request")
approve_payload = {"comment": "Approved by Approver-1 during E2E test"}
resp = requests.post(
    f"{BASE_URL}/api/Employees/requests/approvals/{request_id}/approve",
    json=approve_payload,
    headers=approver1_headers
)
assert_ok(resp, "Approver-1 approves request")
ok("Request approved by Approver-1!")

# ── Step 13 — View request details (who approved, timestamp) ─────────────────
header("STEP 13 — View request details — approval history")
resp = requests.get(f"{BASE_URL}/api/Employees/requests/me/{request_id}", headers=requester_headers)
details = assert_ok(resp, "Get request details")
history = details.get("data", {}).get("approvalHistory", [])
if history:
    ok(f"Approval history ({len(history)} record(s)):")
    for entry in history:
        approver_name = entry.get("approverName", entry.get("approverId", "?"))
        status       = entry.get("status", "?")
        timestamp    = entry.get("createdAt", entry.get("approvedAt", "?"))
        comment      = entry.get("comment", "")
        print(f"      • {approver_name}  →  {status}  at {timestamp}  [{comment}]")
else:
    info("No approval history returned (check response shape)")
dump("Full request details", details.get("data", {}))

# ── Step 14 — Approver-2 views pending approvals (must be empty) ─────────────
header("STEP 14 — Approver-2 views pending approvals — must be EMPTY")
resp = requests.get(f"{BASE_URL}/api/Employees/requests/approvals/pending", headers=approver2_headers)
pending2_after = assert_ok(resp, "Approver-2 pending approvals (after approval)")
items2_after = pending2_after.get("data", {}).get("items", pending2_after.get("data", []))
if any(str(r.get("id")) == str(request_id) for r in items2_after):
    fail(f"Request {request_id} is STILL visible to Approver-2 — it should have been cleared!")
    sys.exit(1)
else:
    ok(f"✔ Request {request_id} no longer appears in Approver-2's pending list")

# ── ALL DONE — print credentials summary ─────────────────────────────────────
header("✅  ALL STEPS PASSED — Test run complete")
print()
print(f"  {BOLD}{'─'*56}{RESET}")
print(f"  {BOLD}  Credentials for manual testing (run id: {RUN_ID}){RESET}")
print(f"  {BOLD}{'─'*56}{RESET}")
print()
users = [
    ("Company Admin",  ADMIN_EMAIL,                              ADMIN_PASSWORD,   None,            "pre-existing"),
    ("Approver-1",     f"emp_approver1_{RUN_ID}@test.com",      "Passdev@1234",   approver1_id,    "created this run"),
    ("Approver-2",     f"emp_approver2_{RUN_ID}@test.com",      "Passdev@1234",   approver2_id,    "created this run"),
    ("Requester",      f"emp_requester_{RUN_ID}@test.com",      "Passdev@1234",   requester_id,    "created this run"),
]
for role, email, password, emp_id, note in users:
    emp_part = f"  empId: {YELLOW}{emp_id}{RESET}" if emp_id else ""
    print(f"  {CYAN}{role:<18}{RESET}  email: {YELLOW}{email:<45}{RESET}  pw: {GREEN}{password}{RESET}{emp_part}  {DIM}({note}){RESET}")
print()
print(f"  {DIM}Request ID  : {request_id}{RESET}")
print(f"  {DIM}Definition  : {definition_id}{RESET}")
print(f"  {DIM}Role ID     : {role_id}{RESET}")
print(f"  {DIM}OrgNode ID  : {target_node_id}{RESET}")
print()
print(f"  {BOLD}{CYAN}POST {BASE_URL}/api/auth/login{RESET}  {DIM}→ {{\"email\":\"...\",\"password\":\"...\"}}{RESET}")
print()

# ── Tokens (copy-paste ready for Postman / curl) ──────────────────────────────
print(f"  {BOLD}{'─'*56}{RESET}")
print(f"  {BOLD}  Bearer tokens (valid until server restart){RESET}")
print(f"  {BOLD}{'─'*56}{RESET}")
print()
for role, email, password, emp_id, note in users:
    token = _tokens.get(email)
    if token:
        print(f"  {CYAN}{role:<18}{RESET}")
        print(f"  {DIM}Authorization: Bearer {token}{RESET}")
        print()
print()
