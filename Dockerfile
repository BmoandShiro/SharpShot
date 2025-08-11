# Multi-stage build for WPF application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set working directory
WORKDIR /src

# Copy project file
COPY SharpShot.csproj ./

# Restore dependencies
RUN dotnet restore SharpShot.csproj

# Copy source code
COPY . ./

# Build the application for Windows
RUN dotnet build SharpShot.csproj --no-restore --configuration Release -p:Platform=x64

# Publish the application for Windows
RUN dotnet publish SharpShot.csproj --no-restore --configuration Release --output /app/publish --runtime win-x64 --self-contained false

# Create a simple runtime image to copy the published files
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=build /app/publish ./publish

# The application will be copied to the host system
CMD ["echo", "Build complete. Application is ready to run on Windows host."] 