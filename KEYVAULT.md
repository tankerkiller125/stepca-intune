# Azure Key Vault Integration Guide

## Overview

The StepCA Intune SCEP Connector supports optional integration with Azure Key Vault for secure secret management. This allows you to store sensitive credentials (like Azure AD client secrets and Step-CA provisioner passwords) in Key Vault instead of plain configuration.

## Benefits

- **Enhanced Security**: Secrets are stored in a centralized, secure vault
- **Access Control**: Fine-grained RBAC on who can access secrets
- **Audit Logging**: Track all secret access attempts
- **Secret Rotation**: Update secrets without redeploying the application
- **Compliance**: Meet regulatory requirements for secret storage

## Setup

### 1. Create Azure Key Vault

```bash
# Create resource group (if needed)
az group create --name rg-stepca-intune --location eastus

# Create Key Vault
az keyvault create \
  --name kv-stepca-intune-prod \
  --resource-group rg-stepca-intune \
  --location eastus
```

### 2. Store Secrets in Key Vault

```bash
# Store Intune Azure App Secret
az keyvault secret set \
  --vault-name kv-stepca-intune-prod \
  --name IntuneAzureAppSecret \
  --value "your-actual-secret-value"

# Store Step-CA Provisioner Password
az keyvault secret set \
  --vault-name kv-stepca-intune-prod \
  --name StepCAProvisionerPassword \
  --value "your-provisioner-password"
```

### 3. Configure Function App Access

#### Option A: Using Managed Identity (Recommended)

1. Enable System-Assigned Managed Identity on your Function App:

```bash
az functionapp identity assign \
  --name func-stepca-intune-prod \
  --resource-group rg-stepca-intune
```

2. Grant the managed identity access to Key Vault:

```bash
# Get the principal ID of the managed identity
PRINCIPAL_ID=$(az functionapp identity show \
  --name func-stepca-intune-prod \
  --resource-group rg-stepca-intune \
  --query principalId -o tsv)

# Grant Key Vault Secrets User role
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $PRINCIPAL_ID \
  --scope /subscriptions/{subscription-id}/resourceGroups/rg-stepca-intune/providers/Microsoft.KeyVault/vaults/kv-stepca-intune-prod
```

#### Option B: Using Access Policies (Legacy)

```bash
# Get the principal ID
PRINCIPAL_ID=$(az functionapp identity show \
  --name func-stepca-intune-prod \
  --resource-group rg-stepca-intune \
  --query principalId -o tsv)

# Set access policy
az keyvault set-policy \
  --name kv-stepca-intune-prod \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

### 4. Configure Application Settings

Add the Key Vault URL to your Function App configuration:

```bash
az functionapp config appsettings set \
  --name func-stepca-intune-prod \
  --resource-group rg-stepca-intune \
  --settings "KeyVault:VaultUrl=https://kv-stepca-intune-prod.vault.azure.net/"
```

## Configuration

### Using Key Vault References

The connector supports two ways to reference Key Vault secrets:

#### Method 1: Prefix Notation (Recommended)

Use `keyvault:` or `kv:` prefix followed by the secret name:

```json
{
  "KeyVault:VaultUrl": "https://your-vault.vault.azure.net/",
  "Intune:AzureAppSecret": "keyvault:IntuneAzureAppSecret",
  "StepCA:ProvisionerPassword": "kv:StepCAProvisionerPassword"
}
```

**Benefits:**
- Runtime resolution - secrets are fetched when needed
- Works with local development using DefaultAzureCredential
- Supports various authentication methods (Managed Identity, Azure CLI, VS, etc.)

#### Method 2: Plain Values

If Key Vault URL is not configured or you don't use the prefix, values are used as-is:

```json
{
  "Intune:AzureAppSecret": "my-actual-secret",
  "StepCA:ProvisionerPassword": "my-provisioner-password"
}
```

### Configuration Hierarchy

The connector resolves configuration in this order:
1. Check if `KeyVault:VaultUrl` is configured
2. If a value has `keyvault:` or `kv:` prefix, retrieve from Key Vault
3. Otherwise, use the value directly

## Local Development

### Using Azure CLI Authentication

1. Install Azure CLI and login:

```bash
az login
```

2. Configure your `local.settings.json`:

```json
{
  "Values": {
    "KeyVault:VaultUrl": "https://kv-stepca-intune-dev.vault.azure.net/",
    "Intune:AzureAppSecret": "keyvault:IntuneAzureAppSecret",
    "StepCA:ProvisionerPassword": "kv:StepCAProvisionerPassword"
  }
}
```

3. Ensure your Azure account has Key Vault access:

```bash
az keyvault set-policy \
  --name kv-stepca-intune-dev \
  --upn your-email@company.com \
  --secret-permissions get list
