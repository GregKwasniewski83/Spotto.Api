# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Development
- `dotnet run --project PlaySpace.Api` - Run the API in development mode
- `dotnet watch --project PlaySpace.Api` - Run with hot reload
- `dotnet build` - Build the entire solution
- `dotnet build PlaySpace.Api` - Build specific project
- `dotnet restore` - Restore NuGet packages

### Database
- `dotnet ef migrations add <MigrationName> --project PlaySpace.Repositories --startup-project PlaySpace.Api` - Create new migration
- `dotnet ef database update --project PlaySpace.Repositories --startup-project PlaySpace.Api` - Apply migrations

### Testing
- `dotnet test` - Run all tests in the solution

### Docker
- `docker build -t play-space-api:latest .` - Build Docker image
- `docker run -d --name play-space-api -p 7125:7125 play-space-api:latest` - Run containerized API

## Architecture

This is a .NET 8 Web API following Clean Architecture principles with four main projects:

### Project Structure
- **PlaySpace.Api** - Web API layer with controllers and Program.cs configuration
- **PlaySpace.Application** (Services) - Business logic and application services
- **PlaySpace.Repositories** - Data access layer with Entity Framework Core
- **PlaySpace.Domain** - Domain entities, DTOs, and configuration models

### Key Technologies
- **Database**: PostgreSQL with Entity Framework Core 9.0.8
- **Authentication**: JWT Bearer tokens with custom validation
- **API Documentation**: Swagger/OpenAPI
- **Containerization**: Docker with multi-stage build

### Dependency Injection Pattern
Services and repositories are registered in Program.cs using the interface/implementation pattern:
- Services: `IUserService -> UserService`, `IFacilityService -> FacilityService`, etc.
- Repositories: `IUserRepository -> UserRepository`, `IFacilityRepository -> FacilityRepository`, etc.

### Database Configuration
- Uses Options pattern with `DatabaseOptions` class
- PostgreSQL connection configured through `appsettings.json`
- Database seeding occurs on application startup via `DatabaseSeeder`

### Authentication & Security
- JWT authentication with symmetric key validation
- CORS configured for "AllowMobileApp" policy (allows any origin/method/header)
- File upload limit set to 100MB
- BCrypt.Net for password hashing

### API Structure
Controllers follow RESTful conventions with route pattern `api/[controller]`:
- AuthController - Authentication (signup/signin)
- UserController - User management
- FacilityController - Facility operations
- PaymentController, TimeSlotController, BusinessProfileController, etc.

### Deployment
- GitHub Actions workflow deploys to OVH cloud on master branch pushes
- Docker container runs on port 7125, exposed as port 83
- Automatic database connection to host.docker.internal PostgreSQL instance