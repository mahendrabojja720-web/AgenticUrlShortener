# Agentic URL Shortener - Setup Guide

## Quick Start

### 1. Prerequisites
- .NET 8 SDK or later
- Visual Studio 2022 / VS Code
- PowerShell or Command Prompt

### 2. Clone/Navigate to Workspace
```bash
cd C:\work\AgenticUrlShortener
```

### 3. Restore and Build
```bash
# Restore NuGet packages
dotnet restore

# Build solution
dotnet build
```

### 4. Run Tests
```bash
# Run all unit and integration tests
dotnet test

# Run with verbose output
dotnet test -v d

# Run specific test class
dotnet test --filter "ClassName=UrlShortenerServiceTests"
```

### 5. Run the API Server
```bash
cd AgenticUrlShortener

# Development mode (includes Swagger UI)
dotnet run

# Output:
# Building...
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: https://localhost:7001
#       Now listening on: http://localhost:5000
```

### 6. Access the API

**Swagger UI** (Interactive API documentation):
```
https://localhost:7001/swagger/index.html
```

**Base URL**:
```
https://localhost:7001/api
```

---

## File Structure

```
AgenticUrlShortener/
??? Controllers/
?   ??? UrlShortenerController.cs          # REST API for URL operations
?   ??? WorkflowController.cs              # Orchestration control endpoints
??? Domain/
?   ??? Models/
?       ??? ShortUrl.cs                    # Core entity
?       ??? UrlAnalytics.cs                # Analytics model
?       ??? WorkflowExecution.cs           # Orchestration state model
??? Application/
?   ??? Services/
?   ?   ??? UrlShortenerService.cs         # Business logic
?   ??? DTOs/
?       ??? UrlShortenerDTOs.cs            # Request/response contracts
??? Infrastructure/
?   ??? Persistence/
?       ??? IUrlRepository.cs              # Repository interface + in-memory impl
?       ??? IWorkflowRepository.cs         # Workflow repository
??? Orchestration/
?   ??? Core/
?   ?   ??? WorkflowStage.cs               # Abstract stage base class
?   ?   ??? WorkflowOrchestrator.cs        # Orchestration engine
?   ??? Stages/
?       ??? UrlCreationStages.cs           # Greenfield workflow stages
?       ??? AccessAndDeletionStages.cs     # Brownfield workflow stages
??? Program.cs                             # Dependency injection setup
??? AgenticUrlShortener.csproj

AgenticUrlShortener.Tests/
??? Services/
?   ??? UrlShortenerServiceTests.cs        # 11 unit tests
??? Orchestration/
?   ??? WorkflowOrchestratorTests.cs       # 12 unit tests
??? AgenticUrlShortener.Tests.csproj

Documentation/
??? ENGINEERING_SUMMARY.md                 # Architecture, decisions, risks
??? SETUP_INSTRUCTIONS.md                  # This file
??? API_EXAMPLES.md                        # cURL/Postman examples
```

---

## API Usage Examples

### 1. Create a Shortened URL

**Request**:
```bash
curl -X POST https://localhost:7001/api/urlshortener/create \
  -H "Content-Type: application/json" \
  -d '{
    "originalUrl": "https://www.example.com/very/long/path/to/resource",
    "customShortCode": "mycode",
    "createdBy": "user@example.com"
  }'
```

**Response**:
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "shortCode": "mycode",
  "shortUrl": "https://short.url/mycode",
  "originalUrl": "https://www.example.com/very/long/path/to/resource",
  "createdAt": "2024-01-15T10:30:00Z",
  "expiresAt": null
}
```

### 2. Retrieve a Shortened URL

**Request**:
```bash
curl https://localhost:7001/api/urlshortener/mycode
```

**Response**:
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "originalUrl": "https://www.example.com/very/long/path/to/resource",
  "shortCode": "mycode",
  "createdAt": "2024-01-15T10:30:00Z",
  "expiresAt": null,
  "isActive": true,
  "accessCount": 1,
  "lastAccessedAt": "2024-01-15T10:31:00Z"
}
```

### 3. Get Analytics

**Request**:
```bash
curl https://localhost:7001/api/urlshortener/mycode/analytics
```

**Response**:
```json
{
  "shortUrlId": "123e4567-e89b-12d3-a456-426614174000",
  "shortCode": "mycode",
  "totalAccess": 42,
  "lastAccessedAt": "2024-01-15T15:45:00Z",
  "lastAccessedFrom": "192.168.1.1",
  "referrerCounts": {}
}
```

### 4. List All URLs