```

### Using Service Principal (CI/CD)

```bash
# Create service principal
az ad sp create-for-rbac \
  --name sp-stepca-intune-dev \
  --role "Key Vault Secrets User" \
  --scopes /subscriptions/{subscription-id}/resourceGroups/rg-stepca-intune/providers/Microsoft.KeyVault/vaults/kv-stepca-intune-dev

# Set environment variables
export AZURE_CLIENT_ID="<appId from output>"
export AZURE_CLIENT_SECRET="<password from output>"
export AZURE_TENANT_ID="<tenant from output>"
```

### Without Key Vault (Fallback)

Leave `KeyVault:VaultUrl` empty or omit it to disable Key Vault integration:

```json
{
  "Values": {
    "Intune:AzureAppSecret": "your-actual-secret",
    "StepCA:ProvisionerPassword": "your-actual-password"
  }
}
```

## Production Deployment

### Complete Configuration Example

Azure Function App Application Settings:

```
KeyVault:VaultUrl = https://kv-stepca-intune-prod.vault.azure.net/

Intune:AzureAppId = 00000000-0000-0000-0000-000000000000
Intune:AzureAppSecret = keyvault:IntuneAzureAppSecret
Intune:TenantId = 00000000-0000-0000-0000-000000000000
Intune:ProviderNameAndVersion = StepCA-Intune-Connector/1.0

StepCA:ServerUrl = https://ca.example.com:9000
StepCA:ProvisionerName = intune-provisioner
StepCA:ProvisionerPassword = kv:StepCAProvisionerPassword
StepCA:ValidityHours = 8760
```

### Infrastructure as Code (Bicep)

```bicep
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: 'kv-stepca-intune-prod'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
  }
}

resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: 'func-stepca-intune-prod'
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    siteConfig: {
      appSettings: [
        {
          name: 'KeyVault:VaultUrl'
          value: keyVault.properties.vaultUri
        }
        {
          name: 'Intune:AzureAppSecret'
          value: 'keyvault:IntuneAzureAppSecret'
        }
        {
          name: 'StepCA:ProvisionerPassword'
          value: 'kv:StepCAProvisionerPassword'
        }
      ]
    }
  }
}

resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, functionApp.id, 'Key Vault Secrets User')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}
```

## Troubleshooting

### Common Issues

#### 1. "Failed to retrieve secret from Key Vault"

**Cause**: Function App doesn't have permission to access Key Vault

**Solution**:
```bash
# Verify managed identity is enabled
az functionapp identity show \
  --name func-stepca-intune-prod \
  --resource-group rg-stepca-intune

# Check role assignments
az role assignment list \
  --assignee <principal-id> \
  --scope <key-vault-resource-id>
```

#### 2. "Key Vault integration is disabled"

**Cause**: `KeyVault:VaultUrl` is not configured or invalid

**Solution**: Verify the configuration:
```bash
az functionapp config appsettings list \
  --name func-stepca-intune-prod \
  --resource-group rg-stepca-intune \
  --query "[?name=='KeyVault:VaultUrl']"
```

#### 3. Local Development Authentication Fails

**Cause**: No valid Azure credentials available

**Solution**:
```bash
# Login with Azure CLI
az login

