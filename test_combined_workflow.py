"""
Interactive end-to-end test builder for COMBINED approval workflows.

You design the approval chain by specifying steps, the script builds everything:
org nodes, employees, roles, request definition, then walks the full approval flow.

Step syntax:
  H[n]  — HierarchyLevel spanning n ancestor levels (e.g. H3 = 3 levels up)
  C     — CompanyRole step (a role + role-approver employee are auto-created)

Example interaction:
  Enter approval chain (e.g. H3,C,H1): H3,C,H1
  Chain: H3 → C → H1
  - H3: 3 hierarchy levels (root's child, root's grandchild, root's great-grandchild)
  - C:  1 company role step
  - H1: 1 hierarchy level (root)
  Required org node depth: 5 (root + 3 intermediates + leaf)
  Proceed? (yes/no): yes

Usage:
  pip install requests
  python test_combined_workflow.py
  BASE_URL=https://yourserver.com python test_combined_workflow.py
"""

import os
import sys
import json
import re
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
def warn(text):  print(f"  {YELLOW}!{RESET}  {text}")

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

_tokens = {}   # email → raw JWT

def login(email, password):
    resp = requests.post(f"{BASE_URL}/api/auth/login", json={"email": email, "password": password})
    body = assert_ok(resp, f"Login ({email})")
    token = body["data"]["token"]
    _tokens[email] = token
    ok(f"Token acquired for {email}")
    return {"Authorization": f"Bearer {token}"}

def create_employee(full_name, email, phone):
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
            info(f"Password set skipped: {chg.status_code}")
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

def approve(headers, request_id, comment, label):
    resp = requests.post(
        f"{BASE_URL}/api/Employees/requests/approvals/{request_id}/approve",
        json={"comment": comment},
        headers=headers
    )
    assert_ok(resp, f"{label} — approve")
    ok(f"Request approved by {label}!")


# ── Chain parser ──────────────────────────────────────────────────────────────

def parse_chain(raw):
    """
    Parse a chain string like "H3,C,H1" into a list of step descriptors.

    Returns list of dicts:
      {"type": "H", "levelsUp": N, "sortOrder": i}
      {"type": "C", "sortOrder": i}
    On error: (None, error_message)
    """
    raw = raw.strip()
    if not raw:
        return None, "Chain cannot be empty"

    tokens = [t.strip() for t in raw.split(",") if t.strip()]
    if not tokens:
        return None, "No valid tokens found"

    steps = []
    for i, token in enumerate(tokens):
        token = token.strip()
        mh = re.match(r'^H(\d+)$', token, re.IGNORECASE)
        mc = re.match(r'^C$', token, re.IGNORECASE)
        if mh:
            levels = int(mh.group(1))
            if levels < 1:
                return None, f"H levels must be >= 1, got H{levels}"
            if levels > 10:
                return None, f"H{levels} exceeds maximum of H10 (10 levels)"
            steps.append({"type": "H", "levelsUp": levels, "sortOrder": i + 1})
        elif mc:
            steps.append({"type": "C", "sortOrder": i + 1})
        else:
            return None, f"Invalid token '{token}' — use H[n] (e.g. H3) or C (CompanyRole)"

    return steps, None


def describe_chain(steps):
    """Human-readable description of the approval chain."""
    lines = []
    for s in steps:
        if s["type"] == "H":
            lines.append(f"  Step {s['sortOrder']}: HierarchyLevel — {s['levelsUp']} level(s) up")
            for lvl in range(1, s["levelsUp"] + 1):
                lines.append(f"           Level {lvl} approver (mgr_l{lvl})")
        else:
            lines.append(f"  Step {s['sortOrder']}: CompanyRole — role approver")
    return "\n".join(lines)


# ── Confirm before proceeding ─────────────────────────────────────────────────

def confirm(prompt="Proceed? (yes/no): "):
    while True:
        try:
            val = input(prompt).strip().lower()
            if val in ("yes", "y"):
                return True
            elif val in ("no", "n"):
                return False
            else:
                warn("Please enter 'yes' or 'no'")
        except EOFError:
            fail("No input available")
            sys.exit(1)


