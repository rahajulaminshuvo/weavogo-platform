# Phase 4: Business Services Architecture

## 🏢 Four Core Business Services

This phase defines the architecture for the four main business microservices that handle domain-specific operations.

---

## 1️⃣ WeavoHCM - Human Capital Management

### Core Modules
```
Employee Management
├── Employee Records
├── Documents & Qualifications
├── Employment History
└── Family Information

Payroll Processing
├── Salary Structures
├── Deductions & Additions
├── Tax Calculations
├── Payslips Generation
└── Bank Transfers

Attendance & Time
├── Check-in/Check-out
├── Daily Attendance
├── Shift Management
└── Time-off Tracking

Leave Management
├── Leave Policies
├── Leave Requests
├── Approvals
└── Leave Balance

Performance Management
├── Goals & Objectives
├── Performance Reviews
├── Feedback & Coaching
└── Rating & Ranking

Recruitment
├── Job Postings
├── Applicant Tracking
├── Interviews
└── Offers & Onboarding
```

### Database Schema (Key Tables)
```sql
-- Employees
CREATE TABLE hcm.employees (
    id UUID PRIMARY KEY,
    employee_id VARCHAR(50) UNIQUE,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    email VARCHAR(255),
    date_of_birth DATE,
    gender VARCHAR(20),
    department_id UUID,
    manager_id UUID,
    position_id UUID,
    employment_type VARCHAR(50),  -- Full-time, Part-time, Contract
    date_of_joining DATE,
    date_of_exit DATE,
    employment_status VARCHAR(50),  -- Active, Inactive, Terminated
    created_at TIMESTAMP
);

-- Salary Structures
CREATE TABLE hcm.salary_structures (
    id UUID PRIMARY KEY,
    employee_id UUID REFERENCES hcm.employees(id),
    base_salary DECIMAL(15,2),
    hra DECIMAL(15,2),
    conveyance DECIMAL(15,2),
    effective_from DATE,
    effective_to DATE,
    created_at TIMESTAMP
);

-- Leave Policies
CREATE TABLE hcm.leave_policies (
    id UUID PRIMARY KEY,
    tenant_id UUID,
    leave_type VARCHAR(50),  -- Annual, Sick, Personal
    entitled_days INT,
    carryover_days INT,
    financial_year_start DATE,
    financial_year_end DATE
);

-- Leave Requests
CREATE TABLE hcm.leave_requests (
    id UUID PRIMARY KEY,
    employee_id UUID REFERENCES hcm.employees(id),
    leave_type VARCHAR(50),
    start_date DATE,
    end_date DATE,
    num_days INT,
    reason TEXT,
    status VARCHAR(50),  -- Pending, Approved, Rejected
    approved_by UUID,
    approved_at TIMESTAMP,
    created_at TIMESTAMP
);

-- Attendance
CREATE TABLE hcm.attendance (
    id UUID PRIMARY KEY,
    employee_id UUID REFERENCES hcm.employees(id),
    attendance_date DATE,
    check_in TIMESTAMP,
    check_out TIMESTAMP,
    working_hours DECIMAL(5,2),
    status VARCHAR(50),  -- Present, Absent, Late, Half-day
    created_at TIMESTAMP
);
```

### Key Workflows
```
1. Employee Onboarding
   Create Employee → Assign Department → Set Salary → Add Benefits
   → Generate First Payroll → Send Welcome Email

2. Payroll Processing (Monthly)
   Gather Attendance → Calculate Deductions → Apply Taxes
   → Generate Payslips → Process Payments → Send Notifications

3. Leave Approval
   Employee Submits → Manager Review → HR Approval
   → Update Balance → Send Confirmation

4. Performance Review
   Set Goals → Mid-year Review → Final Review → Rating
   → Feedback → Archive
```

### API Endpoints
```bash
# Employees
GET    /api/hcm/employees
POST   /api/hcm/employees
GET    /api/hcm/employees/{id}
PUT    /api/hcm/employees/{id}

# Payroll
GET    /api/hcm/payroll/{employeeId}
POST   /api/hcm/payroll/process
GET    /api/hcm/payslips/{employeeId}

# Leave
GET    /api/hcm/leaves/policies
POST   /api/hcm/leaves/requests
PUT    /api/hcm/leaves/requests/{id}/approve
GET    /api/hcm/leaves/balance/{employeeId}

# Attendance
POST   /api/hcm/attendance/check-in
POST   /api/hcm/attendance/check-out
GET    /api/hcm/attendance/records/{employeeId}

# Performance
GET    /api/hcm/performance/goals/{employeeId}
POST   /api/hcm/performance/reviews
PUT    /api/hcm/performance/reviews/{id}/submit
```

