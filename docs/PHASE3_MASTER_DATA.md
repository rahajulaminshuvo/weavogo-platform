# Phase 3: Master Data Platform Design

## 📊 Overview

The Master Data Platform is the single source of truth for all reference data across WeavoGo. It ensures data consistency, enables efficient caching, and supports multi-tenant scenarios.

## 🏗️ Eleven Core Domains

### 1. Core Reference Data
**Purpose**: Basic reference information used globally

```sql
CREATE TABLE master_data.languages (
    id UUID PRIMARY KEY,
    code VARCHAR(10) UNIQUE NOT NULL,  -- ISO 639-1 (en, fr, es)
    name VARCHAR(100) NOT NULL,
    native_name VARCHAR(100),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP
);

CREATE TABLE master_data.currencies (
    id UUID PRIMARY KEY,
    code VARCHAR(3) UNIQUE NOT NULL,  -- ISO 4217 (USD, EUR, GBP)
    name VARCHAR(100) NOT NULL,
    symbol VARCHAR(10),
    exchange_rate DECIMAL(10,4),
    is_active BOOLEAN DEFAULT TRUE
);

CREATE TABLE master_data.units_of_measure (
    id UUID PRIMARY KEY,
    code VARCHAR(10) UNIQUE NOT NULL,  -- kg, m, l, pcs
    name VARCHAR(100),
    measurement_class VARCHAR(50),  -- Weight, Length, Volume, Quantity
    conversion_factor DECIMAL(10,4),
    base_unit_id UUID
);
```

**API Endpoints**:
```bash
GET    /api/master-data/languages
POST   /api/master-data/languages
GET    /api/master-data/currencies
GET    /api/master-data/units-of-measure
```

---

### 2. Geography
**Purpose**: Location-based master data

```sql
CREATE TABLE master_data.countries (
    id UUID PRIMARY KEY,
    code VARCHAR(2) UNIQUE NOT NULL,  -- ISO 3166-1 (US, IN, FR)
    name VARCHAR(100) NOT NULL,
    currency_id UUID REFERENCES master_data.currencies(id),
    time_zone VARCHAR(50),
    is_active BOOLEAN DEFAULT TRUE
);

CREATE TABLE master_data.states_provinces (
    id UUID PRIMARY KEY,
    country_id UUID REFERENCES master_data.countries(id),
    code VARCHAR(10),
    name VARCHAR(100) NOT NULL,
    UNIQUE(country_id, code)
);

CREATE TABLE master_data.cities (
    id UUID PRIMARY KEY,
    state_id UUID REFERENCES master_data.states_provinces(id),
    name VARCHAR(100) NOT NULL,
    postal_code VARCHAR(20),
    latitude DECIMAL(9,6),
    longitude DECIMAL(9,6)
);
```

**Cache Strategy**:
```
Redis Key: master-data:countries
TTL: 24 hours
Update: Event-driven invalidation

Redis Key: master-data:country:{countryId}:states
TTL: 24 hours
```

---

### 3. Organization Structure
**Purpose**: Company and organizational hierarchy

```sql
CREATE TABLE master_data.organizations (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    code VARCHAR(50) NOT NULL,
    name VARCHAR(255) NOT NULL,
    organization_type VARCHAR(50),  -- Company, Division, Department, Branch
    parent_id UUID REFERENCES master_data.organizations(id),
    owner_id UUID,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP
);

CREATE TABLE master_data.divisions (
    id UUID PRIMARY KEY,
    organization_id UUID REFERENCES master_data.organizations(id),
    code VARCHAR(50),
    name VARCHAR(255) NOT NULL,
    manager_id UUID
);

CREATE TABLE master_data.departments (
    id UUID PRIMARY KEY,
    division_id UUID REFERENCES master_data.divisions(id),
    code VARCHAR(50),
    name VARCHAR(255) NOT NULL,
    manager_id UUID,
    cost_center_id UUID
);

CREATE TABLE master_data.cost_centers (
    id UUID PRIMARY KEY,
    organization_id UUID REFERENCES master_data.organizations(id),
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255),
    budget DECIMAL(15,2)
);
```

**Organization Hierarchy**:
```
Company (Organization)
├── Division 1
│   ├── Department 1.1 → Cost Center A
│   └── Department 1.2 → Cost Center B
└── Division 2
    └── Department 2.1 → Cost Center C
```

---

### 4. Party Management
**Purpose**: External entities (Customers, Suppliers, Partners)

