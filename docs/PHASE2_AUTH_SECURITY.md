# Phase 2: Auth & Security Platform Design

## 🔐 Overview

The Auth & Security Platform is the foundation of WeavoGo, providing:
- Centralized authentication and authorization
- Multi-tenant organization management
- Session and token management
- Security audit and compliance
- Policy enforcement

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    API Gateway (YARP)                        │
└─────────────────────────────┬───────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
        ┌─────▼──────┐  ┌────▼──────┐  ┌────▼─────────┐
        │  Identity  │  │  AuthN    │  │ SessionToken │
        │  Service   │  │  Service  │  │  Service     │
        └─────┬──────┘  └────┬──────┘  └────┬─────────┘
              │               │               │
        ┌─────▼──────┐  ┌────▼──────┐  ┌────▼─────────┐
        │  AuthZ     │  │OrgAccess  │  │    Audit     │
        │  Service   │  │  Service  │  │   Service    │
        └─────┬──────┘  └────┬──────┘  └────┬─────────┘
              │               │               │
              └───────────────┼───────────────┘
                              │
                    ┌─────────▼─────────┐
                    │  Policy Service   │
                    └─────────┬─────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
    ┌───▼────┐          ┌────▼───┐          ┌──────▼──┐
    │PostgreSQL         │ Redis  │          │RabbitMQ│
    │  (Auth DB)        │(Cache) │          │(Events)│
    └────────┘          └────────┘          └────────┘
```

## 🔑 Seven Core Services

### 1. Identity Service (Port 5001)
**Purpose**: User account and profile management

**Responsibilities**:
- User account creation and lifecycle
- Profile information management
- Email verification
- Account status management
- Personal data management (GDPR compliant)

**Database Tables**:
```sql
users (id, email, username, first_name, last_name, phone, created_at)
user_profiles (user_id, bio, avatar_url, preferences)
user_preferences (user_id, theme, language, notifications)
```

**Key Endpoints**:
```bash
POST   /api/identity/users                    # Create user
GET    /api/identity/users/{id}               # Get user
PUT    /api/identity/users/{id}               # Update user
DELETE /api/identity/users/{id}               # Delete user
GET    /api/identity/users/{id}/profile       # Get profile
PUT    /api/identity/users/{id}/profile       # Update profile
POST   /api/identity/users/{id}/email/verify  # Verify email
POST   /api/identity/users/search             # Search users
```

**Data Model**:
```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string PhoneNumber { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public UserProfile Profile { get; set; }
    public ICollection<UserPreference> Preferences { get; set; }
}
```

---

### 2. Authentication Service (Port 5001)
**Purpose**: Authentication and login management

**Responsibilities**:
- Local authentication (email/password)
- OAuth 2.0 integration (Google, GitHub, Azure AD)
- Multi-factor authentication (TOTP, SMS, Email)
- Password management and reset
- Session initialization

**Supported Auth Methods**:
```
1. Email/Password (Local)
2. OAuth 2.0 (Social)
   - Google
   - GitHub
   - Microsoft Azure AD
   - Custom OIDC providers
3. Multi-Factor Authentication
   - TOTP (Time-based OTP)
   - Email OTP
   - SMS OTP
   - Backup Codes
```

**Key Endpoints**:
```bash
POST   /api/auth/register              # Register new user
POST   /api/auth/login                 # Login with credentials
POST   /api/auth/logout                # Logout
POST   /api/auth/refresh-token         # Refresh JWT
POST   /api/auth/password/reset        # Request password reset
POST   /api/auth/password/reset-confirm # Confirm password reset
POST   /api/auth/mfa/setup             # Setup MFA
POST   /api/auth/mfa/verify            # Verify MFA
GET    /api/auth/oauth/{provider}      # OAuth callback
POST   /api/auth/token/validate        # Validate token
```

**Login Flow**:
```
1. User submits email/password
2. Verify against bcrypt hash
3. Check if MFA enabled
   - If yes: Send MFA challenge
   - If no: Generate JWT tokens
