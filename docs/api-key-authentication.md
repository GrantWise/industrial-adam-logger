# API Key Authentication Guide

**Version**: 1.0
**Date**: 2025-10-04
**Status**: Production Ready

---

## Overview

The Industrial ADAM Logger uses **API key authentication** for secure access to all endpoints. This simple, pragmatic approach is ideal for industrial IoT scenarios where:

- Machine-to-machine (M2M) communication is primary
- Services and scripts need programmatic access
- Offline operation is required (air-gapped factories)
- Simple credential management is preferred

## How It Works

1. **Request includes API key header**
   ```
   X-API-Key: IND-ADAM-PROD-abc123def456
   ```

2. **Server validates key**
   - Checks if key exists in configuration
   - Verifies key hasn't expired
   - Returns 401 if invalid

3. **Request proceeds with authenticated identity**
   - Claims populated from key info
   - Authorization policies applied

---

## Configuration

### API Keys File

Location: `src/Industrial.Adam.Logger.WebApi/config/apikeys.json`

**Format**:
```json
{
  "keys": [
    {
      "id": "unique-key-id",
      "key": "IND-ADAM-PROD-your-strong-key-here",
      "name": "Human-readable name",
      "expiresAt": "2025-12-31T23:59:59Z",
      "permissions": ["read", "write", "restart"]
    }
  ]
}
```

**Fields**:
- `id`: Unique identifier for logging/auditing
- `key`: The actual API key value (share this with consumers)
- `name`: Descriptive name for humans
- `expiresAt`: Optional expiration (null = never expires)
- `permissions`: Optional permissions array (for future RBAC)

### Application Configuration

In `appsettings.json`:
```json
{
  "ApiKeys": {
    "FilePath": "config/apikeys.json"
  }
}
```

Override path with environment variable:
```bash
export ApiKeys__FilePath="/secure/path/apikeys.json"
```

---

## Generating API Keys

### Secure Random Keys

**Using OpenSSL** (recommended):
```bash
# Generate a strong 32-character key
openssl rand -hex 16
# Output: abc123def456...

# Full format
echo "IND-ADAM-PROD-$(openssl rand -hex 16)"
# Output: IND-ADAM-PROD-abc123def456...
```

**Using PowerShell**:
```powershell
# Generate strong key
$key = [System.Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
"IND-ADAM-PROD-$key"
```

**Key Format** (recommended):
```
IND-ADAM-[ENV]-[RANDOM]

Examples:
- IND-ADAM-PROD-a1b2c3d4e5f6g7h8
- IND-ADAM-DEV-x9y8z7w6v5u4t3s2
- IND-ADAM-TEST-m1n2o3p4q5r6s7t8
```

---

## Using API Keys

### cURL Example

```bash
curl -H "X-API-Key: IND-ADAM-DEV-2024-abc123def456ghi789" \
     http://localhost:5000/devices
```

### PowerShell Example

```powershell
$headers = @{
    "X-API-Key" = "IND-ADAM-DEV-2024-abc123def456ghi789"
}

Invoke-RestMethod -Uri "http://localhost:5000/devices" -Headers $headers
```

### Python Example

```python
import requests

headers = {
    "X-API-Key": "IND-ADAM-DEV-2024-abc123def456ghi789"
}

response = requests.get("http://localhost:5000/devices", headers=headers)
print(response.json())
```

### C# Example

```csharp
using var client = new HttpClient();
client.DefaultRequestHeaders.Add("X-API-Key", "IND-ADAM-DEV-2024-abc123def456ghi789");

var response = await client.GetAsync("http://localhost:5000/devices");
var data = await response.Content.ReadAsStringAsync();
```

### Swagger UI

1. Open Swagger UI: `http://localhost:5000`
2. Click **"Authorize"** button (top right)
3. Enter your API key
4. Click **"Authorize"**
5. Try endpoints

---

## Security Best Practices

### Production Deployment

1. **Secure Storage**
   ```bash
   # Store API keys file outside web root
   export ApiKeys__FilePath="/etc/adam-logger/apikeys.json"

   # Restrict file permissions
   chmod 600 /etc/adam-logger/apikeys.json
   chown adam-logger:adam-logger /etc/adam-logger/apikeys.json
   ```

2. **HTTPS Only**
   - **Never** use HTTP in production
   - API keys in plain text over HTTP = insecure
   - Always use HTTPS/TLS

3. **Key Rotation**
   ```bash
   # Generate new key
   NEW_KEY="IND-ADAM-PROD-$(openssl rand -hex 16)"

   # Add to apikeys.json with expiration on old key
   # Update consumers with new key
   # Remove old key after transition period
   ```

