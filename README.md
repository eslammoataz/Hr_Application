# HrSystemApp

A .NET 8.0 Web API project following Clean Architecture principles.

## 📁 Project Structure

```
HrSystemApp/
├── HrSystemApp.Domain/          # Entities, enums, value objects
├── HrSystemApp.Application/     # Services, interfaces, DTOs, CQRS
├── HrSystemApp.Infrastructure/  # EF Core, repositories, JWT, Identity
└── HrSystemApp.Api/             # Controllers, Program.cs
```

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK
- Docker & Docker Compose (optional)

### Run locally
```bash
# Start the database
docker-compose up -d

# Run the API
dotnet run --project HrSystemApp.Api
```

### API Documentation
Open http://localhost:5119/swagger

## 🔐 Authentication
1. `POST /api/auth/otp/generate` with `{"phoneNumber": "+1234567890"}`
2. `POST /api/auth/otp/validate` with `{"phoneNumber": "+1234567890", "otpCode": "123456"}`
3. Use returned token: `Authorization: Bearer <token>`
