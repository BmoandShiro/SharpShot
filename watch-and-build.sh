#!/bin/bash

# SharpShot Live Development File Watcher
# This script watches for ALL file changes and triggers rebuilds

echo "=== SharpShot Live Development Watcher ==="
echo "Watching for all file changes including assets..."

# Function to build the project
build_project() {
    echo "🔄 File change detected! Building project..."
    dotnet build --project /app/SharpShot.csproj --configuration Debug
    if [ $? -eq 0 ]; then
        echo "✅ Build successful!"
        echo "📝 You can now run 'dotnet run' to test your changes"
    else
        echo "❌ Build failed!"
    fi
    echo "👀 Watching for more changes..."
}

# Initial build
echo "🔨 Performing initial build..."
dotnet build --project /app/SharpShot.csproj --configuration Debug
if [ $? -eq 0 ]; then
    echo "✅ Initial build successful!"
else
    echo "❌ Initial build failed!"
fi

echo "👀 Starting file watcher..."
echo "Watching for changes in:"
echo "  - Source files (*.cs, *.xaml, *.csproj)"
echo "  - Asset files (*.png, *.jpg, *.ico, *.json)"
echo "  - Configuration files"
echo ""

# Use inotifywait to watch for all file changes
# Watch for common file types that might affect the build
inotifywait -m -r /app \
    -e modify,create,delete,move \
    --include ".*\.(cs|csproj|xaml|png|jpg|jpeg|ico|json|xml|txt|md|ps1|bat)$" \
    --exclude "bin|obj|\.git|\.vs" |
while read path action file; do
    echo "📁 Change detected: $action $path$file"
    build_project
done 