# Or set service principal credentials
export AZURE_CLIENT_ID="..."
export AZURE_CLIENT_SECRET="..."
export AZURE_TENANT_ID="..."
```

#### 4. "Secret not found in Key Vault"

**Cause**: Secret name mismatch or secret doesn't exist

**Solution**:
```bash
# List all secrets
az keyvault secret list --vault-name kv-stepca-intune-prod

# Check specific secret
az keyvault secret show \
  --vault-name kv-stepca-intune-prod \
  --name IntuneAzureAppSecret
```

### Debug Logging

Enable debug logging to see Key Vault operations:

```json
{
  "logging": {
    "logLevel": {
      "StepCA.Intune.ScepValidation.KeyVaultSecretProvider": "Debug"
    }
  }
}
```

Log messages will show:
- When Key Vault is enabled/disabled
- Secret retrieval attempts
- Success/failure of operations

## Security Best Practices

1. **Use Managed Identity**: Avoid storing credentials in configuration
2. **Principle of Least Privilege**: Grant only "get" and "list" permissions on secrets
3. **Enable Soft Delete**: Protect against accidental secret deletion
4. **Enable Purge Protection**: Prevent permanent secret deletion
5. **Monitor Access**: Review Key Vault audit logs regularly
6. **Rotate Secrets**: Implement regular secret rotation
7. **Separate Environments**: Use different Key Vaults for dev/test/prod

## Advanced Scenarios

### Multiple Key Vaults

If you need to retrieve secrets from multiple Key Vaults, you can:

1. Use Azure App Configuration with Key Vault references
2. Create multiple KeyVaultSecretProvider instances
3. Use Azure Functions' built-in Key Vault reference syntax: `@Microsoft.KeyVault(...)`

### Secret Versioning

The connector always retrieves the latest version of a secret. To use a specific version:

```json
{
  "Intune:AzureAppSecret": "keyvault:IntuneAzureAppSecret/abc123def456"
}
```

Note: You'll need to modify the `KeyVaultSecretProvider` to support version URIs.

### Caching Secrets

By default, secrets are retrieved on application startup. For runtime refresh:

1. Implement a caching mechanism with TTL
2. Use Azure App Configuration's dynamic refresh
3. Restart the Function App when secrets change

## Performance Considerations

- Secrets are resolved during application startup (singleton registration)
- No performance impact during request processing
- Key Vault calls are made once per application lifetime
- Consider caching if you need frequent secret rotation without restarts

## Cost

- Azure Key Vault costs: ~$0.03 per 10,000 transactions
- With startup-time resolution, typical cost: < $1/month
- Managed Identity: No additional cost

## Migration Guide

### From Plain Secrets to Key Vault

1. **Store secrets in Key Vault**:
   ```bash
   az keyvault secret set --vault-name <vault> --name <name> --value "<current-value>"
   ```

2. **Update configuration**:
   ```
   Before: "Intune:AzureAppSecret": "actual-secret"
   After:  "Intune:AzureAppSecret": "keyvault:IntuneAzureAppSecret"
   ```

3. **Add Key Vault URL**:
   ```
   "KeyVault:VaultUrl": "https://your-vault.vault.azure.net/"
   ```

4. **Grant permissions** to Function App

5. **Test and deploy**

### Rollback Plan

If issues occur:
1. Update configuration to use plain secrets
2. Remove or comment out `KeyVault:VaultUrl`
3. Restart Function App

## Testing

### Unit Testing

Mock the `KeyVaultSecretProvider`:

```csharp
var mockProvider = new Mock<KeyVaultSecretProvider>();
mockProvider
    .Setup(x => x.GetSecretAsync("keyvault:TestSecret", null))
    .ReturnsAsync("resolved-secret-value");
```

### Integration Testing

Use Azure Key Vault emulator or test environment with actual secrets.

## Support

For issues related to:
- Key Vault access: Check Azure AD and RBAC configuration
- Secret retrieval: Verify secret names and permissions
- Local development: Ensure Azure CLI or credentials are configured
- Production: Check managed identity and role assignments