4. Create session
5. Return tokens + session info
6. API Gateway validates JWT on subsequent requests
```

**MFA Flow**:
```
1. After successful password verification
2. Send MFA challenge (email/SMS/TOTP)
3. User responds with MFA code
4. Verify TOTP/OTP code
5. If valid: Generate JWT + refresh token
6. If invalid: Log failed attempt, return error
```

**Password Policy**:
```
Minimum Length: 8 characters
Must include:
  - Uppercase (A-Z)
  - Lowercase (a-z)
  - Numbers (0-9)
  - Special chars (!@#$%^&*)
Expiration: 90 days
History: Last 5 passwords blocked
Lockout: 5 failed attempts = 15 min lockout
```

---

### 3. Authorization Service (Port 5001)
**Purpose**: Access control and permissions management

**Responsibilities**:
- Role-Based Access Control (RBAC)
- Attribute-Based Access Control (ABAC)
- Permission management
- Resource-level access control
- Delegation support

**RBAC Model**:
```
User → Role(s) → Permission(s) → Resource Action

Example:
- Admin Role
  ├── user.create
  ├── user.read
  ├── user.update
  ├── user.delete
  ├── role.manage
  └── organization.manage
```

**Database Tables**:
```sql
roles (id, name, description, is_system_role, created_at)
permissions (id, name, resource, action, description)
role_permissions (role_id, permission_id)
user_roles (user_id, role_id, organization_id)
```

**Key Endpoints**:
```bash
GET    /api/authorization/roles                    # List roles
POST   /api/authorization/roles                    # Create role
GET    /api/authorization/roles/{id}               # Get role
PUT    /api/authorization/roles/{id}               # Update role
DELETE /api/authorization/roles/{id}               # Delete role

GET    /api/authorization/permissions              # List permissions
POST   /api/authorization/permissions              # Create permission

POST   /api/authorization/assign-role              # Assign role to user
DELETE /api/authorization/revoke-role              # Revoke role

GET    /api/authorization/check/{userId}           # Check permissions
POST   /api/authorization/check                    # Check resource access
```

**Permission Checking**:
```csharp
// User can perform action?
await authzService.IsAuthorizedAsync(
    userId: user.Id,
    resource: "article",
    action: "delete",
    resourceId: "article-123"
);

// Returns: true/false based on:
// 1. User roles
// 2. Role permissions
// 3. Resource policies
// 4. Ownership (if user owns resource)
```

---

### 4. Organization Access Service (Port 5001)
**Purpose**: Multi-tenant organization management

**Responsibilities**:
- Organization creation and management
- Multi-tenant data isolation
- Organization hierarchy (parent/child)
- Member management and roles
- Department and team structure
- Access level control

**Multi-Tenancy Model**:
```
Organization (Company)
├── Division 1
│   ├── Department 1.1
│   │   └── Team 1.1.1
│   └── Department 1.2
└── Division 2
    └── Department 2.1
```

**Database Tables**:
```sql
organizations (id, name, code, owner_id, parent_id, tenant_id)
organization_members (user_id, organization_id, role_id)
organization_teams (id, organization_id, name, manager_id)
team_members (user_id, team_id)
```

**Key Endpoints**:
```bash
POST   /api/organizations                         # Create org
GET    /api/organizations/{id}                    # Get org
PUT    /api/organizations/{id}                    # Update org
DELETE /api/organizations/{id}                    # Delete org

GET    /api/organizations/{id}/members            # List members
POST   /api/organizations/{id}/members            # Add member
DELETE /api/organizations/{id}/members/{userId}   # Remove member
PUT    /api/organizations/{id}/members/{userId}/role  # Update member role

GET    /api/organizations/{id}/structure          # Get hierarchy
POST   /api/organizations/{id}/teams              # Create team
```

**Tenant Isolation**:
```
Every query automatically filtered by tenant_id:

SELECT * FROM users
WHERE tenant_id = @currentTenantId

This ensures:
✓ Data security
✓ Complete isolation
✓ Compliance with regulations
```

---

### 5. Session Token Service (Port 5001)
**Purpose**: JWT token generation and management

**Responsibilities**:
- JWT token generation
- Token validation and verification
- Refresh token handling
- Token revocation/blacklisting
- Claims extraction
- Token expiration management

**JWT Structure**:
```
Header:
{
  "alg": "HS256",
  "typ": "JWT"
}

Payload:
{
  "sub": "user-123",
  "email": "user@example.com",
  "roles": ["admin", "user"],
  "org_id": "org-456",
  "iat": 1692806200,
  "exp": 1692810200,
  "iss": "weavogo.auth",
  "aud": "weavogo-platform"
}

Signature:
HS256(Base64Url(Header) + "." + Base64Url(Payload), secret)
```

**Token Configuration**:
```json
{
  "Jwt": {
    "Secret": "very-secret-key-256-bits-minimum",
    "Issuer": "https://weavogo.auth",
    "Audience": "weavogo-platform",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7,
    "Algorithm": "HS256",
    "ValidateIssuer": true,
    "ValidateAudience": true,
    "ValidateLifetime": true
  }
}
```

**Key Endpoints**:
```bash
POST   /api/tokens/generate              # Generate JWT pair
POST   /api/tokens/validate              # Validate token
POST   /api/tokens/refresh               # Refresh access token
POST   /api/tokens/revoke                # Revoke token
GET    /api/tokens/claims/{token}        # Extract claims
GET    /api/tokens/blacklist             # Check if blacklisted
```

**Token Lifecycle**:
```
1. Generate Access + Refresh Token
   ├── Access Token (1 hour)
   └── Refresh Token (7 days)

2. Store Refresh Token
   ├── Database
   ├── With expiration
   └── With usage tracking

3. Client stores tokens
   ├── Access Token: Memory/SessionStorage
   └── Refresh Token: HttpOnly Cookie

4. When Access Token expires
   ├── Client uses Refresh Token
   ├── Service validates Refresh Token
   └── Generate new Access Token pair

5. Logout/Revocation
   ├── Add tokens to blacklist
   ├── Delete Refresh Token from DB
   └── Clear client storage
```

---

### 6. Security Audit Service (Port 5001)
**Purpose**: Security event logging and compliance

**Responsibilities**:
- Security event logging
- Audit trail maintenance
- Compliance reporting
- Anomaly detection
- Data retention policies
- Regulatory compliance (GDPR, SOC2, ISO 27001)

**Audit Events**:
```
Authentication Events:
  - user.login_success
  - user.login_failed
  - user.logout
  - user.token_refresh
  - user.mfa_setup
  - user.password_change

Authorization Events:
  - role.assigned
  - role.revoked
  - permission.granted
  - permission.denied
  - access.check

Resource Events:
  - resource.created
  - resource.updated
  - resource.deleted
  - resource.accessed

System Events:
  - config.changed
  - policy.updated
  - security.alert
```

**Database Tables**:
```sql
audit_logs (
    id, user_id, event_type, resource_type, resource_id,
    status, ip_address, user_agent, details, created_at
)

audit_log_details (
    audit_log_id, key, value
)
```

**Key Endpoints**:
```bash
GET    /api/audit/logs                   # List audit logs
GET    /api/audit/logs/{id}              # Get audit log
GET    /api/audit/logs/search            # Search logs

GET    /api/audit/reports/login          # Login report
GET    /api/audit/reports/access         # Access report
GET    /api/audit/reports/compliance     # Compliance report
GET    /api/audit/reports/anomalies      # Anomaly detection

GET    /api/audit/timeline/{userId}      # User activity timeline
```

**Compliance Reports**:
```
GDPR Report:
  - User data access
  - Data modifications
  - Deletions
  - Exports

SOC2 Report:
  - Authentication events
  - Access changes
  - Security incidents
  - Change management

ISO 27001 Report:
  - Security events
  - Incident response
  - Vulnerability management
```

---

### 7. Policy Service (Port 5001)
**Purpose**: Policy definition and enforcement

**Responsibilities**:
- Policy creation and management
- Conditional access policies
- Risk-based access
- Resource policies
- API rate limit policies
- Password policies

**Policy Types**:
```
1. Conditional Access Policies
   - User location
   - Device type
   - Time-based access
   - IP whitelist/blacklist

2. Resource Policies
   - Resource-level access control
   - API method restrictions
   - Data classification

3. Organizational Policies
   - Password policies
   - MFA requirements
   - Session timeout
   - API rate limits

4. Risk Policies
   - Impossible travel detection
   - Suspicious login detection
   - Brute force protection
```

**Policy Engine**:
```json
{
  "id": "policy-123",
  "name": "Block Access from Unknown Locations",
  "enabled": true,
  "conditions": [
    {
      "type": "location",
      "operator": "notIn",
      "values": ["US", "CA", "UK"]
    },
    {
      "type": "risk_level",
      "operator": "greaterThan",
      "value": 50
    }
  ],
  "actions": [
    {
      "type": "requireMFA"
    },
    {
      "type": "alertUser",
      "message": "Unusual activity detected"
    }
  ]
}
```

**Key Endpoints**:
```bash
GET    /api/policies                      # List policies
POST   /api/policies                      # Create policy
GET    /api/policies/{id}                 # Get policy
PUT    /api/policies/{id}                 # Update policy
DELETE /api/policies/{id}                 # Delete policy

POST   /api/policies/evaluate             # Evaluate policy
POST   /api/policies/apply                # Apply policy

GET    /api/policies/templates            # Policy templates
```

---

## 🔐 Security Architecture

### Defense in Depth
```
Layer 1: Authentication
  ├── Strong password requirements
  ├── Multi-factor authentication
  └── Account lockout after failed attempts

Layer 2: Authorization
  ├── RBAC/ABAC checks
  ├── Resource-level permissions
  └── Policy evaluation

Layer 3: Encryption
  ├── Passwords: bcrypt (salt rounds: 10)
  ├── Data at rest: AES-256
  └── Data in transit: TLS 1.3

Layer 4: Audit & Monitoring
  ├── All security events logged
  ├── Real-time anomaly detection
  ├── Compliance reporting
  └── Alert notifications
```

### Token Security
```
┌─────────────────────────────────────┐
│     Access Token (JWT)              │
├─────────────────────────────────────┤
│ • Short-lived (1 hour)              │
│ • Signed with HS256                 │
│ • Stored in memory                  │
│ • Sent in Authorization header      │
│ • Validated on each API request     │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│     Refresh Token                   │
├─────────────────────────────────────┤
│ • Long-lived (7 days)               │
│ • Stored in HttpOnly cookie         │
│ • Stored in database                │
│ • Never sent in API headers         │
│ • Rotation on each refresh          │
│ • Can be revoked                    │
└─────────────────────────────────────┘
```

---

## 📊 Data Models

### Core Domain Entities

**User**
```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string PhoneNumber { get; set; }
    public bool EmailVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties
    public ICollection<Role> Roles { get; set; }
    public ICollection<Organization> Organizations { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; }
}
```

**Role**
```csharp
public class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsSystemRole { get; set; }
    public Guid? OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public ICollection<User> Users { get; set; }
    public ICollection<Permission> Permissions { get; set; }
}
```

**Permission**
```csharp
public class Permission
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Resource { get; set; }
    public string Action { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public ICollection<Role> Roles { get; set; }
}
```

**Organization**
```csharp
public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public User Owner { get; set; }
    public Organization Parent { get; set; }
    public ICollection<Organization> Children { get; set; }
    public ICollection<User> Members { get; set; }
}
```

---

## 🔄 Communication Patterns

### Synchronous (Request-Response)
```
┌─────────────┐        REST/gRPC         ┌──────────────────┐
│   API Gw    │◄──────────────────────►  │ Auth Service     │
└─────────────┘                          └──────────────────┘
      ▲
      │ Validate JWT
      │ Check permissions
      │
   Client
