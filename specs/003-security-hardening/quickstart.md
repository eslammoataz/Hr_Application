# Quickstart: Phase 1 - Security Hardening

This guide explains how to verify the security hardening changes.

## Prerequisites
- PostgreSQL database up and running.
- .NET SDK installed.

## Setup
1. Configure `SeedPasswordSettings` in `appsettings.Development.json`:
   ```json
   "SeedPasswordSettings": "YourSecureSeedPassword123!"
   ```

2. Run the application:
   ```bash
   dotnet run --project HrSystemApp.Api
   ```

## Verification Steps

### 1. Hardened Password Policy
- Navigate to the Register or Change Password endpoint.
- Attempt to set a password that is less than 12 characters or lacks special characters.
- **Expected**: The API returns a validation error from Identity.

### 2. Forced Password Reset
- Log in with a legacy seeded account (e.g., `admin@hrsystem.com`).
- Check the JWT or the `/api/auth/me` endpoint (if available) to see if `MustChangePassword` is required.
- **Expected**: Any account with a weak password hash is flagged for reset.

### 3. Claim Constants
- Inspect `HrSystemApp.Domain.Constants.AppClaimTypes`.
- **Expected**: All security logic uses these constants instead of inline strings.
