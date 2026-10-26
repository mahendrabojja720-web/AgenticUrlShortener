# Agentic URL Shortener - API Examples

This document provides detailed examples for all API endpoints.

## Base URL
```
https://localhost:7001/api
```

## Table of Contents
1. [URL Shortener Endpoints](#url-shortener-endpoints)
2. [Workflow Orchestration Endpoints](#workflow-orchestration-endpoints)
3. [Scenario Workflows](#scenario-workflows)

---

## URL Shortener Endpoints

### 1. Create Shortened URL

**Endpoint**: `POST /urlshortener/create`

**Description**: Create a new shortened URL with optional custom code and expiration.

**Request Body**:
```json
{
  "originalUrl": "https://www.example.com/blog/article/how-to-build-scalable-systems",
  "customShortCode": "blog123",
  "expiresAt": "2025-12-31T23:59:59Z",
  "createdBy": "user@example.com"
}
```

**Response** (200 OK):
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "shortCode": "blog123",
  "shortUrl": "https://short.url/blog123",
  "originalUrl": "https://www.example.com/blog/article/how-to-build-scalable-systems",
  "createdAt": "2024-01-15T10:30:00Z",
  "expiresAt": "2025-12-31T23:59:59Z"
}
```

**Error Response** (400 Bad Request):
```json
{
  "error": "URL must be a valid HTTP or HTTPS URL."
}
```

**Error Response** (409 Conflict):
```json
{
  "error": "Short code 'blog123' already exists."
}
```

---

### 2. Get Shortened URL

**Endpoint**: `GET /urlshortener/{shortCode}`

**Description**: Retrieve a shortened URL by its short code. Updates access count.

**Example Request**:
```bash
curl -X GET https://localhost:7001/api/urlshortener/blog123
```

**Response** (200 OK):
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "originalUrl": "https://www.example.com/blog/article/how-to-build-scalable-systems",
  "shortCode": "blog123",
  "createdAt": "2024-01-15T10:30:00Z",
  "expiresAt": "2025-12-31T23:59:59Z",
  "isActive": true,
  "accessCount": 5,
  "lastAccessedAt": "2024-01-15T15:45:30Z"
}
```

**Error Response** (404 Not Found):
```json
{
  "error": "Short URL not found or expired"
}
```

---

### 3. Get Analytics

**Endpoint**: `GET /urlshortener/{shortCode}/analytics`

**Description**: Retrieve usage analytics for a shortened URL.

**Example Request**:
```bash
curl -X GET https://localhost:7001/api/urlshortener/blog123/analytics
```

**Response** (200 OK):
```json
{
  "shortUrlId": "550e8400-e29b-41d4-a716-446655440000",
  "shortCode": "blog123",
  "totalAccess": 5,
  "lastAccessedAt": "2024-01-15T15:45:30Z",
  "lastAccessedFrom": "192.168.1.100",
  "referrerCounts": {}
}
```

---

### 4. List All URLs

**Endpoint**: `GET /urlshortener`

**Description**: Retrieve all active shortened URLs.

**Example Request**:
```bash
curl -X GET https://localhost:7001/api/urlshortener
```

**Response** (200 OK):
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "originalUrl": "https://www.example.com/blog/article/...",
    "shortCode": "blog123",
    "createdAt": "2024-01-15T10:30:00Z",
    "expiresAt": "2025-12-31T23:59:59Z",
    "isActive": true,
    "accessCount": 5,
    "lastAccessedAt": "2024-01-15T15:45:30Z"
  },
  {
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "originalUrl": "https://www.example.com/docs/...",
    "shortCode": "docs456",
    "createdAt": "2024-01-14T08:00:00Z",
    "expiresAt": null,
    "isActive": true,
    "accessCount": 23,
    "lastAccessedAt": "2024-01-15T16:30:00Z"
  }
]
```

---

### 5. Delete Shortened URL

**Endpoint**: `DELETE /urlshortener/{id}`

**Description**: Delete a shortened URL by its ID. Triggers a deletion workflow with approval gates.

**Example Request**:
```bash
curl -X DELETE https://localhost:7001/api/urlshortener/550e8400-e29b-41d4-a716-446655440000
```

**Response** (204 No Content):
```
(empty body)
```

**Error Response** (404 Not Found):
```json
{
  "error": "Short URL not found"
}
```

---

## Workflow Orchestration Endpoints

### 6. Get Workflow Execution Status

**Endpoint**: `GET /workflow/{workflowId}`

**Description**: Retrieve the current status and details of a workflow execution.

**Example Request**:
```bash
curl -X GET https://localhost:7001/api/workflow/wf-123456-78901
```

**Response** (200 OK):
```json
{
  "id": "wf-123456-78901",
  "workflowType": "UrlCreation",
  "status": "Completed",
  "createdAt": "2024-01-15T10:30:00Z",
  "completedAt": "2024-01-15T10:30:05Z",
  "context": {
    "request": {
      "originalUrl": "https://www.example.com",
      "customShortCode": "test",
      "createdBy": "user@example.com"
    }
  },
  "stages": [
    {
      "stageName": "ValidateAndNormalize",
      "status": "Completed",
      "startedAt": "2024-01-15T10:30:00.000Z",
      "completedAt": "2024-01-15T10:30:00.500Z",
      "output": {
        "normalized_url": "https://www.example.com/",
        "custom_code": "test"
      },
      "errorMessage": null,
      "retryCount": 0,
      "dependsOn": []
    },
    {
      "stageName": "CheckDuplicateAndReserve",
      "status": "Completed",
      "startedAt": "2024-01-15T10:30:00.500Z",
      "completedAt": "2024-01-15T10:30:01.000Z",
      "output": {
        "short_code": "test",
        "is_duplicate": false
      },
      "retryCount": 0,
      "dependsOn": ["ValidateAndNormalize"]
    },
    {
      "stageName": "PersistUrl",
      "status": "Completed",
      "startedAt": "2024-01-15T10:30:01.000Z",
      "completedAt": "2024-01-15T10:30:01.500Z",
      "output": {
        "url_id": "550e8400-e29b-41d4-a716-446655440000",
        "short_code": "test"
      },
      "retryCount": 0,
      "dependsOn": ["CheckDuplicateAndReserve"]
    },
    {
      "stageName": "GenerateResponse",
      "status": "Completed",
      "startedAt": "2024-01-15T10:30:01.500Z",
      "completedAt": "2024-01-15T10:30:02.000Z",
      "output": {
        "short_url": "https://short.url/test",
        "short_code": "test"
      },
      "retryCount": 0,
      "dependsOn": ["PersistUrl"]
    }
  ],
  "approvalCheckpoints": [],
  "approvedBy": null,
  "approvedAt": null,
  "auditLog": {
    "created_at_2024-01-15T10:30:00Z": "Workflow created: UrlCreation",
    "audit_2024-01-15T10:30:00.100Z": "Workflow started by test-user",
    "audit_2024-01-15T10:30:00.500Z": "URL validated and normalized: https://www.example.com/",
    "audit_2024-01-15T10:30:00.900Z": "Short code reserved: test",
    "audit_2024-01-15T10:30:01.500Z": "URL persisted: ID=550e8400-e29b-41d4-a716-446655440000, Code=test",
    "audit_2024-01-15T10:30:02.100Z": "Response generated for short code: test"
  }
}
```

---

### 7. Get Pending Approvals

**Endpoint**: `GET /workflow/approvals/pending`

**Description**: Retrieve all workflows awaiting approval.

**Example Request**:
```bash
curl -X GET https://localhost:7001/api/workflow/approvals/pending
```

**Response** (200 OK):
```json
[
  {
    "id": "wf-delete-001",
    "workflowType": "UrlDeletion",
    "status": "AwaitingApproval",
    "createdAt": "2024-01-15T14:20:00Z",
    "completedAt": null,
    "context": {
      "url_id": "550e8400-e29b-41d4-a716-446655440000"
    },
    "stages": [
      {
        "stageName": "DeleteUrl",
        "status": "Pending",
        "dependsOn": []
      }
    ],
    "approvalCheckpoints": ["admin"],
    "approvedBy": null,
    "approvedAt": null
  },
  {
    "id": "wf-delete-002",
    "workflowType": "UrlDeletion",
    "status": "AwaitingApproval",
    "createdAt": "2024-01-15T15:10:00Z",
    "context": {
      "url_id": "660e8400-e29b-41d4-a716-446655440001"
    },
    "stages": [
      {
        "stageName": "DeleteUrl",
        "status": "Pending",
        "dependsOn": []
      }
    ],
    "approvalCheckpoints": ["admin"],
    "approvedBy": null,
    "approvedAt": null
  }
]
```

---

### 8. Approve Workflow

**Endpoint**: `POST /workflow/{workflowId}/approve`

**Description**: Approve a pending workflow. Triggers stage execution.

**Request Body**:
```json
{
  "approvedBy": "admin@example.com",
  "reason": "Confirmed this is a duplicate entry and can be safely deleted"
}
```

**Example Request**:
```bash
curl -X POST https://localhost:7001/api/workflow/wf-delete-001/approve \
  -H "Content-Type: application/json" \
  -d '{
    "approvedBy": "admin@example.com",
    "reason": "Confirmed duplicate"
  }'