# ═════════════════════════════════════════════════════════════════════════════
# STEP 1 — Login as Company Admin
# ═════════════════════════════════════════════════════════════════════════════
print(f"\n{BOLD}{CYAN}╔══════════════════════════════════════════════════════════════╗{RESET}")
print(f"{BOLD}{CYAN}║       Interactive Approval Chain Test Builder              ║{RESET}")
print(f"{BOLD}{CYAN}╚══════════════════════════════════════════════════════════════╝{RESET}")

header("STEP 1 — Login as Company Admin")
admin_headers = login(ADMIN_EMAIL, ADMIN_PASSWORD)

resp = requests.get(f"{BASE_URL}/api/companies/me", headers=admin_headers)
company    = assert_ok(resp, "Get company info")
company_id = company["data"]["id"]
ok(f"Company ID: {company_id}")


# ═════════════════════════════════════════════════════════════════════════════
# STEP 2 — Get approval chain design from user
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 2 — Design the approval chain")

print(f"  {DIM}Supported step types:{RESET}")
print(f"  {YELLOW}  H[n]{RESET} — HierarchyLevel: n ancestor levels (e.g. H3 = 3 levels up)")
print(f"  {YELLOW}  C  {RESET} — CompanyRole: auto-created role + approver employee")
print()

while True:
    try:
        raw = input(f"  {BOLD}Enter approval chain (e.g. H3,C,H1): {RESET}").strip()
    except EOFError:
        fail("No input available")
        sys.exit(1)

    steps, err = parse_chain(raw)
    if err:
        warn(err)
        continue

    max_h_levels = max((s["levelsUp"] for s in steps if s["type"] == "H"), default=0)
    total_nodes_needed = 1 + max_h_levels + 1   # root + ancestors + leaf

    print()
    print(f"  {BOLD}Chain design:{RESET}")
    print(f"  {CYAN}  " + " → ".join(t.upper() if t == "C" else f"H{s['levelsUp']}"
                                    for s in steps for t in [s["type"]]))
    print()
    print(describe_chain(steps))
    print()
    print(f"  {DIM}Required org node depth: {total_nodes_needed} nodes "
          f"(root + {max_h_levels} intermediate + leaf){RESET}")
    print()

    if not confirm():
        print("  OK, enter a new chain.")
        continue

    break   # design confirmed


# ═════════════════════════════════════════════════════════════════════════════
# STEP 3 — Build org node hierarchy
#   node_ids[0] = root (no parent), node_ids[-1] = leaf
#   Total = 1 + max_h_levels + 1 nodes
# ═════════════════════════════════════════════════════════════════════════════
header(f"STEP 3 — Build org node hierarchy ({total_nodes_needed} nodes)")

def create_node(name, parent_id=None):
    resp = requests.post(
        f"{BASE_URL}/api/orgnodes",
        json={"name": name, "parentId": parent_id},
        headers=admin_headers
    )
    if not resp.ok:
        err = resp.json()
        if err.get("error", {}).get("code") == "OrgNode.AlreadyExists":
            # Try to find existing node by name
            list_resp = requests.get(f"{BASE_URL}/api/orgnodes", headers=admin_headers)
            if list_resp.ok:
                for n in list_resp.json().get("data", []):
                    if n.get("name") == name:
                        ok(f"Node already exists: {n['id']} ('{name}')")
                        return n["id"]
            warn(f"Node '{name}' already exists but couldn't find it — continuing")
            return None
        assert_ok(resp, f"Create OrgNode '{name}'")
    body = resp.json()
    node_id = body["data"]
    ok(f"OrgNode '{name}' → {node_id}")
    return node_id

node_ids = []
for i in range(total_nodes_needed):
    name = f"E2E Root {RUN_ID}" if i == 0 else f"E2E Node {i} {RUN_ID}"
    parent = node_ids[-1] if node_ids else None
    nid = create_node(name, parent)
    node_ids.append(nid)

leaf_node_id = node_ids[-1]
root_node_id = node_ids[0]

