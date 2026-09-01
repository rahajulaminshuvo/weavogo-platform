#!/usr/bin/env python3
"""Generates the Newman/Postman integration collection for the Item Master API.

Run:  python3 build_collection.py
Then: newman run ItemMaster.postman_collection.json -e local.postman_environment.json
"""
import json
from pathlib import Path

BASE = "{{baseUrl}}/api/v1"


def req(name, method, url, *, body=None, token="{{token}}", tests=None, prereq=None, auth=True):
    item = {
        "name": name,
        "event": [],
        "request": {
            "method": method,
            "header": [{"key": "Content-Type", "value": "application/json"}],
            "url": {"raw": url, "host": [url]},
        },
    }
    if auth:
        item["request"]["header"].append({"key": "Authorization", "value": f"Bearer {token}"})
    if body is not None:
        item["request"]["body"] = {"mode": "raw", "raw": json.dumps(body, indent=2)}
    if prereq:
        item["event"].append({"listen": "prerequest",
                              "script": {"type": "text/javascript", "exec": prereq}})
    if tests:
        item["event"].append({"listen": "test",
                              "script": {"type": "text/javascript", "exec": tests}})
    return item


def login(name, user, var):
    return req(
        name, "POST", f"{BASE}/auth/login",
        body={"userName": user, "password": "{{password}}"},
        auth=False,
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "const b = pm.response.json();",
            f"pm.collectionVariables.set('{var}', b.accessToken);",
            "pm.test('token issued', () => pm.expect(b.accessToken).to.be.a('string'));",
            "pm.test('roles present', () => pm.expect(b.roles.length).to.be.above(0));",
        ],
    )


def error_test(code, status):
    return [
        f"pm.test('HTTP {status}', () => pm.response.to.have.status({status}));",
        "const b = pm.response.json();",
        f"pm.test('code is {code}', () => pm.expect(b.error.code).to.eql('{code}'));",
        "pm.test('envelope shape', () => { pm.expect(b.error).to.have.property('message');"
        " pm.expect(b.error).to.have.property('traceId'); });",
    ]