```

### Asynchronous (Event-Driven)
```
Auth Service              RabbitMQ              Other Services
     │                        │                        │
     ├─ user.login ──────────►│                        │
     │                        ├──────────────────────►│
     │                        │  UserLoggedIn event   │ Update cache
     │                        │                        │ Send notification
     │
     ├─ role.assigned ───────►│                        │
     │                        ├──────────────────────►│
     │                        │  RoleAssigned event   │ Update permissions
```

---

## 🚀 Deployment

### Docker Configuration
```yaml
services:
  auth-service:
    build: .
    ports:
      - "5001:5001"
    environment:
      - ConnectionStrings__DefaultConnection=postgres://...
      - Jwt__Secret=${JWT_SECRET}
      - OAuth__Google__ClientId=${GOOGLE_CLIENT_ID}
      - Redis__Host=redis:6379
      - RabbitMQ__Host=rabbitmq:5672
    depends_on:
      - postgres
      - redis
      - rabbitmq
```

### Kubernetes Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: weavogo-auth-service
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: auth-service
        image: weavogo-auth-service:1.0.0
        ports:
        - containerPort: 5001
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: auth-secrets
              key: postgres-connection
```

---

## 📈 Performance & Scalability

### Caching Strategy
```
Redis Cache Layers:
  ├── User sessions (TTL: 1 hour)
  ├── Role permissions (TTL: 1 hour)
  ├── Organization structure (TTL: 24 hours)
  └── Policy definitions (TTL: 1 hour)

Cache Invalidation:
  ├── On-demand (immediate)
  ├── TTL-based (time)
  └── Event-based (RabbitMQ)
```