```sql
CREATE TABLE master_data.parties (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    party_type VARCHAR(50),  -- Customer, Supplier, Partner, Competitor
    name VARCHAR(255) NOT NULL,
    code VARCHAR(50),
    registration_number VARCHAR(100),
    website VARCHAR(255),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP
);

CREATE TABLE master_data.party_addresses (
    id UUID PRIMARY KEY,
    party_id UUID REFERENCES master_data.parties(id),
    address_type VARCHAR(50),  -- Billing, Shipping, Office
    street_address VARCHAR(255),
    city_id UUID REFERENCES master_data.cities(id),
    postal_code VARCHAR(20),
    primary_address BOOLEAN DEFAULT FALSE
);

CREATE TABLE master_data.party_contacts (
    id UUID PRIMARY KEY,
    party_id UUID REFERENCES master_data.parties(id),
    contact_type VARCHAR(50),  -- Email, Phone, Mobile
    value VARCHAR(255),
    is_primary BOOLEAN DEFAULT FALSE
);

CREATE TABLE master_data.party_classifications (
    id UUID PRIMARY KEY,
    party_id UUID REFERENCES master_data.parties(id),
    classification_type VARCHAR(50),  -- Industry, Size, Rating
    classification_value VARCHAR(100),
    rank INT
);
```

---

### 5. Universal Item (Product Master)
**Purpose**: Product and material master data

```sql
CREATE TABLE master_data.items (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    sku VARCHAR(100) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    category_id UUID REFERENCES master_data.item_categories(id),
    uom_id UUID REFERENCES master_data.units_of_measure(id),
    item_type VARCHAR(50),  -- Finished Good, Raw Material, Component
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP
);

CREATE TABLE master_data.item_categories (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    code VARCHAR(50),
    name VARCHAR(255),
    parent_category_id UUID REFERENCES master_data.item_categories(id),
    level INT  -- 1: Main, 2: Sub, 3: Sub-sub
);

CREATE TABLE master_data.item_variations (
    id UUID PRIMARY KEY,
    item_id UUID REFERENCES master_data.items(id),
    sku_variant VARCHAR(100),
    color VARCHAR(50),
    size VARCHAR(50),
    weight DECIMAL(10,2),
    dimensions_length DECIMAL(10,2),
    dimensions_width DECIMAL(10,2),
    dimensions_height DECIMAL(10,2)
);

CREATE TABLE master_data.item_pricing (
    id UUID PRIMARY KEY,
    item_id UUID REFERENCES master_data.items(id),
    currency_id UUID REFERENCES master_data.currencies(id),
    cost_price DECIMAL(15,4),
    selling_price DECIMAL(15,4),
    margin_percentage DECIMAL(5,2),
    effective_from DATE,
    effective_to DATE
);
```

---

### 6. Apparel Size Specifications
**Purpose**: Size charts and measurements for apparel industry

```sql
CREATE TABLE master_data.size_charts (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(100),
    category VARCHAR(50),  -- T-Shirt, Pants, Shoes, etc.
    size_system VARCHAR(50),  -- XS, S, M, L, XL or Numeric
    is_standard BOOLEAN DEFAULT FALSE
);

CREATE TABLE master_data.sizes (
    id UUID PRIMARY KEY,
    size_chart_id UUID REFERENCES master_data.size_charts(id),
    size_code VARCHAR(10),
    size_label VARCHAR(50),
    sort_order INT
);

CREATE TABLE master_data.measurements (
    id UUID PRIMARY KEY,
    size_id UUID REFERENCES master_data.sizes(id),
    measurement_type VARCHAR(50),  -- Chest, Waist, Inseam, Shoulder
    min_value DECIMAL(10,2),
    max_value DECIMAL(10,2),
    unit_of_measure VARCHAR(10)  -- cm, inches
);
```

---

### 7. Manufacturing Data
**Purpose**: Manufacturing facilities and equipment

```sql
CREATE TABLE master_data.manufacturing_facilities (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255),
    city_id UUID REFERENCES master_data.cities(id),
    facility_type VARCHAR(50),  -- Factory, Warehouse, Assembly
    capacity INT,  -- Daily production capacity
    is_active BOOLEAN DEFAULT TRUE
);

CREATE TABLE master_data.work_centers (
    id UUID PRIMARY KEY,
    facility_id UUID REFERENCES master_data.manufacturing_facilities(id),
    code VARCHAR(50),
    name VARCHAR(255),
    work_center_type VARCHAR(50),  -- Assembly, Testing, Packaging
    capacity INT
);

CREATE TABLE master_data.equipment (
    id UUID PRIMARY KEY,
    work_center_id UUID REFERENCES master_data.work_centers(id),
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255),
    equipment_type VARCHAR(50),
    manufacturer VARCHAR(100),
    model VARCHAR(100),
    purchased_date DATE,
    is_operational BOOLEAN DEFAULT TRUE
);

CREATE TABLE master_data.production_lines (
    id UUID PRIMARY KEY,
    facility_id UUID REFERENCES master_data.manufacturing_facilities(id),
    code VARCHAR(50),
    name VARCHAR(255),
    line_type VARCHAR(50),  -- Assembly, Testing, Packaging
    capacity INT
);
```