items = [
    # ---- authentication ---------------------------------------------------
    login("Login — Category Manager", "catmgr", "tokenCat"),
    login("Login — IT Security Review", "itsec", "tokenSec"),
    login("Login — Finance Controller", "finctrl", "tokenFin"),
    login("Login — Read-Only", "readonly", "tokenRead"),

    # ---- create (SDS 10.3) -------------------------------------------------
    req("Create item — Draft (10.3)", "POST", f"{BASE}/items", token="{{tokenCat}}",
        prereq=["pm.collectionVariables.set('runCode', 'ICT-LTP-' + "
                "String(Date.now()).slice(-6));"],
        body={
            "itemCode": "{{runCode}}",
            "itemName": "Dell Latitude 7450 Laptop (integration run)",
            "itemFamilyId": 6,
            "baseUOMId": 1,
            "canPurchase": True, "canSell": False, "canManufacture": False,
            "canStock": True, "canTransfer": True,
            "isSerialControlled": True, "isLotControlled": False,
            "attributes": [],
            "businessUnitIds": [6, 10],
        },
        tests=[
            "pm.test('201 Created', () => pm.response.to.have.status(201));",
            "const b = pm.response.json();",
            "pm.collectionVariables.set('itemId', b.itemId);",
            "pm.test('starts in Draft (8.1)', () => pm.expect(b.itemStatus).to.eql('Draft'));",
            "pm.test('version 1', () => pm.expect(b.versionNumber).to.eql(1));",
            "pm.test('_links present (10.3)', () => pm.expect(b._links.submitForApproval)"
            ".to.include('/submit'));",
        ]),

    req("Create item — duplicate code -> 409", "POST", f"{BASE}/items", token="{{tokenCat}}",
        body={
            "itemCode": "{{runCode}}",
            "itemName": "Duplicate",
            "itemFamilyId": 6, "baseUOMId": 1,
            "canPurchase": True, "canStock": True,
            "attributes": [], "businessUnitIds": [6],
        },
        tests=error_test("ITEM_CODE_DUPLICATE", 409)),

    req("Create item — no transactable flag -> 400", "POST", f"{BASE}/items", token="{{tokenCat}}",
        body={
            "itemCode": "ZZ-BAD-000001", "itemName": "No flags",
            "itemFamilyId": 6, "baseUOMId": 1,
            "canPurchase": False, "canSell": False, "canManufacture": False,
            "canStock": True, "attributes": [], "businessUnitIds": [6],
        },
        tests=error_test("VALIDATION_FAILED", 400)),

    # ---- submit before required attributes (12.1 rule 7) -------------------
    req("Submit without required attributes -> 422", "POST", f"{BASE}/items/{{{{itemId}}}}/submit",
        token="{{tokenCat}}", body={"workflowTemplateCode": "ICT-ASSET"},
        tests=error_test("ATTRIBUTE_REQUIRED_MISSING", 422)),

    # ---- patch draft (10.6) ------------------------------------------------
    req("Patch Draft — fill required attributes", "PATCH", f"{BASE}/items/{{{{itemId}}}}",
        token="{{tokenCat}}",
        body={"attributes": [
            {"attributeCode": "BRAND", "value": "Dell"},
            {"attributeCode": "MODEL", "value": "Latitude 7450"},
            {"attributeCode": "CPU", "value": "Intel Core Ultra 7"},
            {"attributeCode": "RAM", "value": 16},
            {"attributeCode": "SSD", "value": 1},
            {"attributeCode": "DISPLAY", "value": "14\" FHD"},
            {"attributeCode": "OS", "value": "Windows 11 Pro"},
        ]},
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "pm.test('still Draft', () => pm.expect(pm.response.json().itemStatus).to.eql('Draft'));",
        ]),

    req("Patch Draft — wrong data type -> 422", "PATCH", f"{BASE}/items/{{{{itemId}}}}",
        token="{{tokenCat}}",
        body={"attributes": [{"attributeCode": "RAM", "value": "not-a-number"}]},
        tests=error_test("ATTRIBUTE_DATATYPE_MISMATCH", 422)),

    # ---- submit + approve chain (10.7, 8.2) --------------------------------
    req("Submit for approval (10.7)", "POST", f"{BASE}/items/{{{{itemId}}}}/submit",
        token="{{tokenCat}}", body={"workflowTemplateCode": "ICT-ASSET"},
        tests=[
            "pm.test('202 Accepted', () => pm.response.to.have.status(202));",
            "const b = pm.response.json();",
            "pm.collectionVariables.set('requestId', b.requestId);",
            "pm.test('step 1 pending', () => pm.expect(b.currentStepOrder).to.eql(1));",
            "pm.test('overall Pending', () => pm.expect(b.overallStatus).to.eql('Pending'));",
            "pm.test('item PendingApproval', () => pm.expect(b.itemStatus).to.eql('PendingApproval'));",
        ]),

    req("Approve step 1 — Category Manager", "POST",
        f"{BASE}/approval-requests/{{{{requestId}}}}/approval-actions", token="{{tokenCat}}",
        body={"stepOrder": 1, "decision": "Approved", "comments": "Matches standing laptop specification"},
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "const b = pm.response.json();",
            "pm.test('advanced to step 2', () => pm.expect(b.currentStepOrder).to.eql(2));",
            "pm.test('role is IT Security Review', () => "
            "pm.expect(b.currentStepRole).to.eql('IT Security Review'));",
        ]),

    req("Approve step 2 as wrong role -> 403", "POST",
        f"{BASE}/approval-requests/{{{{requestId}}}}/approval-actions", token="{{tokenFin}}",
        body={"stepOrder": 2, "decision": "Approved"},
        tests=error_test("APPROVAL_ROLE_MISMATCH", 403)),

    req("Approve step 2 — IT Security Review", "POST",
        f"{BASE}/approval-requests/{{{{requestId}}}}/approval-actions", token="{{tokenSec}}",
        body={"stepOrder": 2, "decision": "Approved", "comments": "Meets endpoint hardening baseline"},
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "pm.test('advanced to step 3', () => "
            "pm.expect(pm.response.json().currentStepOrder).to.eql(3));",
        ]),

    req("Approve step 3 — Finance Controller -> Active", "POST",
        f"{BASE}/approval-requests/{{{{requestId}}}}/approval-actions", token="{{tokenFin}}",
        body={"stepOrder": 3, "decision": "Approved", "comments": "Within capex threshold"},
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "const b = pm.response.json();",
            "pm.test('request Approved', () => pm.expect(b.overallStatus).to.eql('Approved'));",
            "pm.test('item Active (8.2.3)', () => pm.expect(b.itemStatus).to.eql('Active'));",
        ]),

    # ---- read paths (10.4, 10.5) ------------------------------------------
    req("Get item detail (10.4)", "GET", f"{BASE}/items/{{{{itemId}}}}", token="{{tokenCat}}",
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "const b = pm.response.json();",
            "pm.test('Active', () => pm.expect(b.itemStatus).to.eql('Active'));",
            "pm.test('classification resolved', () => "
            "pm.expect(b.classification.categoryName).to.eql('ICT Equipment'));",
            "pm.test('attributes resolved', () => pm.expect(b.attributes.length).to.be.above(0));",
            "pm.test('business units listed', () => pm.expect(b.businessUnits.length).to.eql(2));",
        ]),

    req("Search items (10.5)", "GET",
        f"{BASE}/items?categoryCode=ICT&status=Active&page=1&pageSize=20", token="{{tokenCat}}",
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "const b = pm.response.json();",
            "pm.test('pagination envelope', () => { pm.expect(b.pagination.page).to.eql(1);"
            " pm.expect(b.pagination.pageSize).to.eql(20); });",
            "pm.test('every row is ICT/Active', () => b.items.forEach(i => {"
            " pm.expect(i.categoryCode).to.eql('ICT'); pm.expect(i.itemStatus).to.eql('Active'); }));",
        ]),

    req("Search — pageSize is capped at 100", "GET", f"{BASE}/items?page=1&pageSize=5000",
        token="{{tokenCat}}",
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "pm.test('capped', () => pm.expect(pm.response.json().pagination.pageSize).to.eql(100));",
        ]),

    # ---- re-approval of an Active item (10.6, 8.4) -------------------------
    req("Patch Active item -> opens re-approval (10.6)", "PATCH", f"{BASE}/items/{{{{itemId}}}}",
        token="{{tokenCat}}",
        body={"attributes": [{"attributeCode": "RAM", "value": 32}],
              "changeReason": "RAM configuration corrected from 16GB to 32GB post-launch"},
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "const b = pm.response.json();",
            "pm.collectionVariables.set('requestId2', b.approvalRequestId);",
            "pm.test('item PendingApproval', () => pm.expect(b.itemStatus).to.eql('PendingApproval'));",
            "pm.test('pending version 2 (8.4)', () => pm.expect(b.pendingVersionNumber).to.eql(2));",
        ]),

    req("Re-approve step 1", "POST", f"{BASE}/approval-requests/{{{{requestId2}}}}/approval-actions",
        token="{{tokenCat}}", body={"stepOrder": 1, "decision": "Approved", "comments": "Spec bump agreed"},
        tests=["pm.test('200 OK', () => pm.response.to.have.status(200));"]),
    req("Re-approve step 2", "POST", f"{BASE}/approval-requests/{{{{requestId2}}}}/approval-actions",
        token="{{tokenSec}}", body={"stepOrder": 2, "decision": "Approved", "comments": "No security impact"},
        tests=["pm.test('200 OK', () => pm.response.to.have.status(200));"]),
    req("Re-approve step 3 -> Active at version 2", "POST",
        f"{BASE}/approval-requests/{{{{requestId2}}}}/approval-actions", token="{{tokenFin}}",
        body={"stepOrder": 3, "decision": "Approved", "comments": "Cost delta accepted"},
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "pm.test('Active again', () => pm.expect(pm.response.json().itemStatus).to.eql('Active'));",
        ]),

    req("Version history (8.4)", "GET", f"{BASE}/items/{{{{itemId}}}}/versions", token="{{tokenCat}}",
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "const b = pm.response.json();",
            "pm.test('two versions recorded', () => pm.expect(b.length).to.eql(2));",
            "pm.test('newest first', () => pm.expect(b[0].versionNumber).to.eql(2));",
            "pm.test('change reason kept', () => "
            "pm.expect(b[0].changeReason).to.include('32GB'));",
        ]),

    req("Audit log populated by triggers (8.3)", "GET", f"{BASE}/items/{{{{itemId}}}}/audit-log",
        token="{{tokenCat}}",
        tests=[
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "const b = pm.response.json();",
            "pm.test('has rows', () => pm.expect(b.length).to.be.above(0));",
            "pm.test('records the status changes', () => pm.expect("
            "b.filter(r => r.fieldName === 'ItemStatus').length).to.be.above(1));",
        ]),

    # ---- security matrix (12.3) -------------------------------------------
    req("Read-Only may view", "GET", f"{BASE}/items/{{{{itemId}}}}", token="{{tokenRead}}",
        tests=["pm.test('200 OK', () => pm.response.to.have.status(200));"]),

    req("Read-Only may not create -> 403", "POST", f"{BASE}/items", token="{{tokenRead}}",
        body={"itemCode": "ZZ-RO-000001", "itemName": "Blocked", "itemFamilyId": 6,
              "baseUOMId": 1, "canPurchase": True, "canStock": True,
              "attributes": [], "businessUnitIds": [6]},
        tests=["pm.test('403 Forbidden', () => pm.response.to.have.status(403));"]),

    req("No token -> 401", "GET", f"{BASE}/items/{{{{itemId}}}}", auth=False,
        tests=["pm.test('401 Unauthorized', () => pm.response.to.have.status(401));"]),

    # ---- obsolete item is terminal (8.1) ----------------------------------
    req("Patch the seeded Obsolete item -> 409", "PATCH", f"{BASE}/items/18", token="{{tokenCat}}",
        body={"itemName": "Cannot rename an obsolete item"},
        tests=error_test("INVALID_STATUS_TRANSITION", 409)),
]

