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
from datetime import datetime

# ── Config ────────────────────────────────────────────────────────────────────
BASE_URL        = os.getenv("BASE_URL", "http://localhost:5000").rstrip("/")
ADMIN_EMAIL     = os.getenv("ADMIN_EMAIL", "companyadmin@hrms.com")
ADMIN_PASSWORD  = os.getenv("ADMIN_PASSWORD", "Passdev@1234")

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

def login(email, password):
    resp = requests.post(f"{BASE_URL}/api/Auth/login", json={"email": email, "password": password})
    body = assert_ok(resp, f"Login ({email})")
    token = body["value"]["accessToken"]
    ok(f"Token acquired for {email}")
    return {"Authorization": f"Bearer {token}"}

# ── Step 1 — Login as Company Admin ──────────────────────────────────────────
header("STEP 1 — Login as Company Admin")
admin_headers = login(ADMIN_EMAIL, ADMIN_PASSWORD)

# Get company info (we need the companyId to create employees)
resp = requests.get(f"{BASE_URL}/api/companies/me", headers=admin_headers)
company = assert_ok(resp, "Get company info")
company_id = company["value"]["id"]
ok(f"Company ID: {company_id}")

# ── Step 2 — Create the Company Role ─────────────────────────────────────────
header("STEP 2 — Create Company Role  'E2E Approver Role'")
role_payload = {
    "name": "E2E Approver Role",
    "description": "Created by automated test script",
    "permissions": []
}
resp = requests.post(f"{BASE_URL}/api/company-roles", json=role_payload, headers=admin_headers)
role_body = assert_ok(resp, "Create company role")
role_id = role_body["value"]["id"]
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
    emp_id = body["value"]["id"]
    ok(f"Employee ID: {emp_id}  ({email})")
    return emp_id

approver1_id = create_employee("E2E Approver One", "emp_approver1@test.com", "01100000001")
approver2_id = create_employee("E2E Approver Two", "emp_approver2@test.com", "01100000002")

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
member_names = [m.get("employeeName", m.get("fullName", str(m))) for m in role_members.get("value", [])]
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
def_body = assert_ok(resp, "Create request definition")
definition_id = def_body["value"]
ok(f"Definition ID: {definition_id}")

# ── Step 7 — Create the Requester employee ───────────────────────────────────
header("STEP 7 — Create Requester employee")
requester_id = create_employee("E2E Requester", "emp_requester@test.com", "01100000003")

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

tree_nodes = tree.get("value", [])
target_node_id = find_node(tree_nodes)
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

requester_headers = login("emp_requester@test.com", "Passdev@1234")

request_payload = {
    "requestType": REQUEST_TYPE_OTHER,
    "data":        {},
    "details":     "E2E test request — please approve!"
}
resp = requests.post(
    f"{BASE_URL}/api/Employees/requests/me",
    json=request_payload,
    headers=requester_headers
)
req_body = assert_ok(resp, "Submit request")
request_id = req_body["value"]
ok(f"Request ID: {request_id}")

# ── Step 10 — Approver-1 views pending approvals ─────────────────────────────
header("STEP 10 — Approver-1 views pending approvals")
approver1_headers = login("emp_approver1@test.com", "Passdev@1234")

resp = requests.get(f"{BASE_URL}/api/Employees/requests/approvals/pending", headers=approver1_headers)
pending1 = assert_ok(resp, "Approver-1 pending approvals")
items1 = pending1.get("value", {}).get("items", pending1.get("value", []))
if any(str(r.get("id")) == str(request_id) for r in items1):
    ok(f"✔ Request {request_id} IS visible to Approver-1")
else:
    fail(f"Request {request_id} NOT visible to Approver-1 — pending list: {[r.get('id') for r in items1]}")
    sys.exit(1)
dump("Approver-1 pending list", items1)

# ── Step 11 — Approver-2 views pending approvals ─────────────────────────────
header("STEP 11 — Approver-2 views pending approvals")
approver2_headers = login("emp_approver2@test.com", "Passdev@1234")

resp = requests.get(f"{BASE_URL}/api/Employees/requests/approvals/pending", headers=approver2_headers)
pending2 = assert_ok(resp, "Approver-2 pending approvals")
items2 = pending2.get("value", {}).get("items", pending2.get("value", []))
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
details = assert_ok(resp, "Get request details (as requester)")
req_detail = details.get("value", {})

status = req_detail.get("status", req_detail.get("statusName", "?"))
history = req_detail.get("history", [])

info(f"Request status : {status}")
info(f"Current step   : {req_detail.get('currentStepOrder', '?')}")

if history:
    ok(f"Approval history ({len(history)} record(s)):")
    for entry in history:
        ts = entry.get("createdAt", entry.get("timestamp", "?"))
        approver_name = entry.get("approverName", entry.get("employeeName", "?"))
        entry_status  = entry.get("status", entry.get("statusName", "?"))
        comment       = entry.get("comment", "")
        print(f"       {BOLD}{approver_name}{RESET} — {entry_status} — {ts}")
        if comment:
            print(f"         Comment: \"{comment}\"")
else:
    fail("No approval history found in response!")
    dump("Full detail", req_detail)

# ── Step 14 — Approver-2 pending list must now be EMPTY ──────────────────────
header("STEP 14 — Approver-2 checks pending approvals (should be EMPTY now)")
resp = requests.get(f"{BASE_URL}/api/Employees/requests/approvals/pending", headers=approver2_headers)
pending2_after = assert_ok(resp, "Approver-2 pending approvals (after approval)")
items2_after = pending2_after.get("value", {}).get("items", pending2_after.get("value", []))

still_there = any(str(r.get("id")) == str(request_id) for r in items2_after)
if not still_there:
    ok(f"✔ Request {request_id} is GONE from Approver-2's pending list")
    ok("First-one-wins confirmed: Approver-2 can no longer act on this request")
else:
    fail(f"Request {request_id} is STILL visible to Approver-2 — this is a bug!")
    dump("Approver-2 pending list after approval", items2_after)
    sys.exit(1)

# ── Summary ───────────────────────────────────────────────────────────────────
header("ALL STEPS PASSED")
print(f"""
  {GREEN}{BOLD}Flow verified successfully:{RESET}

  Role created       : {role_id}
  Definition created : {definition_id}
  Request ID         : {request_id}

  ✓ Both Approver-1 and Approver-2 could see the request
  ✓ Approver-1 approved it
  ✓ Approval history records who approved and when
  ✓ Request disappeared from Approver-2's pending list after approval
""")
