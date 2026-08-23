# WeavoGo Platform - Monorepo Structure

## 📁 Directory Organization

```
weavogo-platform/
├── README.md (main)
├── .gitignore
├── .env.example
├── docker-compose.yml
├── package.json (root workspace)
│
├── services/
│   ├── api-gateway/               # YARP API Gateway
│   │   ├── src/
│   │   ├── tests/
│   │   ├── Dockerfile
│   │   └── docker-compose.yml
│   │
│   ├── auth-security/              # Auth & Security (7 sub-services)
│   │   ├── identity-service/
│   │   ├── authentication-service/
│   │   ├── authorization-service/
│   │   ├── organization-access-service/
│   │   ├── session-token-service/
│   │   ├── security-audit-service/
│   │   ├── policy-service/
│   │   ├── shared/
│   │   ├── Dockerfile
│   │   └── docker-compose.yml
│   │
│   ├── master-data/                 # Master Data Platform
│   │   ├── src/
│   │   │   ├── MasterData.API/
│   │   │   ├── MasterData.Application/
│   │   │   ├── MasterData.Domain/
│   │   │   └── MasterData.Infrastructure/
│   │   ├── tests/
│   │   ├── Dockerfile
│   │   └── docker-compose.yml
│   │
│   ├── hcm/                         # WeavoHCM
│   │   ├── src/
│   │   ├── tests/
│   │   ├── Dockerfile
│   │   └── docker-compose.yml
│   │
│   ├── plm/                         # WeavoPLM
│   │   ├── src/
│   │   ├── tests/
│   │   ├── Dockerfile
│   │   └── docker-compose.yml
│   │
│   ├── erp/                         # WeavoERP
│   │   ├── src/
│   │   ├── tests/
│   │   ├── Dockerfile
│   │   └── docker-compose.yml
│   │
│   └── mes/                         # WeavoMES
│       ├── src/
│       ├── tests/
│       ├── Dockerfile
│       └── docker-compose.yml
│
├── shared/
│   ├── libs/                        # Shared libraries
│   │   ├── WeavoGo.Common/
│   │   ├── WeavoGo.Security/
│   │   ├── WeavoGo.Data/
│   │   ├── WeavoGo.Cache/
│   │   ├── WeavoGo.Messaging/
│   │   └── WeavoGo.Logging/
│   │
│   └── models/                      # Shared DTOs and entities
│       ├── WeavoGo.Models/
│       └── WeavoGo.Enums/
│
├── infrastructure/
│   ├── docker/                      # Docker configurations
│   ├── kubernetes/                  # K8s manifests
│   ├── terraform/                   # IaC for cloud
│   ├── helm/                        # Helm charts
│   ├── scripts/
│   │   ├── init-db.sql
│   │   ├── setup-dev-env.sh
│   │   └── deploy.sh
│   └── monitoring/
│       ├── prometheus/
│       ├── grafana/
│       └── jaeger/
│
├── docs/
│   ├── ARCHITECTURE.md              # System design
│   ├── API.md                       # API documentation
│   ├── DEVELOPMENT.md               # Dev setup guide
│   ├── DEPLOYMENT.md                # Deployment guide
│   ├── SECURITY.md                  # Security policies
│   ├── DATABASE.md                  # Database schema
│   ├── api-specs/
│   │   ├── auth-service.openapi.yml
│   │   ├── master-data.openapi.yml
│   │   └── ...
│   └── diagrams/
│
├── tests/
│   ├── integration/                 # Integration tests
│   ├── e2e/                         # End-to-end tests
│   └── load/                        # Load tests
│
├── tools/
│   ├── postman/                     # Postman collections
│   ├── scripts/
│   └── cli/
│
├── docker-compose.yml               # Main orchestration
├── docker-compose.prod.yml          # Production setup
├── docker-compose.test.yml          # Testing setup
├── Dockerfile.base                  # Base image
├── .github/
│   ├── workflows/
│   │   ├── ci.yml
│   │   ├── cd.yml
│   │   └── security-scan.yml
│   └── ISSUE_TEMPLATE/
├── CONTRIBUTING.md
├── LICENSE
└── .editorconfig
```

## 🎯 Monorepo Benefits