---

## 2️⃣ WeavoPLM - Product Lifecycle Management

### Core Modules
```
Product Management
├── Product Creation
├── Product Specifications
├── Product Classification
└── Product Hierarchy

Bill of Materials (BOM)
├── BOM Creation
├── Component Management
├── Assembly Structure
├── Revisions
└── Approvals

Change Management
├── Engineering Change Order (ECO)
├── Impact Analysis
├── Approvals
└── Implementation

Release Management
├── Release Planning
├── Release Approval
├── Launch Schedule
└── Distribution

Quality Management
├── Testing Plans
├── Test Cases
├── Certifications
└── Quality Reports

Document Management
├── Design Documents
├── Specifications
├── Test Reports
└── Version Control
```

### Database Schema (Key Tables)
```sql
-- Products
CREATE TABLE plm.products (
    id UUID PRIMARY KEY,
    tenant_id UUID,
    sku VARCHAR(100) UNIQUE,
    name VARCHAR(255),
    description TEXT,
    category_id UUID,
    current_version VARCHAR(50),
    lifecycle_status VARCHAR(50),  -- Concept, Design, Development, QC, Released, EOL
    created_at TIMESTAMP
);

-- BOM (Bill of Materials)
CREATE TABLE plm.bom (
    id UUID PRIMARY KEY,
    product_id UUID REFERENCES plm.products(id),
    version VARCHAR(50),
    revision_number INT,
    status VARCHAR(50),  -- Draft, Approved, Released, Obsolete
    released_date DATE,
    created_at TIMESTAMP
);

-- BOM Items
CREATE TABLE plm.bom_items (
    id UUID PRIMARY KEY,
    bom_id UUID REFERENCES plm.bom(id),
    component_id UUID,
    quantity DECIMAL(10,2),
    unit_of_measure VARCHAR(50),
    sequence_number INT,
    is_alternative BOOLEAN DEFAULT FALSE,
    lead_time_days INT
);

-- Engineering Change Order (ECO)
CREATE TABLE plm.eco (
    id UUID PRIMARY KEY,
    product_id UUID REFERENCES plm.products(id),
    eco_number VARCHAR(50) UNIQUE,
    title VARCHAR(255),
    description TEXT,
    change_reason VARCHAR(255),
    status VARCHAR(50),  -- Draft, Submitted, Under Review, Approved, Implemented
    priority VARCHAR(50),  -- Low, Medium, High, Critical
    created_by UUID,
    created_at TIMESTAMP
);

-- Releases
CREATE TABLE plm.releases (
    id UUID PRIMARY KEY,
    product_id UUID REFERENCES plm.products(id),
    release_number VARCHAR(50),
    release_date DATE,
    status VARCHAR(50),  -- Planned, Ready, Released, Supported, EOL
    release_notes TEXT,
    created_at TIMESTAMP
);
```

### Key Workflows
```
1. Product Development
   Create Product → Define Specs → Create BOM → Design Review
   → Technical Validation → Quality Testing → Release Approval
   → Launch → Market

2. BOM Management
   Create BOM → Add Components → Define Assemblies
   → Get Approvals → Release → Track Changes

3. Change Control
   Submit ECO → Impact Analysis → Technical Review
   → Management Approval → Implementation → Verification
```

### API Endpoints
```bash
# Products
GET    /api/plm/products
POST   /api/plm/products
GET    /api/plm/products/{id}
GET    /api/plm/products/{id}/versions

# BOM
GET    /api/plm/bom/{productId}
POST   /api/plm/bom
GET    /api/plm/bom/{id}/revisions
PUT    /api/plm/bom/{id}/release

# ECO
GET    /api/plm/eco
POST   /api/plm/eco
PUT    /api/plm/eco/{id}/approve
GET    /api/plm/eco/{id}/impacts

# Releases
GET    /api/plm/releases
POST   /api/plm/releases
PUT    /api/plm/releases/{id}/publish
```

---

## 3️⃣ WeavoERP - Enterprise Resource Planning

### Core Modules
```
Financial Management
├── General Ledger (GL)
├── Accounts Receivable (AR)
├── Accounts Payable (AP)
├── Fixed Assets
└── Bank Reconciliation

Supply Chain Management
├── Procurement
├── Purchase Orders
├── Goods Receipt
├── Inventory Management
└── Logistics

Vendor Management
├── Vendor Master
├── Vendor Contracts
├── Vendor Performance
└── Vendor Payments

Reporting & Analytics
├── Financial Statements
├── Trial Balance
├── Cost Analysis
├── Custom Reports
└── KPI Dashboards
```

