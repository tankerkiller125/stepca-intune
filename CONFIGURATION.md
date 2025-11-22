# StepCA Intune SCEP Connector Configuration Guide

## Overview

This guide explains how to configure the StepCA Intune SCEP Connector for production use.

## Configuration Structure

The connector uses hierarchical configuration with the following structure:

```
Intune:
  - AzureAppId: Azure AD Application ID
  - AzureAppSecret: Azure AD Application Secret
  - TenantId: Azure AD Tenant ID
  - ProviderNameAndVersion: Connector identifier
  - IntuneResourceUrl: Microsoft Graph API scope
  - GraphApiEndpoint: Microsoft Graph API endpoint
  - ServiceVersion: SCEP service version

StepCA:
  - ServerUrl: Step-CA server URL
  - ProvisionerName: Provisioner name
  - ProvisionerPassword: Provisioner password
  - ValidityHours: Certificate validity in hours
  - RootCertificatePath: (Optional) Path to CA root certificate
```

## Environment-Specific Configuration

### Local Development (local.settings.json)

Create `src/StepCA.Intune.Functions/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Intune:AzureAppId": "00000000-0000-0000-0000-000000000000",
    "Intune:AzureAppSecret": "your-secret-here",
    "Intune:TenantId": "00000000-0000-0000-0000-000000000000",
    "Intune:ProviderNameAndVersion": "StepCA-Intune-Connector/1.0",
    "Intune:IntuneResourceUrl": "https://graph.microsoft.com/.default",
    "Intune:GraphApiEndpoint": "https://graph.microsoft.com",
    "Intune:ServiceVersion": "2018-02-20",
    "StepCA:ServerUrl": "https://localhost:9000",
    "StepCA:ProvisionerName": "intune-provisioner",
    "StepCA:ProvisionerPassword": "your-provisioner-password",
    "StepCA:ValidityHours": "8760"
  }
}
```

### Azure Production (Application Settings)

Configure in Azure Portal → Function App → Configuration → Application Settings:

| Name | Value | Description |
|------|-------|-------------|
| `Intune:AzureAppId` | `<your-app-id>` | Azure AD App Registration ID |
| `Intune:AzureAppSecret` | `<your-secret>` | Use Key Vault reference: `@Microsoft.KeyVault(SecretUri=...)` |
| `Intune:TenantId` | `<your-tenant-id>` | Azure AD Tenant ID |
| `Intune:ProviderNameAndVersion` | `StepCA-Intune-Connector/1.0` | Connector identifier |
| `StepCA:ServerUrl` | `https://ca.example.com:9000` | Step-CA server URL |
| `StepCA:ProvisionerName` | `intune-provisioner` | Step-CA provisioner name |
| `StepCA:ProvisionerPassword` | `<provisioner-password>` | Use Key Vault reference |
| `StepCA:ValidityHours` | `8760` | Certificate validity (1 year = 8760 hours) |

## Azure Key Vault Integration

### Storing Secrets in Key Vault

1. Create an Azure Key Vault
2. Add secrets:
   - `IntuneAzureAppSecret`
   - `StepCAProvisionerPassword`

3. Grant Function App access:
   - Enable Managed Identity on Function App
   - Grant "Key Vault Secrets User" role to the managed identity

4. Reference secrets in Application Settings:
   ```
   Intune:AzureAppSecret = @Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/IntuneAzureAppSecret/)
   StepCA:ProvisionerPassword = @Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/StepCAProvisionerPassword/)
   ```

## Azure AD App Registration

### Required API Permissions

In Azure AD App Registration, add Microsoft Graph API permissions:

1. **Application Permissions:**
   - `DeviceManagementConfiguration.ReadWrite.All`
   - `DeviceManagementManagedDevices.ReadWrite.All`

2. **Grant Admin Consent** for the permissions

### Create Client Secret

