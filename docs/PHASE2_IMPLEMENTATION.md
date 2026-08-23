# Auth & Security Service - Implementation Guide

## 🛠️ Service Implementation Templates

Each of the 7 Auth services follows this structure:

```
service-name/
├── src/
│   ├── {Service}.API/
│   │   ├── Controllers/
│   │   ├── Middleware/
│   │   ├── Program.cs
│   │   └── appsettings.json
│   ├── {Service}.Application/
│   │   ├── Services/
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   └── MappingProfiles/
│   ├── {Service}.Domain/
│   │   ├── Entities/
│   │   ├── Interfaces/
│   │   └── ValueObjects/
│   └── {Service}.Infrastructure/
│       ├── Data/
│       ├── Repositories/
│       └── ExternalServices/
├── tests/
│   ├── {Service}.API.Tests/
│   ├── {Service}.Application.Tests/
│   └── {Service}.Infrastructure.Tests/
└── docker-compose.yml
```

## 🔐 Shared Components

### Shared Middleware
```csharp
// Authentication Middleware
public class AuthenticationMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
    {
        var token = ExtractToken(context.Request.Headers);
        if (!string.IsNullOrEmpty(token))
        {
            context.User = await tokenService.ValidateAndGetClaimsAsync(token);
        }
        await _next(context);
    }
}

// Authorization Middleware
public class AuthorizationMiddleware
{
    public async Task InvokeAsync(HttpContext context, IAuthorizationService authService)
    {
        var (isAuthorized, reason) = await authService.IsAuthorizedAsync(
            context.User,
            context.Request.Method,
            context.Request.Path
        );
        
        if (!isAuthorized)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = reason });
        }
        
        await _next(context);
    }
}

// Audit Middleware
public class AuditMiddleware
{
    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        var startTime = DateTime.UtcNow;
        await _next(context);
        
        await auditService.LogAsync(new AuditLog
        {
            UserId = context.User?.FindFirst("sub")?.Value,
            Action = $"{context.Request.Method} {context.Request.Path}",
            Status = context.Response.StatusCode.ToString(),
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers["User-Agent"],
            CreatedAt = startTime
        });
    }
}
```

## 🏗️ Base Service Implementation

```csharp
// Generic base service
public abstract class BaseAuthService
{
    protected readonly ILogger Logger;
    protected readonly ICache Cache;
    protected readonly IMessageBus MessageBus;
    protected readonly IAuditService AuditService;
    protected readonly IUnitOfWork UnitOfWork;
    
    public async Task PublishEventAsync<TEvent>(TEvent @event) where TEvent : DomainEvent
    {
        await MessageBus.PublishAsync(@event);
        await AuditService.LogEventAsync(@event);
    }
    
    protected async Task<T> GetFromCacheOrDbAsync<T>(string cacheKey, Func<Task<T>> dbQuery)
    {
        var cached = await Cache.GetAsync<T>(cacheKey);
        if (cached != null) return cached;
        
        var data = await dbQuery();
        await Cache.SetAsync(cacheKey, data, TimeSpan.FromHours(1));
        return data;
    }
    
    protected void InvalidateCache(string pattern)
    {
        Cache.InvalidateByPattern(pattern);
    }
}
```

## 🔄 Integration Points

### Service-to-Service Communication
```csharp
// Call another service
public class AuthenticationService
{
    private readonly IIdentityServiceClient _identityService;
    private readonly IOrganizationAccessServiceClient _orgAccessService;
    
    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        // 1. Verify with Identity Service
        var user = await _identityService.GetUserByEmailAsync(request.Email);
        if (user == null) throw new UnauthorizedAccessException();
        
        // 2. Check organization access
        var orgAccess = await _orgAccessService.CheckAccessAsync(user.Id);
        if (!orgAccess.IsAuthorized) throw new ForbiddenAccessException();
        
        // 3. Generate tokens
        var tokens = await _tokenService.GenerateTokensAsync(user);
        
        return new LoginResult { AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken };
    }
}
```