### Database Schema (Key Tables)
```sql
-- GL Accounts
CREATE TABLE erp.gl_accounts (
    id UUID PRIMARY KEY,
    account_code VARCHAR(50) UNIQUE,
    account_name VARCHAR(255),
    account_type VARCHAR(50),  -- Asset, Liability, Equity, Revenue, Expense
    account_class VARCHAR(50),
    balance DECIMAL(15,2),
    is_active BOOLEAN,
    created_at TIMESTAMP
);

-- Journal Entries
CREATE TABLE erp.journal_entries (
    id UUID PRIMARY KEY,
    entry_date DATE,
    reference_number VARCHAR(100),
    description TEXT,
    total_debit DECIMAL(15,2),
    total_credit DECIMAL(15,2),
    status VARCHAR(50),  -- Draft, Posted
    created_by UUID,
    created_at TIMESTAMP
);

-- Invoices
CREATE TABLE erp.invoices (
    id UUID PRIMARY KEY,
    invoice_number VARCHAR(100) UNIQUE,
    customer_id UUID,
    invoice_date DATE,
    due_date DATE,
    currency_id UUID,
    amount DECIMAL(15,2),
    tax_amount DECIMAL(15,2),
    status VARCHAR(50),  -- Draft, Sent, Partial, Paid, Overdue
    created_at TIMESTAMP
);

-- Purchase Orders
CREATE TABLE erp.purchase_orders (
    id UUID PRIMARY KEY,
    po_number VARCHAR(100) UNIQUE,
    vendor_id UUID,
    order_date DATE,
    expected_delivery_date DATE,
    currency_id UUID,
    total_amount DECIMAL(15,2),
    status VARCHAR(50),  -- Draft, Sent, Accepted, Partial Rcv, Received
    created_at TIMESTAMP
);

-- Inventory Items
CREATE TABLE erp.inventory_items (
    id UUID PRIMARY KEY,
    item_code VARCHAR(100) UNIQUE,
    item_name VARCHAR(255),
    warehouse_id UUID,
    quantity_on_hand INT,
    quantity_reserved INT,
    reorder_level INT,
    unit_cost DECIMAL(15,4),
    created_at TIMESTAMP
);
```

### Key Workflows
```
1. Purchase to Payment
   Create PO → Send to Vendor → Receive Goods
   → Create Bill → 3-Way Match → Approve → Pay

2. Sales to Cash
   Create Invoice → Send to Customer → Receive Payment
   → Bank Reconciliation → AR Aging

3. Month-End Close
   Post Transactions → GL Reconciliation → Trial Balance
   → Adjustments → Financial Statements → Archive
```

### API Endpoints
```bash
# Accounting
GET    /api/erp/gl/accounts
POST   /api/erp/gl/accounts
GET    /api/erp/gl/entries
GET    /api/erp/gl/trial-balance

# AR/AP
GET    /api/erp/ar/invoices
POST   /api/erp/ar/invoices
PUT    /api/erp/ar/invoices/{id}/payment
GET    /api/erp/ap/bills

# Procurement
GET    /api/erp/purchase-orders
POST   /api/erp/purchase-orders
PUT    /api/erp/purchase-orders/{id}/receive

# Inventory
GET    /api/erp/inventory/stock-levels
GET    /api/erp/inventory/valuation
POST   /api/erp/inventory/adjustments

# Reporting
GET    /api/erp/reports/financial-statements
GET    /api/erp/reports/income-statement
GET    /api/erp/reports/balance-sheet
```

---

## 4️⃣ WeavoMES - Manufacturing Execution System

### Core Modules
```
Production Planning
├── Demand Forecasting
├── Master Production Schedule (MPS)
├── Material Requirements Planning (MRP)
└── Capacity Planning

Work Order Management
├── Work Order Creation
├── Routing Management
├── Material Allocation
├── Labor Assignment
└── Status Tracking

Quality Control
├── Inspection Plans
├── Test Cases
├── Defect Tracking
├── Non-Conformance Reports
└── Corrective Actions

Equipment Management
├── Asset Registry
├── Preventive Maintenance
├── Corrective Maintenance
├── Performance Tracking
└── Work Orders

Analytics & Reporting
├── OEE Calculation
├── Production Metrics
├── Quality Metrics
└── Cost Analysis
```