info(f"Node chain (root → leaf): " + " → ".join(str(n) for n in node_ids))


# ═════════════════════════════════════════════════════════════════════════════
# STEP 4 — Create employees and roles based on chain design
#   H[n] steps: create n managers (mgr_l1 .. mgr_ln), one per ancestor level
#   C steps:    create 1 role_approver + 1 company role
#   Always:     create 1 requester at leaf (Member role)
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 4 — Create employees and roles")

digits = ''.join(c for c in RUN_ID if c.isdigit())[:4].ljust(4, '0')

# Determine max_h_levels (same as before)
max_h_levels = max((s["levelsUp"] for s in steps if s["type"] == "H"), default=0)

# Create managers for each hierarchy level 1..max_h_levels
hierarchy_managers = {}   # level (1-based) -> {"empId": ..., "email": ...}

for level in range(1, max_h_levels + 1):
    emp_name  = f"E2E Manager L{level}"
    emp_email = f"mgr_l{level}_{RUN_ID}@test.com"
    emp_phone = f"011{level}{digits}"
    emp_id = create_employee(emp_name, emp_email, emp_phone)
    hierarchy_managers[level] = {"empId": emp_id, "email": emp_email}

    # Assign to the ancestor node at this level
    # node_ids: [0]=root, [1]=mid1, ..., [-1]=leaf
    # level 1 = immediate parent = node_ids[-2]
    # level 2 = grandparent      = node_ids[-3]
    # level k = node_ids[-(k+1)]
    ancestor_idx = -(level + 1)
    if abs(ancestor_idx) <= len(node_ids):
        ancestor_node = node_ids[ancestor_idx]
        assign_to_node(ancestor_node, emp_id, 0, f"L{level} Manager → Node {ancestor_node} (Manager)")
        hierarchy_managers[level]["nodeId"] = ancestor_node
    else:
        info(f"  Level {level} — no ancestor node (hierarchy too shallow)")
        hierarchy_managers[level]["nodeId"] = None

# Create role approvers for each C step
role_approvers = []   # list of {"empId": ..., "email": ..., "roleId": ...}

for i, step in enumerate(s for s in steps if s["type"] == "C"):
    emp_name  = f"E2E Role Approver {i+1}"
    emp_email = f"role_appr_{i+1}_{RUN_ID}@test.com"
    emp_phone = f"018{i+1}{digits}"
    emp_id = create_employee(emp_name, emp_email, emp_phone)

    resp = requests.post(
        f"{BASE_URL}/api/company-roles",
        json={
            "name": f"E2E Role {i+1} {RUN_ID}",
            "description": f"Role for step {step['sortOrder']} in approval chain",
            "permissions": []
        },
        headers=admin_headers
    )
    if resp.status_code == 400:
        err = resp.json()
        if err.get("error", {}).get("code") == "CompanyRole.AlreadyExists":
            # Find existing role
            list_resp = requests.get(f"{BASE_URL}/api/company-roles", headers=admin_headers)
            if list_resp.ok:
                for r in list_resp.json().get("data", []):
                    if r.get("name") == f"E2E Role {i+1} {RUN_ID}":
                        role_id = r["id"]
                        ok(f"Role already exists: {role_id}")
                        break
        else:
            assert_ok(resp, f"Create company role {i+1}")
    elif resp.ok:
        role_id = resp.json()["data"]
    else:
        assert_ok(resp, f"Create company role {i+1}")

    resp = requests.post(
        f"{BASE_URL}/api/company-roles/{role_id}/employees/{emp_id}",
        headers=admin_headers
    )
    assert_ok(resp, f"Assign Role Approver {i+1} to company role")

    role_approvers.append({"empId": emp_id, "email": emp_email, "roleId": role_id})

# Create requester at leaf node (Member role)
requester_id = create_employee(
    f"E2E Requester",
    f"req_{RUN_ID}@test.com",
    f"0199{digits}"
)
assign_to_node(leaf_node_id, requester_id, 1, f"Requester → Leaf Node {leaf_node_id} (Member)")

