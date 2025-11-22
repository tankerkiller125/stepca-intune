# Use the official Azure Functions .NET 8 image as base
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot
EXPOSE 80

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["StepCA.Intune.sln", "./"]
COPY ["src/StepCA.Intune.Functions/StepCA.Intune.Functions.csproj", "src/StepCA.Intune.Functions/"]
COPY ["src/StepCA.Intune.ScepValidation/StepCA.Intune.ScepValidation.csproj", "src/StepCA.Intune.ScepValidation/"]

# Restore dependencies
RUN dotnet restore "src/StepCA.Intune.Functions/StepCA.Intune.Functions.csproj"

# Copy the rest of the code
COPY src/ src/

# Build the Functions project
WORKDIR "/src/src/StepCA.Intune.Functions"
RUN dotnet build "StepCA.Intune.Functions.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "StepCA.Intune.Functions.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .

# Set environment variables for Azure Functions
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