### Database Schema (Key Tables)
```sql
-- Work Orders
CREATE TABLE mes.work_orders (
    id UUID PRIMARY KEY,
    work_order_number VARCHAR(100) UNIQUE,
    product_id UUID,
    quantity INT,
    scheduled_start DATE,
    scheduled_completion DATE,
    actual_start TIMESTAMP,
    actual_completion TIMESTAMP,
    status VARCHAR(50),  -- Created, Released, In Progress, Completed
    priority INT,
    created_at TIMESTAMP
);

-- Work Order Operations
CREATE TABLE mes.work_order_operations (
    id UUID PRIMARY KEY,
    work_order_id UUID REFERENCES mes.work_orders(id),
    operation_sequence INT,
    work_center_id UUID,
    operation_name VARCHAR(255),
    planned_duration INTERVAL,
    actual_duration INTERVAL,
    status VARCHAR(50),
    created_at TIMESTAMP
);

-- Quality Inspections
CREATE TABLE mes.quality_inspections (
    id UUID PRIMARY KEY,
    work_order_id UUID REFERENCES mes.work_orders(id),
    inspection_type VARCHAR(50),  -- Input, In-Process, Final
    inspection_date TIMESTAMP,
    inspector_id UUID,
    result VARCHAR(50),  -- Pass, Fail, Conditional
    defects_found INT,
    comments TEXT
);

-- Equipment
CREATE TABLE mes.equipment (
    id UUID PRIMARY KEY,
    equipment_code VARCHAR(100) UNIQUE,
    equipment_name VARCHAR(255),
    location VARCHAR(100),
    work_center_id UUID,
    equipment_type VARCHAR(50),
    status VARCHAR(50),  -- Operational, Maintenance, Down
    last_maintenance TIMESTAMP,
    next_maintenance TIMESTAMP
);

-- Production Metrics
CREATE TABLE mes.production_metrics (
    id UUID PRIMARY KEY,
    equipment_id UUID REFERENCES mes.equipment(id),
    metric_date DATE,
    metric_hour INT,
    pieces_produced INT,
    defects INT,
    downtime_minutes INT,
    availability DECIMAL(5,2),
    performance DECIMAL(5,2),
    quality DECIMAL(5,2),
    oee DECIMAL(5,2)
);
```

### Key Workflows
```
1. Production Execution
   Create WO → Release to Shop → Operations → QC Check
   → Move to Next Operation → Complete WO → Update Inventory

2. Quality Control
   Define Inspection Plan → Perform Inspection
   → Document Results → Non-Conformance → Corrective Action

3. Maintenance
   Schedule Maintenance → Execute Maintenance
   → Record Downtime → Close Work Order
```

### API Endpoints
```bash
# Planning
GET    /api/mes/demand-forecast
GET    /api/mes/mps
GET    /api/mes/mrp
GET    /api/mes/capacity-plan

# Work Orders
GET    /api/mes/work-orders
POST   /api/mes/work-orders
PUT    /api/mes/work-orders/{id}/start
PUT    /api/mes/work-orders/{id}/complete

# Quality
GET    /api/mes/quality/inspections
POST   /api/mes/quality/inspections
GET    /api/mes/quality/defects

# Equipment
GET    /api/mes/equipment
GET    /api/mes/equipment/{id}/maintenance
POST   /api/mes/equipment/{id}/maintenance

# Reporting
GET    /api/mes/reports/oee
GET    /api/mes/reports/production-metrics
GET    /api/mes/reports/quality-metrics
```

---

## 🔗 Inter-Service Dependencies

```
┌─────────────────────────────────────────────────────┐
│            Auth & Security Platform                │
│  (Identity, AuthN, AuthZ, Audit)                   │
└────────────────────┬────────────────────────────────┘
                     │
         ┌───────────┼───────────┐
         ▼           ▼           ▼
   ┌─────────┐ ┌──────────┐ ┌──────────┐
   │ WeavoHCM│ │WeavoPLM  │ │WeavoERP  │
   └────┬────┘ └────┬─────┘ └────┬─────┘
        │           │             │
        └───────────┼─────────────┘
                    ▼
        ┌──────────────────────┐
        │ Master Data Platform │
        │ (Reference Data)     │
        └──────────────────────┘
                    ▲
                    │
        ┌───────────┴───────────┐
        ▼                       ▼
   ┌─────────┐          ┌──────────┐
   │WeavoMES │          │API Gw    │
   └─────────┘          └──────────┘
```

---

**Next**: Phase 5 - Inter-Service Communication Patterns