1. Go to Certificates & secrets
2. New client secret
3. Note the secret value (it won't be shown again)
4. Store in Key Vault

## Step-CA Configuration

### Create Provisioner

On your Step-CA server:

```bash
# Create a JWK provisioner for the connector
step ca provisioner add intune-provisioner \
  --type JWK \
  --create

# Or create a password-based provisioner
step ca provisioner add intune-provisioner \
  --type password \
  --password-file provisioner-password.txt
```

### Configure Certificate Templates (Optional)

Create custom certificate templates in Step-CA for Intune devices:

```json
{
  "subject": {
    "commonName": "{{ .Subject.CommonName }}",
    "organization": ["Your Organization"]
  },
  "sans": ["{{ .Subject.CommonName }}"],
  "keyUsage": ["digitalSignature", "keyEncipherment"],
  "extKeyUsage": ["clientAuth"]
}
```

## Intune Configuration

### SCEP Certificate Profile

1. In Intune admin center, navigate to:
   - Devices → Configuration profiles → Create profile

2. Select:
   - Platform: Windows 10 and later (or iOS/iPadOS, Android, etc.)
   - Profile type: SCEP certificate

3. Configure SCEP settings:
   - **SCEP Server URLs**: `https://your-function-app.azurewebsites.net/api/scep/pkiclient.exe`
   - **Subject name format**: CN={{UserPrincipalName}}
   - **Subject alternative name**: UPN={{UserPrincipalName}}
   - **Certificate validity period**: 1 year
   - **Key storage provider**: Enroll to Trusted Platform Module (TPM) if available

4. Assign to device groups

## Network Configuration

### Azure Function Network Settings

For enhanced security:

1. **Restrict inbound access:**
   - Enable Access Restrictions
   - Allow only Intune service IPs

2. **Outbound to Step-CA:**
   - If Step-CA is on-premises, use VNet Integration
   - Configure Private Endpoints if needed

### Firewall Rules

Ensure the following connectivity:

| Source | Destination | Port | Protocol | Purpose |
|--------|-------------|------|----------|---------|
| Intune service | Azure Function | 443 | HTTPS | SCEP requests |
| Azure Function | Step-CA server | 9000 | HTTPS | Certificate issuance |
| Azure Function | Microsoft Graph API | 443 | HTTPS | Validation & notification |

## Monitoring and Logging

### Application Insights

Enable Application Insights for the Function App:

1. Create Application Insights resource
2. Link to Function App
3. Configure log levels in `host.json`

### Log Queries

Sample Kusto queries for monitoring:

```kusto
// Failed certificate requests
traces
| where message contains "Failed to process certificate request"
| project timestamp, message, severityLevel

// Certificate issuance rate
requests
| where name == "ProcessScepRequest"
| summarize count() by bin(timestamp, 1h)
```

## Troubleshooting

### Common Configuration Issues

1. **"Failed to acquire access token"**
   - Verify AzureAppId, AzureAppSecret, and TenantId
   - Check Azure AD app permissions
   - Ensure admin consent is granted

2. **"Step-CA returned error"**
   - Verify ServerUrl is accessible
   - Check provisioner name and password
   - Review Step-CA logs

3. **"Validation failed"**
   - Verify Intune configuration
   - Check that SCEP profile points to correct URL
   - Review transaction ID in logs

### Testing Configuration

Test the deployment:

```bash
# Health check
curl https://your-function-app.azurewebsites.net/api/health

# Expected response:
# {"status":"healthy","service":"StepCA Intune SCEP Connector","version":"1.0.0"}
```

## Security Best Practices

1. **Always use Key Vault** for secrets
2. **Enable Managed Identity** instead of using explicit credentials where possible
3. **Restrict network access** using Access Restrictions
4. **Monitor certificate operations** via Application Insights
5. **Rotate secrets regularly** (e.g., every 90 days)
6. **Use RBAC** to control access to Function App and Key Vault
7. **Enable diagnostic logs** for audit trail

## Performance Tuning

### Function App Settings

For high-volume environments:

```json
{
  "functionTimeout": "00:05:00",
  "maxConcurrentRequests": 100,
  "maxOutstandingRequests": 200,
  "routePrefix": "api"
}
```

### Step-CA Optimization

- Use Step-CA in HA configuration
- Consider load balancing multiple Step-CA instances
- Monitor Step-CA performance metrics

## Maintenance

### Regular Tasks

1. **Monitor certificate issuance** - Review Application Insights daily
2. **Check error rates** - Set up alerts for failed requests
3. **Review logs** - Investigate any validation failures
4. **Update secrets** - Rotate credentials quarterly
5. **Update connector** - Deploy new versions with minimal downtime

### Backup

Ensure backups of:
- Azure Key Vault secrets
- Step-CA configuration
- Function App configuration
- Intune SCEP profiles

## Support

For issues and questions:
- Check Application Insights logs
- Review Step-CA documentation: https://smallstep.com/docs/step-ca
- Consult Intune SCEP documentation: https://docs.microsoft.com/mem/intune/protect/certificates-scep-configure