✅ **Single Repository**: All code in one place
✅ **Shared Dependencies**: DRY principle for common libraries
✅ **Atomic Commits**: Related changes in one commit
✅ **Easy Navigation**: All services visible
✅ **Consistent Standards**: Unified code quality rules
✅ **Simplified CI/CD**: Single workflow for all services
✅ **Code Reuse**: Share models, utilities, configurations

## 🚀 Getting Started

### Clone the Monorepo
```bash
git clone https://github.com/rahajulaminshuvo/weavogo-platform.git
cd weavogo-platform
```

### Setup Development Environment
```bash
# Copy environment file
cp .env.example .env

# Start all services
docker-compose up

# Or specific service
docker-compose up api-gateway
```

## 📦 Service Structure

Each service follows Clean Architecture:

```
services/{service}/
├── src/
│   ├── {Service}.API/               # REST endpoints
│   ├── {Service}.Application/       # Business logic
│   ├── {Service}.Domain/            # Core entities
│   └── {Service}.Infrastructure/    # Data access
├── tests/
│   ├── {Service}.API.Tests/
│   ├── {Service}.Application.Tests/
│   └── {Service}.Infrastructure.Tests/
├── Dockerfile
├── docker-compose.yml
└── README.md
```

## 🔄 Inter-Service Communication

### Synchronous (REST/gRPC)
- API Gateway routes to services
- Services communicate via APIs
- Consul for service discovery

### Asynchronous (Message Queue)
- RabbitMQ for event publishing
- Services subscribe to events
- Ensures loose coupling

## 🧪 Testing Strategy

```
tests/
├── integration/          # Test service interactions
├── e2e/                  # Full workflow testing
└── load/                 # Performance testing
```

## 📚 Documentation Structure

```
docs/
├── ARCHITECTURE.md       # System design & diagrams
├── API.md               # REST API documentation
├── DEVELOPMENT.md       # Local setup guide
├── DEPLOYMENT.md        # Production deployment
├── SECURITY.md          # Security policies
├── DATABASE.md          # Schema & migrations
└── api-specs/           # OpenAPI specifications
```

## 🔐 Security Considerations

- **Secrets Management**: Use .env files (not in git)
- **Network Isolation**: Docker networks per environment
- **Access Control**: RBAC via auth service
- **Audit Logging**: All operations logged
- **Data Encryption**: At rest and in transit

## 📊 Monitoring & Logging

```
infrastructure/monitoring/
├── prometheus/          # Metrics collection
├── grafana/             # Visualization
├── jaeger/              # Distributed tracing
└── elk/                 # Centralized logging
```

## 🛠️ Development Workflow

1. **Create Feature Branch**: `git checkout -b feature/FEATURE-NAME`
2. **Make Changes**: Across services as needed
3. **Write Tests**: Unit & integration tests
4. **Run CI**: GitHub Actions validates all services
5. **Create PR**: With clear description
6. **Review & Merge**: Atomic commit across services

## 📝 Commit Convention

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Examples:**
```
feat(auth): implement OAuth 2.0 integration
fix(master-data): resolve cache synchronization bug
docs(api): update OpenAPI specifications
chore(infra): update Docker base images
```

## 🚀 CI/CD Pipeline

GitHub Actions automatically:
1. ✅ Runs tests for affected services
2. ✅ Builds Docker images
3. ✅ Runs security scans
4. ✅ Deploys to staging/production

## 📦 Dependencies

- **.NET 8.0** - Backend framework
- **PostgreSQL 15** - Primary database
- **Redis 7** - Caching
- **RabbitMQ 3.12** - Message queue
- **Consul** - Service discovery
- **YARP** - API Gateway
- **Docker** - Containerization
- **Kubernetes** - Orchestration (production)

## 🤝 Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md) for:
- Code standards
- Git workflow
- PR process
- Testing requirements

## 📄 License

MIT License - see [LICENSE](./LICENSE)

## 🗺️ Roadmap

**Q3 2024** - Phase 1: Core Infrastructure & Auth
**Q4 2024** - Phase 2: Master Data Platform
**Q1 2025** - Phase 3: Business Services MVP
**Q2 2025** - Phase 4: Advanced Features

---

**Last Updated**: August 2024 | **Version**: 1.0.0-alpha