```

**Response** (200 OK):
```json
{
  "message": "Workflow approved and execution started"
}
```

**Error Response** (400 Bad Request):
```json
{
  "error": "Cannot approve workflow. Check status."
}
```

---

### 9. Reject Workflow

**Endpoint**: `POST /workflow/{workflowId}/reject`

**Description**: Reject a pending workflow. Cancels execution.

**Request Body**:
```json
{
  "approvedBy": "reviewer@example.com",
  "reason": "Need more information on the deletion request"
}
```

**Example Request**:
```bash
curl -X POST https://localhost:7001/api/workflow/wf-delete-001/reject \
  -H "Content-Type: application/json" \
  -d '{
    "approvedBy": "reviewer@example.com",
    "reason": "Needs more context"
  }'
```

**Response** (200 OK):
```json
{
  "message": "Workflow rejected"
}
```

---

### 10. Rollback Workflow

**Endpoint**: `POST /workflow/{workflowId}/rollback`

**Description**: Rollback a completed or failed workflow. Reverses all persisted changes.

**Request Body**:
```json
{
  "reason": "Deployment rolled back to previous version due to critical bug"
}
```

**Example Request**:
```bash
curl -X POST https://localhost:7001/api/workflow/wf-123456-78901/rollback \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Deployment reverted to v1.0"
  }'