4. **Principle of Least Privilege**
   - Create separate keys per service/user
   - Use `permissions` field for RBAC (future)
   - Set expiration dates where appropriate

5. **Audit Logging**
   - All API key usage is logged
   - Monitor failed authentication attempts
   - Alert on suspicious activity

### Key Management

**DO**:
- ✅ Use strong random keys (min 16 bytes)
- ✅ Store keys securely (encrypted at rest)
- ✅ Rotate keys periodically
- ✅ Use HTTPS for all API calls
- ✅ Set expiration dates
- ✅ One key per service/application

**DON'T**:
- ❌ Commit keys to source control
- ❌ Share keys in plain text (email, chat)
- ❌ Reuse keys across environments
- ❌ Use HTTP (always HTTPS)
- ❌ Use weak/predictable keys

---

## Troubleshooting

### 401 Unauthorized

**Symptom**: API returns `401 Unauthorized`

**Causes**:
1. Missing `X-API-Key` header
2. Invalid/expired API key
3. Key not in `apikeys.json`

**Solution**:
```bash
# Check logs
docker logs adam-logger | grep "Invalid API key"

# Verify key exists
cat config/apikeys.json | jq '.keys[] | select(.key=="YOUR-KEY")'

# Test with valid dev key
curl -H "X-API-Key: IND-ADAM-DEV-2024-abc123def456ghi789" \
     http://localhost:5000/devices
```

### No API Keys Loaded

**Symptom**: Log message "No API keys file found"

**Cause**: `apikeys.json` file missing or wrong path

**Solution**:
```bash
# Check file exists
ls -la src/Industrial.Adam.Logger.WebApi/config/apikeys.json

# Verify path in config
cat appsettings.json | jq '.ApiKeys.FilePath'

# Create from example
cp config/apikeys.example.json config/apikeys.json
```

### Key Expired

**Symptom**: Previously working key now returns 401

**Cause**: `expiresAt` date passed

**Solution**:
```json
// Update apikeys.json
{
  "id": "prod-service-1",
  "key": "IND-ADAM-PROD-abc123",
  "name": "Production Service",
  "expiresAt": null  // or new future date
}
```

---

## Migration from JWT (if applicable)

If upgrading from JWT-based auth:

1. **Keep JWT config** (for backward compatibility)
2. **Add API key auth** (new primary method)
3. **Migrate clients** to API keys over time
4. **Remove JWT** once all clients migrated

**Dual Auth** (support both):
```csharp
// Future enhancement - see PRODUCTION-READINESS.md
// Supports both X-API-Key header AND JWT Bearer token
```

---

## FAQ

**Q: Can I use multiple API keys?**
A: Yes, add multiple entries to `apikeys.json`. Each service should have its own key.

**Q: How do I revoke a key?**
A: Remove it from `apikeys.json` and restart the service. Takes effect immediately.

**Q: Do keys need to be in a specific format?**
A: No, but we recommend `IND-ADAM-[ENV]-[RANDOM]` for clarity.

**Q: Can I use API keys with MQTT?**
A: This is for REST API only. MQTT uses separate authentication (if enabled).

**Q: What about rate limiting?**
A: Not currently implemented. Add if needed for production (see ASP.NET Core rate limiting middleware).

**Q: How do I audit API key usage?**
A: Check application logs. All authenticated requests are logged with key ID and name.

---

## Next Steps

1. **Generate production keys**
   ```bash
   openssl rand -hex 16
   ```

2. **Update `apikeys.json`**
   ```json
   {
     "keys": [
       {
         "id": "prod-line-1",
         "key": "IND-ADAM-PROD-<your-generated-key>",
         "name": "Production Line 1",
         "expiresAt": "2025-12-31T23:59:59Z"
       }
     ]
   }
   ```

3. **Secure the file**
   ```bash
   chmod 600 config/apikeys.json
   ```

4. **Test authentication**
   ```bash
   curl -H "X-API-Key: <your-key>" http://localhost:5000/health
   ```

5. **Deploy to production** (see deployment guide)

---

## Related Documentation

- [Production Readiness](../PRODUCTION-READINESS.md) - Security requirements
- [Getting Started](getting-started.md) - Quick start guide
- [API Documentation](http://localhost:5000/swagger) - Swagger UI

---

**Security Note**: This authentication method is designed for industrial IoT M2M communication in controlled networks. For public-facing APIs or user authentication, consider adding JWT support (see PRODUCTION-READINESS.md).
