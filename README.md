# stepca-intune
A Step-CA Intune SCEP Connector

This project provides an integration between Microsoft Intune's SCEP (Simple Certificate Enrollment Protocol) management solution and Smallstep's Step-CA certificate authority.

## Overview

The StepCA Intune SCEP Connector enables organizations to use Step-CA as their certificate authority for Intune-managed devices. It validates SCEP certificate requests from Intune, issues certificates through Step-CA, and reports back to Intune.

## Architecture

The solution consists of two main components:

1. **StepCA.Intune.ScepValidation** - Core library containing:
   - Intune SCEP request validation
   - Step-CA certificate issuance
   - Certificate lifecycle management

2. **StepCA.Intune.Functions** - Azure Functions providing:
   - HTTP endpoints for SCEP requests
   - Health check endpoints
   - Certificate request processing

## Prerequisites

- .NET 8.0 SDK or later
- Azure subscription (for hosting Functions)
- Step-CA server
- Azure AD app registration with appropriate permissions
- Microsoft Intune subscription

## Configuration

### Azure AD App Registration

1. Register an application in Azure AD
2. Grant the following Microsoft Graph API permissions:
   - `DeviceManagementConfiguration.ReadWrite.All`
   - `DeviceManagementManagedDevices.ReadWrite.All`
3. Create a client secret
4. Note the Application (client) ID, Directory (tenant) ID, and client secret

### Step-CA Configuration

1. Set up your Step-CA instance
2. Create a provisioner for the connector
3. Note the CA server URL, provisioner name, and provisioner password

### Application Settings

Configure the following settings in `local.settings.json` (for local development) or Azure Function App Configuration (for production):

```json
{
  "KeyVault:VaultUrl": "https://your-vault.vault.azure.net/",
  "Intune:AzureAppId": "your-azure-app-id",
  "Intune:AzureAppSecret": "keyvault:IntuneAzureAppSecret",
  "Intune:TenantId": "your-tenant-id",
  "Intune:ProviderNameAndVersion": "StepCA-Intune-Connector/1.0",
  "StepCA:ServerUrl": "https://ca.example.com:9000",
  "StepCA:ProvisionerName": "your-provisioner-name",
  "StepCA:ProvisionerPassword": "kv:StepCAProvisionerPassword",
  "StepCA:ValidityHours": "8760"
}
```

**Note**: The connector supports optional Azure Key Vault integration for secure secret storage. Use `keyvault:SecretName` or `kv:SecretName` to reference Key Vault secrets. See [KEYVAULT.md](KEYVAULT.md) for complete setup instructions. To use plain secrets instead, omit `KeyVault:VaultUrl` and provide actual values.

## Building

```bash
# Build the entire solution
dotnet build

# Run tests
dotnet test
```

## Running Locally

```bash
# Navigate to the Functions project
cd src/StepCA.Intune.Functions

# Run the Functions host
func start
# Or use dotnet run for development
dotnet run
```

The Functions will be available at:
- Health check: `http://localhost:7071/api/health`
- SCEP request: `http://localhost:7071/api/scep/pkiclient.exe`
- Validation only: `http://localhost:7071/api/scep/validate`

## Deployment

### Deploy to Azure Functions

1. Create an Azure Function App (.NET 8, Isolated worker runtime)
2. Configure application settings with your credentials
3. Deploy using Visual Studio, VS Code, or Azure CLI:

```bash
func azure functionapp publish <your-function-app-name>
```

### Configure Intune

1. In Intune admin center, go to Devices > Configuration profiles
2. Create a new SCEP certificate profile
3. Configure the SCEP server URL to point to your Azure Function endpoint
4. Configure certificate parameters as needed

## API Endpoints

### POST /api/scep/pkiclient.exe

Process a SCEP certificate request.

**Request Body:**
```json
{
  "transactionId": "unique-transaction-id",
  "certificateRequest": "base64-encoded-csr",
  "caConfiguration": "default",
  "certificateAuthority": "StepCA"
}
```

**Response:**
```json
{
  "transactionId": "unique-transaction-id",
  "certificate": "PEM-encoded-certificate",
  "certificateChain": "PEM-encoded-chain",
  "serialNumber": "certificate-serial",
  "thumbprint": "certificate-thumbprint",
  "expirationDate": "2025-11-07T13:26:56Z",
  "issuingAuthority": "CN=Step-CA"
}
```

### POST /api/scep/validate

Validate a SCEP request without issuing a certificate.

### GET /api/health

Health check endpoint.

## Security Considerations

- **Use Azure Key Vault** for storing secrets (Azure AD client secret, Step-CA provisioner password) - see [KEYVAULT.md](KEYVAULT.md)
- Use managed identities where possible
- Enable function authentication (Function keys or Azure AD authentication)
- Restrict network access using Azure networking features
- Monitor and log all certificate operations
- Review the [Key Vault Integration Guide](KEYVAULT.md) for secure secret management

## Logging

The connector uses structured logging with Microsoft.Extensions.Logging. Configure Application Insights for production monitoring.

## Troubleshooting

### Common Issues

1. **Authentication failures**: Verify Azure AD app permissions and credentials
2. **Step-CA connection errors**: Check network connectivity and CA URL
3. **Validation failures**: Ensure Intune is properly configured and CSRs are valid

### Enable Debug Logging

Set logging level in `host.json`:
```json
{
  "logging": {
    "logLevel": {
      "StepCA.Intune": "Debug"
    }
  }
}
```

## License

See LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