```

**Response** (200 OK):
```json
{
  "message": "Workflow rolled back"
}
```

**Rollback Result**:
```json
{
  "id": "wf-123456-78901",
  "workflowType": "UrlCreation",
  "status": "RolledBack",
  "stages": [
    {
      "stageName": "ValidateAndNormalize",
      "status": "RolledBack"
    },
    {
      "stageName": "CheckDuplicateAndReserve",
      "status": "RolledBack"
    },
    {
      "stageName": "PersistUrl",
      "status": "RolledBack"
    },
    {
      "stageName": "GenerateResponse",
      "status": "RolledBack"
    }
  ],
  "auditLog": {
    "..": "..",
    "audit_2024-01-15T16:45:00Z": "Rollback initiated: Deployment reverted to v1.0",
    "audit_2024-01-15T16:45:00.100Z": "Stage rolled back: GenerateResponse",
    "audit_2024-01-15T16:45:00.200Z": "Short code released on rollback: test",
    "audit_2024-01-15T16:45:00.300Z": "URL deleted on rollback: 550e8400-e29b-41d4-a716-446655440000"
  }
}
```

---

## Scenario Workflows

### Scenario 1: End-to-End URL Creation (Greenfield)

**Step 1**: Create a shortened URL (auto-approval, no gates)
```bash
curl -X POST https://localhost:7001/api/urlshortener/create \
  -H "Content-Type: application/json" \
  -d '{
    "originalUrl": "https://en.wikipedia.org/wiki/URL_shortening",
    "customShortCode": "wiki_shortening",
    "createdBy": "researcher@example.com"
  }'
