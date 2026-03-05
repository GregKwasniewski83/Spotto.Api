# 🔄 Development Workflow

## 🚀 Getting Started

### Prerequisites
Before you begin development on PlaySpace.Api, ensure you have the following installed:

| Tool | Version | Purpose |
|------|---------|---------|
| **.NET SDK** | 8.0+ | Core framework |
| **PostgreSQL** | 12+ | Database server |
| **Docker** | Latest | Containerization |
| **Git** | Latest | Version control |
| **IDE** | VS/VS Code/Rider | Development environment |

### Initial Setup

#### 1. Clone the Repository
```bash
git clone [repository-url]
cd PlaySpace.Api
```

#### 2. Restore Dependencies
```bash
# Restore NuGet packages for all projects
dotnet restore

# Verify solution builds
dotnet build
```

#### 3. Database Setup

##### Option A: Local PostgreSQL
```bash
# Install PostgreSQL locally
# Create database: playspace_dev

# Update connection string in appsettings.Development.json
{
  "DatabaseOptions": {
    "ConnectionString": "Host=localhost;Database=playspace_dev;Username=your_user;Password=your_password"
  }
}

# Run migrations
dotnet ef database update --project PlaySpace.Repositories --startup-project PlaySpace.Api
```

##### Option B: Docker PostgreSQL
```bash
# Run PostgreSQL in Docker
docker run --name playspace-postgres -e POSTGRES_DB=playspace_dev -e POSTGRES_USER=dev -e POSTGRES_PASSWORD=devpass -p 5432:5432 -d postgres:13

# Update connection string
{
  "DatabaseOptions": {
    "ConnectionString": "Host=localhost;Database=playspace_dev;Username=dev;Password=devpass"
  }
}

# Run migrations
dotnet ef database update --project PlaySpace.Repositories --startup-project PlaySpace.Api
```

#### 4. Run the Application
```bash
# Development mode with hot reload
dotnet watch --project PlaySpace.Api

# Standard run
dotnet run --project PlaySpace.Api

# Application will be available at:
# - HTTPS: https://localhost:7125
# - HTTP: http://localhost:5125
# - Swagger UI: https://localhost:7125/swagger
```

## 🛠️ Development Commands

### Building & Running
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build PlaySpace.Api

# Run with hot reload (recommended for development)
dotnet watch --project PlaySpace.Api

# Run in production mode
dotnet run --project PlaySpace.Api --configuration Release
```

### Database Operations
```bash
# Create new migration
dotnet ef migrations add MigrationName --project PlaySpace.Repositories --startup-project PlaySpace.Api

# Update database with latest migrations
dotnet ef database update --project PlaySpace.Repositories --startup-project PlaySpace.Api

# Rollback to specific migration
dotnet ef database update PreviousMigrationName --project PlaySpace.Repositories --startup-project PlaySpace.Api

# Remove last migration (if not applied to database)
dotnet ef migrations remove --project PlaySpace.Repositories --startup-project PlaySpace.Api

# Generate SQL script for migrations
dotnet ef migrations script --project PlaySpace.Repositories --startup-project PlaySpace.Api
```

### Testing
```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test PlaySpace.Tests

# Run tests with verbose output
dotnet test --verbosity normal
```

### Docker Operations
```bash
# Build Docker image
docker build -t playspace-api:latest .

# Run containerized application
docker run -d --name playspace-api -p 7125:7125 playspace-api:latest

# View container logs
docker logs playspace-api

# Stop and remove container
docker stop playspace-api
docker rm playspace-api
```

## 📁 Project Structure & Guidelines

### Solution Organization
```
PlaySpace.Api/
├── PlaySpace.Api/              # Web API Layer
│   ├── Controllers/           # API Controllers
│   ├── Middleware/           # Custom middleware
│   ├── Program.cs            # Application startup
│   └── appsettings.json      # Configuration
├── PlaySpace.Application/     # Business Logic Layer
│   ├── Services/             # Business services
│   ├── Interfaces/           # Service contracts
│   └── DTOs/                 # Data transfer objects
├── PlaySpace.Repositories/    # Data Access Layer
│   ├── Repositories/         # Data repositories
│   ├── Context/              # DbContext
│   └── Migrations/           # EF migrations
├── PlaySpace.Domain/          # Domain Layer
│   ├── Models/               # Domain entities
│   ├── Enums/                # Enumerations
│   └── Configuration/        # Configuration models
└── docs/                      # Documentation
```

### Coding Standards

#### Naming Conventions
- **Classes**: PascalCase (e.g., `UserService`, `FacilityController`)
- **Methods**: PascalCase (e.g., `GetUserById`, `CreateReservation`)
- **Properties**: PascalCase (e.g., `UserId`, `CreatedAt`)
- **Variables**: camelCase (e.g., `userId`, `reservationData`)
- **Constants**: PascalCase (e.g., `MaxUploadSize`)

#### Code Organization
```csharp
// Controller example
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        return user != null ? Ok(user) : NotFound();
    }
}
```

#### Service Pattern
```csharp
// Service interface
public interface IUserService
{
    Task<UserDto> GetUserByIdAsync(Guid id);
    Task<UserDto> CreateUserAsync(CreateUserDto createUser);
    Task<bool> UpdateUserAsync(Guid id, UpdateUserDto updateUser);
    Task<bool> DeleteUserAsync(Guid id);
}