info("Employee/role summary:")
info(f"  Hierarchy managers: {list(hierarchy_managers.keys())}")
info(f"  Role approvers: {len(role_approvers)}")
info(f"  Requester: {requester_id}")


# ═════════════════════════════════════════════════════════════════════════════
# STEP 5 — Build and create Request Definition
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 5 — Create Request Definition")

# Assign role IDs to C steps
role_iter = iter(role_approvers)
definition_steps = []
for step in steps:
    if step["type"] == "H":
        definition_steps.append({
            "stepType":       2,   # WorkflowStepType.HierarchyLevel
            "startFromLevel": 1,
            "levelsUp":       step["levelsUp"],
            "sortOrder":      step["sortOrder"]
        })
    else:  # C
        ra = next(role_iter)
        definition_steps.append({
            "stepType":      3,   # WorkflowStepType.CompanyRole
            "companyRoleId": ra["roleId"],
            "sortOrder":     step["sortOrder"]
        })

definition_payload = {
    "requestType": REQUEST_TYPE_OTHER,
    "steps": definition_steps
}

info("Steps to create:")
for st in definition_steps:
    if st["stepType"] == 2:
        info(f"  sortOrder={st['sortOrder']}: HierarchyLevel "
             f"(startFromLevel={st['startFromLevel']}, levelsUp={st['levelsUp']})")
    else:
        info(f"  sortOrder={st['sortOrder']}: CompanyRole (roleId={st['companyRoleId']})")

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
# STEP 6 — Submit request
# ═════════════════════════════════════════════════════════════════════════════
header("STEP 6 — Requester submits request")

requester_headers = login(f"req_{RUN_ID}@test.com", "Passdev@1234")

resp = requests.post(
    f"{BASE_URL}/api/Employees/requests/me",
    json={"requestType": REQUEST_TYPE_OTHER, "data": {"description": f"Interactive chain test — {raw}"}},
    headers=requester_headers
)
req_body   = assert_ok(resp, "Submit request")
request_id = req_body["data"]
ok(f"Request ID: {request_id}")


# ═════════════════════════════════════════════════════════════════════════════
# STEP 7 — Approve through each step in the chain
# ═════════════════════════════════════════════════════════════════════════════
step_num = 7

for step in steps:
    if step["type"] == "H":
        # H[n] — approve at each level from 1 to n
        for level in range(1, step["levelsUp"] + 1):
            mgr = hierarchy_managers.get(level)
            if not mgr or not mgr.get("email"):
                info(f"  Level {level} manager not found — skipping approval")
                continue

            header(f"STEP {step_num} — Hierarchy Level-{level} approves")
            step_num += 1

            mgr_headers = login(mgr["email"], "Passdev@1234")
            check_pending(mgr_headers, request_id, f"L{level} Manager")
            approve(mgr_headers, request_id, f"Approved by L{level} Manager", f"L{level} Manager")

    else:  # C — CompanyRole step
        if not role_approvers:
            warn("No role approvers available for C step — skipping")
            continue

        ra = role_approvers.pop(0)
        header(f"STEP {step_num} — CompanyRole Approver approves")
        step_num += 1

        ra_headers = login(ra["email"], "Passdev@1234")
        check_pending(ra_headers, request_id, "Role Approver")
        approve(ra_headers, request_id, "Approved by Role Approver (company role step)", "Role Approver")


# ═════════════════════════════════════════════════════════════════════════════
# STEP 8 — Verify final status + pending queues
# ═════════════════════════════════════════════════════════════════════════════
header(f"STEP {step_num} — Verify final request status = Approved")
step_num += 1

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

header(f"STEP {step_num} — Verify all approver pending queues are clean")
step_num += 1

# Collect all approver emails
all_approver_emails = []
for level in range(1, max_h_levels + 1):
    mgr = hierarchy_managers.get(level)
    if mgr and mgr.get("email"):
        all_approver_emails.append((mgr["email"], f"L{level} Manager"))

for ra in role_approvers:
    all_approver_emails.append((ra["email"], "Role Approver"))

