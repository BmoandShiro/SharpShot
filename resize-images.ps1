# Image Resizing Script for Microsoft Store Certification
# Resizes images to exact dimensions required by Microsoft

Write-Host "Resizing images for Microsoft Store certification..." -ForegroundColor Green

# Load System.Drawing assembly
Add-Type -AssemblyName System.Drawing

# Function to resize image
function Resize-Image {
    param(
        [string]$InputPath,
        [string]$OutputPath,
        [int]$Width,
        [int]$Height
    )
    
    try {
        $image = [System.Drawing.Image]::FromFile($InputPath)
        $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        
        # Set high quality interpolation
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        
        # Draw the resized image
        $graphics.DrawImage($image, 0, 0, $Width, $Height)
        
        # Save with high quality
        $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        
        # Clean up
        $graphics.Dispose()
        $bitmap.Dispose()
        $image.Dispose()
        
        Write-Host "✓ Resized $InputPath to ${Width}x${Height}" -ForegroundColor Green
    }
    catch {
        Write-Host "✗ Error resizing $InputPath : $_" -ForegroundColor Red
    }
}

# Resize Wide310x150Logo.png to exactly 310x150
if (Test-Path "Assets\Wide310x150Logo.png") {
    Resize-Image -InputPath "Assets\Wide310x150Logo.png" -OutputPath "Assets\Wide310x150Logo.png" -Width 310 -Height 150
} else {
    Write-Host "✗ Wide310x150Logo.png not found" -ForegroundColor Red
}

# Resize SplashScreen.png to exactly 620x300
if (Test-Path "Assets\SplashScreen.png") {
    Resize-Image -InputPath "Assets\SplashScreen.png" -OutputPath "Assets\SplashScreen.png" -Width 620 -Height 300
} else {
    Write-Host "✗ SplashScreen.png not found" -ForegroundColor Red
}

Write-Host "Image resizing completed!" -ForegroundColor Green
Write-Host "Check the Assets folder for resized images." -ForegroundColor Cyan