```

**Response**:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "shortCode": "wiki_shortening",
  "shortUrl": "https://short.url/wiki_shortening",
  "originalUrl": "https://en.wikipedia.org/wiki/URL_shortening",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

**Step 2**: Access the shortened URL (workflow internally tracks analytics)
```bash
curl -X GET https://localhost:7001/api/urlshortener/wiki_shortening
```

**Step 3**: Get analytics
```bash
curl -X GET https://localhost:7001/api/urlshortener/wiki_shortening/analytics
```

---

### Scenario 2: URL Deletion with Approval (Brownfield)

**Step 1**: Initiate deletion (creates AwaitingApproval workflow)
```bash
# Note: This would typically be triggered via a system event or admin dashboard
# For this prototype, deletion is immediate. In production, it would queue an approval workflow.
curl -X DELETE https://localhost:7001/api/urlshortener/550e8400-e29b-41d4-a716-446655440000
```

**Step 2**: Check pending approvals
```bash
curl -X GET https://localhost:7001/api/workflow/approvals/pending
```

**Step 3**: Approve deletion
```bash
curl -X POST https://localhost:7001/api/workflow/wf-delete-001/approve \
  -H "Content-Type: application/json" \
  -d '{
    "approvedBy": "admin@example.com",
    "reason": "Confirmed outdated link"
  }'
```

**Step 4**: Verify deletion by attempting to access
```bash
curl -X GET https://localhost:7001/api/urlshortener/wiki_shortening
# Returns 404: Short URL not found
```

---

### Scenario 3: Rollback Due to Error (Ambiguous/Recovery)

**Step 1**: Create URL
```bash
curl -X POST https://localhost:7001/api/urlshortener/create \
  -H "Content-Type: application/json" \
  -d '{
    "originalUrl": "https://example.com/critical-link"
  }'
```

**Step 2**: Discover error (e.g., wrong URL was encoded)
- Check workflow status
- Decide to rollback

**Step 3**: Rollback
```bash
curl -X POST https://localhost:7001/api/workflow/wf-123456-78901/rollback \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Wrong URL encoded; rolling back"
  }'
```

**Step 4**: Verify rollback
```bash
curl -X GET https://localhost:7001/api/workflow/wf-123456-78901
# Status: RolledBack
```

**Step 5**: Create correct URL
```bash
curl -X POST https://localhost:7001/api/urlshortener/create \
  -H "Content-Type: application/json" \
  -d '{
    "originalUrl": "https://example.com/correct-link"
  }'
```

---

## Response Codes Reference

| Code | Meaning |
|------|---------|
| 200 | OK (successful GET, POST) |
| 201 | Created |
| 204 | No Content (DELETE successful) |
| 400 | Bad Request (validation error) |
| 404 | Not Found |
| 409 | Conflict (duplicate short code) |
| 500 | Internal Server Error |

---

## Testing with Postman

1. Import collection from `postman_collection.json` (to be generated)
2. Set environment variables:
   - `base_url`: `https://localhost:7001/api`
   - `auth_token`: (if authentication added)
3. Run requests sequentially or in batches
4. Validate responses against schemas

---

## Rate Limiting (Future)

Once implemented:
```bash
# Request will include rate limit headers
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 999
X-RateLimit-Reset: 1705338600
```

---

**Last Updated**: 2024  
**API Version**: 1.0