// Service implementation
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto> GetUserByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user?.ToDto();
    }
}
```

## 🔄 Git Workflow

### Branch Strategy
```
main (production)
├── develop (integration)
│   ├── feature/user-authentication
│   ├── feature/facility-booking
│   └── feature/payment-integration
├── hotfix/critical-bug-fix
└── release/v1.2.0
```

### Commit Message Format
```
type(scope): description

Types:
- feat: new feature
- fix: bug fix
- docs: documentation changes
- style: code formatting
- refactor: code restructuring
- test: adding tests
- chore: maintenance tasks

Examples:
feat(auth): add JWT token validation
fix(booking): resolve double booking issue
docs(api): update swagger documentation
```

### Development Process

#### 1. Create Feature Branch
```bash
git checkout develop
git pull origin develop
git checkout -b feature/feature-name
```

#### 2. Development Cycle
```bash
# Make changes
# Run tests
dotnet test

# Commit changes
git add .
git commit -m "feat(feature): implement new functionality"

# Push to remote
git push origin feature/feature-name
```

#### 3. Pull Request Process
1. **Create PR** against `develop` branch
2. **Code Review** by team members
3. **Automated Tests** must pass
4. **Merge** after approval

### Code Review Checklist
- [ ] Code follows naming conventions
- [ ] Unit tests are included
- [ ] API documentation is updated
- [ ] Security considerations addressed
- [ ] Performance implications considered
- [ ] Error handling implemented
- [ ] Database migrations included if needed

## 🧪 Development Testing

### Unit Testing Setup
```csharp
// Test project structure
PlaySpace.Tests/
├── Unit/
│   ├── Services/
│   ├── Repositories/
│   └── Controllers/
├── Integration/
│   ├── API/
│   └── Database/
└── TestHelpers/
```

### Test Examples
```csharp
// Service unit test
[Test]
public async Task GetUserById_ExistingUser_ReturnsUser()
{
    // Arrange
    var userId = Guid.NewGuid();
    var mockRepository = new Mock<IUserRepository>();
    mockRepository.Setup(r => r.GetByIdAsync(userId))
              .ReturnsAsync(new User { Id = userId });
    
    var service = new UserService(mockRepository.Object);

    // Act
    var result = await service.GetUserByIdAsync(userId);

    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual(userId, result.Id);
}

// Integration test
[Test]
public async Task POST_User_ReturnsCreatedUser()
{
    // Arrange
    var client = _factory.CreateClient();
    var userData = new { Email = "test@example.com" };

    // Act
    var response = await client.PostAsJsonAsync("/api/user", userData);

    // Assert
    response.EnsureSuccessStatusCode();
    var user = await response.Content.ReadFromJsonAsync<UserDto>();
    Assert.IsNotNull(user);
}
```

## 🚀 Deployment

### Development Deployment
```bash
# Build for release
dotnet build --configuration Release

# Publish application
dotnet publish --configuration Release --output ./publish

# Docker deployment
docker build -t playspace-api:dev .
docker run -d --name playspace-dev -p 7125:7125 playspace-api:dev
```

### Production Deployment (GitHub Actions)
The application automatically deploys to OVH Cloud when changes are pushed to the `main` branch.

#### GitHub Actions Workflow
```yaml
# .github/workflows/deploy.yml
name: Deploy to Production
on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: 8.0.x
      - name: Build and Test
        run: |
          dotnet restore
          dotnet build --configuration Release
          dotnet test
      - name: Deploy to OVH
        run: |
          # Deployment script
```

## 🐛 Debugging & Troubleshooting

### Common Issues

#### Database Connection Issues
```bash
# Check PostgreSQL is running
sudo systemctl status postgresql

# Verify connection string
# Check logs for EF Core errors
dotnet ef database update --verbose
```

#### Build Errors
```bash
# Clean solution
dotnet clean

# Restore packages
dotnet restore

# Build with verbose output
dotnet build --verbosity detailed
```

#### Migration Issues
```bash
# Check migration status
dotnet ef migrations list --project PlaySpace.Repositories --startup-project PlaySpace.Api

# Reset database (development only)
dotnet ef database drop --project PlaySpace.Repositories --startup-project PlaySpace.Api
dotnet ef database update --project PlaySpace.Repositories --startup-project PlaySpace.Api
```

### Debugging Tools
- **Visual Studio Debugger**: Full debugging support
- **Swagger UI**: API testing at `/swagger`
- **Postman**: API endpoint testing
- **PostgreSQL Admin**: Database inspection

### Logging
```csharp
// Application logging
public class UserService : IUserService
{
    private readonly ILogger<UserService> _logger;

    public async Task<UserDto> GetUserByIdAsync(Guid id)
    {
        _logger.LogInformation("Getting user with ID: {UserId}", id);
        
        try
        {
            var user = await _userRepository.GetByIdAsync(id);
            _logger.LogInformation("User found: {UserId}", id);
            return user?.ToDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user: {UserId}", id);
            throw;
        }
    }
}
```

## 📚 Additional Resources

### Documentation Links
- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [Entity Framework Core](https://docs.microsoft.com/ef/core)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [Docker Documentation](https://docs.docker.com/)

### Team Resources
- **Code Reviews**: GitHub Pull Requests
- **Issue Tracking**: GitHub Issues
- **Documentation**: Project Wiki
- **Communication**: Team Slack/Discord

---

> **New Developers**: Start with the Getting Started section
> **Experienced Team Members**: Reference specific workflow sections as needed
> **DevOps Engineers**: Focus on deployment and Docker sections
> **Architects**: Review project structure and coding standards