### Database Optimization
```
Indexes:
  ├── UNIQUE INDEX: users(email)
  ├── UNIQUE INDEX: users(username)
  ├── INDEX: user_roles(user_id)
  ├── INDEX: user_roles(role_id)
  ├── INDEX: role_permissions(role_id)
  ├── INDEX: audit_logs(user_id, created_at)
  └── INDEX: audit_logs(event_type, created_at)

Queries:
  ├── Eager loading for relationships
  ├── Pagination for large result sets
  └── Query optimization using execution plans
```

---

## ✅ Security Checklist

- [ ] Passwords hashed with bcrypt (min 10 rounds)
- [ ] JWTs signed with strong secret (256+ bits)
- [ ] TLS 1.3 for all data in transit
- [ ] AES-256 for sensitive data at rest
- [ ] HTTPS redirect on API Gateway
- [ ] CORS properly configured
- [ ] CSRF protection enabled
- [ ] SQL injection prevention (parameterized queries)
- [ ] XSS prevention (input validation)
- [ ] Rate limiting on auth endpoints
- [ ] Account lockout after failed attempts
- [ ] Session timeout configured
- [ ] Audit logging enabled
- [ ] Error messages don't leak sensitive info
- [ ] Secrets managed via environment variables
- [ ] Regular security audits scheduled
- [ ] Penetration testing completed
- [ ] OWASP Top 10 addressed

---

## 📚 References

- [OWASP Authentication](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [OWASP Authorization](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)
- [NIST 800-63B](https://pages.nist.gov/800-63-3/sp800-63b.html)
- [OAuth 2.0](https://tools.ietf.org/html/rfc6749)
- [OpenID Connect](https://openid.net/connect/)

---

**Next**: Phase 3 - Master Data Platform Design