**Request**:
```bash
curl https://localhost:7001/api/urlshortener
```

**Response**:
```json
[
  {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "originalUrl": "https://www.example.com/...",
    "shortCode": "mycode",
    "createdAt": "2024-01-15T10:30:00Z",
    "accessCount": 42
  },
  ...
]
```

### 5. Delete a URL

**Request**:
```bash
curl -X DELETE https://localhost:7001/api/urlshortener/123e4567-e89b-12d3-a456-426614174000
```

**Response**:
```
204 No Content
```

---

## Workflow Orchestration Examples

### 6. Get Workflow Status

**Request**:
```bash
curl https://localhost:7001/api/workflow/workflow-id-123
```

**Response**:
```json
{
  "id": "workflow-id-123",
  "workflowType": "UrlCreation",
  "status": "Completed",
  "createdAt": "2024-01-15T10:30:00Z",
  "completedAt": "2024-01-15T10:30:05Z",
  "stages": [
    {
      "stageName": "ValidateAndNormalize",
      "status": "Completed",
      "startedAt": "2024-01-15T10:30:00Z",
      "completedAt": "2024-01-15T10:30:01Z",
      "retryCount": 0
    },
    ...
  ],
  "auditLog": {
    "created_at_2024-01-15T10:30:00Z": "Workflow created: UrlCreation",
    "audit_2024-01-15T10:30:01Z": "URL validated and normalized: https://..."
  }
}
```

### 7. Get Pending Approvals

**Request**:
```bash
curl https://localhost:7001/api/workflow/approvals/pending
```

**Response**:
```json
[
  {
    "id": "workflow-delete-456",
    "workflowType": "UrlDeletion",
    "status": "AwaitingApproval",
    "approvalCheckpoints": ["admin"],
    "createdAt": "2024-01-15T10:40:00Z"
  }
]
```

### 8. Approve a Workflow

**Request**:
```bash
curl -X POST https://localhost:7001/api/workflow/workflow-delete-456/approve \
  -H "Content-Type: application/json" \
  -d '{
    "approvedBy": "admin@example.com",
    "reason": "Confirmed duplicate entry"
  }'
```

**Response**:
```json
{
  "message": "Workflow approved and execution started"
}
```

### 9. Reject a Workflow

**Request**:
```bash
curl -X POST https://localhost:7001/api/workflow/workflow-delete-456/reject \
  -H "Content-Type: application/json" \
  -d '{
    "approvedBy": "reviewer@example.com",
    "reason": "Need more context on why deletion is needed"
  }'
```

### 10. Rollback a Workflow

**Request**:
```bash
curl -X POST https://localhost:7001/api/workflow/workflow-id-123/rollback \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Deployment rolled back to previous version"
  }'
```

---

## Environment Configuration

### Development
```bash
set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=https://localhost:7001;http://localhost:5000
```

### Production
```bash
set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=https://0.0.0.0:443
```

---

## Troubleshooting

### Issue: Port Already in Use
```bash
# Find process using port 7001
netstat -ano | findstr :7001

# Kill process (Windows)
taskkill /PID <PID> /F

# Or run on different port
set ASPNETCORE_URLS=https://localhost:7002
```

### Issue: Certificate Error on Localhost
```bash
# Install self-signed cert (Windows)
dotnet dev-certs https --trust
```

### Issue: Tests Failing
```bash
# Clean and rebuild
dotnet clean
dotnet build
dotnet test -v d
```

---

## Performance Tuning

### Default Limits
- **Max Short Code Length**: 8 characters
- **Min Short Code Length**: 4 characters
- **Max URL Length**: 2048 characters
- **Max Retries**: 3 (per stage)
- **Retry Delay**: 1000ms

### For Production Deployment
1. Replace `InMemoryUrlRepository` with SQL Server / Cosmos DB
2. Add Redis caching layer for frequent lookups
3. Implement distributed rate limiting
4. Configure Application Insights
5. Add authentication/authorization middleware

---

## Testing Checklist

- [ ] Run `dotnet test` (all tests pass)
- [ ] Create a URL via API and verify it works
- [ ] Test URL expiration
- [ ] Test approval workflow
- [ ] Test rollback scenario
- [ ] Check audit trail completeness
- [ ] Verify error messages are clear
- [ ] Load test with concurrent requests

---

## Support & Feedback

For issues or feature requests:
1. Check the ENGINEERING_SUMMARY.md for architecture details
2. Review relevant test cases for usage examples
3. Check orchestration logs in workflow execution audit trail

---

**Last Updated**: 2024  
**Status**: Ready for Testing & Deployment