---

### 8. Inventory Configuration
**Purpose**: Warehouse and inventory structure

```sql
CREATE TABLE master_data.warehouses (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255),
    city_id UUID REFERENCES master_data.cities(id),
    warehouse_type VARCHAR(50),  -- Central, Regional, Local
    total_capacity INT,
    is_active BOOLEAN DEFAULT TRUE
);

CREATE TABLE master_data.warehouse_zones (
    id UUID PRIMARY KEY,
    warehouse_id UUID REFERENCES master_data.warehouses(id),
    code VARCHAR(50),
    name VARCHAR(100),
    zone_type VARCHAR(50),  -- Raw Materials, Finished Goods, QC
    capacity INT
);

CREATE TABLE master_data.storage_types (
    id UUID PRIMARY KEY,
    code VARCHAR(50),
    name VARCHAR(100),
    description TEXT
);

CREATE TABLE master_data.bin_types (
    id UUID PRIMARY KEY,
    code VARCHAR(50),
    name VARCHAR(100),
    capacity INT,
    storage_type_id UUID REFERENCES master_data.storage_types(id)
);
```

---

### 9. Commercial Terms
**Purpose**: Pricing, payment, and commercial policies

```sql
CREATE TABLE master_data.payment_terms (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(100),
    days INT,  -- Payment due in N days
    discount_percentage DECIMAL(5,2),
    discount_days INT  -- Discount if paid within N days
);

CREATE TABLE master_data.pricing_policies (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(255),
    policy_type VARCHAR(50),  -- Volume, Seasonal, Loyalty
    effective_from DATE,
    effective_to DATE,
    is_active BOOLEAN DEFAULT TRUE
);

CREATE TABLE master_data.discount_schemes (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(255),
    discount_type VARCHAR(50),  -- Percentage, Fixed Amount
    min_quantity INT,
    discount_value DECIMAL(10,2)
);

CREATE TABLE master_data.tax_codes (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    code VARCHAR(20) UNIQUE NOT NULL,
    name VARCHAR(100),
    rate DECIMAL(5,2),
    tax_type VARCHAR(50)  -- GST, VAT, Sales Tax
);
```

---

### 10. Workflow Definitions
**Purpose**: Process and workflow templates

```sql
CREATE TABLE master_data.workflows (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(255),
    workflow_type VARCHAR(50),  -- Approval, Process, Notification
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP
);

CREATE TABLE master_data.workflow_steps (
    id UUID PRIMARY KEY,
    workflow_id UUID REFERENCES master_data.workflows(id),
    step_number INT,
    step_name VARCHAR(100),
    step_type VARCHAR(50),  -- Action, Decision, Wait
    required_approver_count INT,
    timeout_days INT
);

CREATE TABLE master_data.approval_hierarchies (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(255),
    hierarchy_type VARCHAR(50),  -- Manager, Department Head, Finance
    max_approval_amount DECIMAL(15,2)
);
```

---

### 11. HCM Reference Data
**Purpose**: HR and organizational reference data

```sql
CREATE TABLE master_data.job_titles (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    code VARCHAR(50) UNIQUE NOT NULL,
    title VARCHAR(255),
    level VARCHAR(50),  -- Entry, Mid, Senior, Executive
    department_id UUID REFERENCES master_data.departments(id)
);

CREATE TABLE master_data.positions (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    code VARCHAR(50) UNIQUE NOT NULL,
    job_title_id UUID REFERENCES master_data.job_titles(id),
    department_id UUID REFERENCES master_data.departments(id),
    reporting_to_position_id UUID REFERENCES master_data.positions(id),
    status VARCHAR(50),  -- Open, Filled, On Hold
);

CREATE TABLE master_data.skill_sets (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    code VARCHAR(50),
    name VARCHAR(255),
    skill_category VARCHAR(50),  -- Technical, Soft, Domain
    proficiency_level VARCHAR(50)  -- Beginner, Intermediate, Expert
);

CREATE TABLE master_data.competencies (
    id UUID PRIMARY KEY,
    job_title_id UUID REFERENCES master_data.job_titles(id),
    skill_set_id UUID REFERENCES master_data.skill_sets(id),
    required_proficiency_level VARCHAR(50)
);
```