### Event Publishing
```csharp
// Domain events published to RabbitMQ
public class UserAuthenticatedEvent : DomainEvent
{
    public Guid UserId { get; set; }
    public DateTime LoginTime { get; set; }
    public string IpAddress { get; set; }
}

// Publish in service
await PublishEventAsync(new UserAuthenticatedEvent
{
    UserId = user.Id,
    LoginTime = DateTime.UtcNow,
    IpAddress = context.Connection.RemoteIpAddress?.ToString()
});

// Subscribe in other services
public class LoginEventHandler : IEventHandler<UserAuthenticatedEvent>
{
    public async Task HandleAsync(UserAuthenticatedEvent @event)
    {
        // Update cache, send notifications, etc.
        await cache.SetAsync($"user-login:{@event.UserId}", @event.LoginTime);
    }
}
```

## 💾 Database Initialization

```sql
-- Create schemas
CREATE SCHEMA IF NOT EXISTS auth;
CREATE SCHEMA IF NOT EXISTS audit;

-- Create base tables
CREATE TABLE auth.users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) UNIQUE NOT NULL,
    username VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    phone VARCHAR(20),
    email_verified BOOLEAN DEFAULT FALSE,
    is_active BOOLEAN DEFAULT TRUE,
    last_login_at TIMESTAMP NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes
CREATE INDEX idx_users_email ON auth.users(email);
CREATE INDEX idx_users_username ON auth.users(username);
CREATE INDEX idx_users_email_verified ON auth.users(email_verified);
CREATE INDEX idx_users_is_active ON auth.users(is_active);

-- Create audit table
CREATE TABLE audit.audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES auth.users(id) ON DELETE SET NULL,
    action VARCHAR(100),
    resource_type VARCHAR(100),
    resource_id VARCHAR(255),
    status VARCHAR(50),
    ip_address INET,
    user_agent TEXT,
    details JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_logs_user_id ON audit.audit_logs(user_id);
CREATE INDEX idx_audit_logs_created_at ON audit.audit_logs(created_at DESC);
CREATE INDEX idx_audit_logs_event_type ON audit.audit_logs(action, created_at DESC);
```

## 🧪 Testing Strategy

```csharp
// Unit Test Example
public class UserServiceTests
{
    [Fact]
    public async Task CreateUserAsync_WithValidData_ShouldCreateUser()
    {
        // Arrange
        var mockRepository = new Mock<IUserRepository>();
        var mockLogger = new Mock<ILogger<UserService>>();
        var service = new UserService(mockRepository.Object, mockLogger.Object);
        
        var createDto = new CreateUserDto
        {
            Email = "test@example.com",
            Username = "testuser",
            Password = "SecurePass123!",
            FirstName = "Test",
            LastName = "User"
        };
        
        // Act
        var result = await service.CreateUserAsync(createDto);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.Email);
        mockRepository.Verify(x => x.AddAsync(It.IsAny<User>()), Times.Once);
    }
}

// Integration Test Example
public class AuthenticationServiceIntegrationTests : IAsyncLifetime
{
    private IContainer _container;
    private IAuthenticationService _authService;
    
    public async Task InitializeAsync()
    {
        _container = new TestContainer();
        _container.RegisterDependencies();
        _authService = _container.Resolve<IAuthenticationService>();
    }
    
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnTokens()
    {
        // Setup test data
        var user = new User { Email = "user@test.com", PasswordHash = BCrypt.HashPassword("Password123!") };
        await _container.Resolve<IUserRepository>().AddAsync(user);
        
        // Act
        var result = await _authService.LoginAsync(new LoginRequest
        {
            Email = "user@test.com",
            Password = "Password123!"
        });
        
        // Assert
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
    }
}
```

---

**Next Steps**:
1. Review individual service implementations
2. Set up database migrations
3. Configure OAuth providers
4. Implement audit logging
5. Add monitoring and alerts