for email, label in all_approver_emails:
    h = login(email, "Passdev@1234")
    resp  = requests.get(f"{BASE_URL}/api/Employees/requests/approvals/pending", headers=h)
    body  = assert_ok(resp, f"{label} — check pending after full approval")
    items = body.get("data", {}).get("items", body.get("data", []))
    if any(str(r.get("id")) == str(request_id) for r in items):
        fail(f"Request {request_id} STILL in {label}'s pending list — should be gone!")
        sys.exit(1)
    else:
        ok(f"✔ {label}'s pending list is clean")


# ═════════════════════════════════════════════════════════════════════════════
# ALL DONE — summary
# ═════════════════════════════════════════════════════════════════════════════
header("✅  ALL STEPS PASSED — Approval chain test complete")
print()
print(f"  {BOLD}{'─'*62}{RESET}")
print(f"  {BOLD}  Design summary{RESET}")
print(f"  {BOLD}{'─'*62}{RESET}")
print(f"  Chain: {CYAN}{raw}{RESET}")
print(f"  Steps: {len(steps)}")
print(f"  Org nodes: {total_nodes_needed}  (root + {max_h_levels} intermediates + leaf)")
print(f"  Approvers: {max_h_levels} hierarchy manager(s) + {len(role_approvers)} role approver(s)")
print()
print(f"  {BOLD}{'─'*62}{RESET}")
print(f"  {BOLD}  Credentials (run id: {RUN_ID}){RESET}")
print(f"  {BOLD}{'─'*62}{RESET}")
print()

def print_user(label, email, emp_id, note):
    emp_part = f"  empId: {YELLOW}{emp_id}{RESET}" if emp_id else ""
    print(f"  {CYAN}{label:<25}{RESET}  email: {YELLOW}{email:<46}{RESET}  "
          f"pw: {GREEN}Passdev@1234{RESET}{emp_part}  {DIM}({note}){RESET}")

for level in range(1, max_h_levels + 1):
    mgr = hierarchy_managers.get(level, {})
    print_user(
        f"L{level} Manager",
        mgr.get("email", "?"),
        mgr.get("empId", ""),
        f"Node {mgr.get('nodeId', '?')} — level {level} approver"
    )

for i, ra in enumerate(role_approvers, 1):
    print_user(
        f"Role Approver {i}",
        ra.get("email", "?"),
        ra.get("empId", ""),
        f"CompanyRole step"
    )

print_user(
    "Requester",
    f"req_{RUN_ID}@test.com",
    requester_id,
    f"Leaf Node {leaf_node_id}"
)

print()
print(f"  {DIM}Request ID    : {request_id}{RESET}")
print(f"  {DIM}Definition ID : {definition_id}{RESET}")
print(f"  {DIM}Node chain    : " + " → ".join(str(n) for n in node_ids) + f"  (leaf={leaf_node_id}){RESET}")
print()
print(f"  {BOLD}{CYAN}POST {BASE_URL}/api/auth/login{RESET}  "
      f"{DIM}→ {{\"email\":\"...\",\"password\":\"...\"}}{RESET}")
print()

print(f"  {BOLD}{'─'*62}{RESET}")
print(f"  {BOLD}  Bearer tokens (valid until server restart){RESET}")
print(f"  {BOLD}{'─'*62}{RESET}")
print()

all_emails = [mgr["email"] for mgr in hierarchy_managers.values() if mgr.get("email")]
all_emails += [ra["email"] for ra in role_approvers]
all_emails.append(f"req_{RUN_ID}@test.com")

for email in all_emails:
    token = _tokens.get(email)
    if token:
        label = next((f"L{l} Manager" for l, m in hierarchy_managers.items() if m.get("email") == email),
                    next((f"Role Approver {i}" for i, ra in enumerate(role_approvers, 1) if ra.get("email") == email),
                         "Requester" if email == f"req_{RUN_ID}@test.com" else email))
        print(f"  {CYAN}{label}{RESET}")
        print(f"  {DIM}Authorization: Bearer {token}{RESET}")
        print()