---

## 🔄 Data Synchronization Strategy

### Event-Driven Sync
```json
{
  "eventType": "MasterDataUpdated",
  "domain": "Geography",
  "action": "CREATE",
  "entity": "Country",
  "entityId": "country-123",
  "timestamp": "2024-08-23T10:30:00Z",
  "data": {
    "code": "IN",
    "name": "India",
    "currency": "INR"
  }
}
```

### Cache Strategy
```
Level 1 (DB):        Source of Truth
  ↓ (ETL/Sync)
Level 2 (Redis):     Distributed Cache (TTL: 1-24 hours)
  ↓ (Pub/Sub)
Level 3 (Services):  Local Cache (TTL: 1 hour)
```

### Invalidation Triggers
```csharp
// Immediate invalidation
await cache.InvalidateAsync("master-data:countries");

// Pattern-based invalidation
await cache.InvalidatePatternAsync("master-data:country:*");

// TTL-based (automatic)
// Event-based (RabbitMQ)
```

---

## 🔐 Multi-Tenancy Model

### Row-Level Security (RLS)
```sql
-- Add tenant_id to all tables
ALTER TABLE master_data.items ADD COLUMN tenant_id UUID NOT NULL;
ALTER TABLE master_data.warehouses ADD COLUMN tenant_id UUID NOT NULL;

-- Create RLS policy
CREATE POLICY tenant_isolation ON master_data.items
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

-- Set tenant context
SET app.current_tenant_id = '550e8400-e29b-41d4-a716-446655440000';
```

### Query Filtering
```csharp
public IQueryable<Item> GetItems(Guid tenantId)
{
    return _context.Items
        .Where(i => i.TenantId == tenantId)
        .AsNoTracking();
}
```

---

## 📈 Performance Optimization

### Indexing Strategy
```sql
-- Composite indexes
CREATE INDEX idx_items_tenant_sku ON master_data.items(tenant_id, sku);
CREATE INDEX idx_organizations_tenant_code ON master_data.organizations(tenant_id, code);
CREATE INDEX idx_parties_tenant_type ON master_data.parties(tenant_id, party_type);

-- Foreign key indexes
CREATE INDEX idx_items_category ON master_data.items(category_id);
CREATE INDEX idx_party_addresses_party ON master_data.party_addresses(party_id);
```

### Query Optimization
```csharp
public async Task<Country> GetCountryWithStatesAsync(string countryCode)
{
    return await _context.Countries
        .Include(c => c.States)
        .ThenInclude(s => s.Cities)
        .FirstOrDefaultAsync(c => c.Code == countryCode);
}
```

---

## 🔗 API Endpoints Summary

| Domain | Endpoints |
|--------|----------|
| Core Reference | GET/POST languages, currencies, UOM |
| Geography | GET countries/{id}/states, cities |
| Organization | GET/POST organizations, divisions, departments |
| Party | GET/POST parties, addresses, contacts |
| Item | GET/POST items, categories, variations |
| Apparel Size | GET size-charts, measurements |
| Manufacturing | GET facilities, equipment, production-lines |
| Inventory | GET warehouses, zones, storage-types |
| Commercial | GET payment-terms, pricing, tax-codes |
| Workflow | GET/POST workflows, approval-hierarchies |
| HCM Ref | GET job-titles, positions, skill-sets |

---

## 📊 Data Volume Estimates

```
Languages:          ~200 records
Currencies:         ~180 records
Countries:          ~195 records
States:             ~3,000 records
Cities:             ~50,000 records (cached)
Organizations:      ~1,000 records (tenant-specific)
Parties:            ~10,000 records (tenant-specific)
Items:              ~100,000 records (tenant-specific)
Warehouses:         ~100 records
Job Titles:         ~500 records
```

---

## ✅ Implementation Checklist

- [ ] Create database schema for all 11 domains
- [ ] Implement entity models
- [ ] Create repositories for each domain
- [ ] Implement caching layer
- [ ] Set up event publishing
- [ ] Implement multi-tenancy filters
- [ ] Create API endpoints
- [ ] Add Swagger/OpenAPI documentation
- [ ] Write unit & integration tests
- [ ] Set up monitoring & alerting
- [ ] Document data sync procedures
- [ ] Create sample data scripts
- [ ] Implement data validation
- [ ] Add audit logging
- [ ] Optimize queries & indexes

---

**Next**: Phase 4 - Business Services Architecture
