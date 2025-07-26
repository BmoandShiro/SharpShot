# Multi-stage build for WPF application
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build

# Set working directory
WORKDIR /src

# Copy project files
COPY SharpShot.csproj ./
COPY *.csproj ./

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Build the application
RUN dotnet build --no-restore --configuration Release

# Publish the application
RUN dotnet publish --no-restore --configuration Release --output /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0-windowsservercore-ltsc2022 AS runtime

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=build /app/publish ./

# Set entry point
ENTRYPOINT ["SharpShot.exe"] 