collection = {
    "info": {
        "name": "WeavoGo Universal Item Master — API integration",
        "description": "End-to-end coverage of SDS Chapter 10 endpoints, the Chapter 8 "
                       "approval workflow, and the Chapter 12 validation and security matrices.",
        "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    },
    "item": items,
    "variable": [
        {"key": "baseUrl", "value": "http://localhost:5080"},
        {"key": "password", "value": "Weavo#2026"},
        {"key": "itemId", "value": ""},
        {"key": "requestId", "value": ""},
        {"key": "requestId2", "value": ""},
        {"key": "runCode", "value": ""},
        {"key": "tokenCat", "value": ""},
        {"key": "tokenSec", "value": ""},
        {"key": "tokenFin", "value": ""},
        {"key": "tokenRead", "value": ""},
    ],
}

environment = {
    "name": "Item Master — local",
    "values": [
        {"key": "baseUrl", "value": "http://localhost:5080", "enabled": True},
        {"key": "password", "value": "Weavo#2026", "enabled": True},
    ],
    "_postman_variable_scope": "environment",
}

here = Path(__file__).resolve().parent
(here / "ItemMaster.postman_collection.json").write_text(json.dumps(collection, indent=2) + "\n")
(here / "local.postman_environment.json").write_text(json.dumps(environment, indent=2) + "\n")
print(f"Wrote collection with {len(items)} requests.")
