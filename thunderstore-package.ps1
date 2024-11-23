function Remove-ExistingPackageDirectory {
    param ([string]$path)
    
    if (Test-Path -Path $path) {
        Remove-Item -Path $path -Recurse -Force
        Write-Host "Removed existing directory: $path"
    }
}

function New-PackageDirectory {
    param ([string]$path)
    
    if (-Not (Test-Path -Path $path)) {
        New-Item -ItemType Directory -Path $path
        Write-Host "Created directory: $path"
    }
    else {
        Write-Host "Directory already exists: $path"
    }
}

function Copy-File {
    param (
        [string]$source,
        [string]$destination
    )
    
    if (Test-Path -Path $source) {
        Copy-Item -Path $source -Destination $destination
        Write-Host "Copied $source to $destination"
        return $true
    }
    Write-Host "Source file not found: $source"
    return $false
}

function Copy-ReadmeFile {
    param (
        [string]$source,
        [string]$destination
    )
    
    if (Test-Path -Path $source) {
        # Read file line by line
        $readmeLines = Get-Content -Path $source
        # Select lines that don't contain icon.png
        $filteredLines = $readmeLines | Where-Object { $_ -notmatch "icon\.png" }
        # Write output
        Set-Content -Path $destination -Value $filteredLines -Encoding UTF8
        Write-Host "Copied $source to $destination and removed icon references"
        return $true
    }
    Write-Host "Source file not found: $source"
    return $false
}

function Get-ProjectInfo {
    param ([string]$csprojPath)
    
    if (-Not (Test-Path -Path $csprojPath)) {
        Write-Host "Project file not found: $csprojPath"
        return $null
    }
    
    try {
        [xml]$csproj = Get-Content -Path $csprojPath -Raw
        $propertyGroup = $csproj.Project.PropertyGroup | Where-Object { $_.TargetFramework -or $_.Product -or $_.Version -or $_.Description -or $_.RepositoryUrl } | Select-Object -First 1

        return @{
            ProjectName     = $propertyGroup.Product ?? ""
            Version         = $propertyGroup.Version ?? "1.0.0"
            Description     = $propertyGroup.Description ?? ""
            WebsiteUrl      = $propertyGroup.RepositoryUrl ?? ""
            TargetFramework = $propertyGroup.TargetFramework ?? "netstandard2.1"
            AssemblyName    = $propertyGroup.AssemblyName ?? $propertyGroup.Product
        }
    }
    catch {
        Write-Host "Error parsing csproj file: $_"
        return $null
    }
}

function New-ManifestFile {
    param (
        [string]$path,
        [hashtable]$projectInfo
    )
    
    if (-Not $projectInfo.Description) { $projectInfo.Description = "No description provided." }
    if (-Not $projectInfo.WebsiteUrl) { $projectInfo.WebsiteUrl = "No URL provided." }
    
    $manifestContent = @"
{
    "name": "$($projectInfo.ProjectName)",
    "version_number": "$($projectInfo.Version)",
    "website_url": "$($projectInfo.WebsiteUrl)",
    "description": "$($projectInfo.Description)",
    "dependencies": ["BepInEx-BepInExPack-5.4.2100"]
}
"@
    Set-Content -Path $path -Value $manifestContent -Encoding UTF8
    Write-Host "Created manifest.json"
}

function New-ZipPackage {
    param (
        [string]$sourceDir,
        [string]$projectName
    )
    
    $zipFileName = "${projectName}.zip"
    
    try {
        Compress-Archive -Path "$sourceDir/*" -DestinationPath $zipFileName -Force
        Write-Host "Created package as $zipFileName"
        return $true
    }
    catch {
        Write-Host "Error creating zip file: $_"
        return $false
    }
}

# Main process
$packageDir = "./thunderstore-package"

Remove-ExistingPackageDirectory -path $packageDir

# Create package directory
New-PackageDirectory -path $packageDir

# Get project info and create manifest
$projectInfo = Get-ProjectInfo -csprojPath "./SulfurRecipeBook.csproj"
if ($projectInfo) {
    New-ManifestFile -path "$packageDir/manifest.json" -projectInfo $projectInfo
}
else {
    Write-Host "Failed to get project information"
    exit 1
}

# Copy icon, DLL and README
Copy-File -source "./docs/thunderstore-icon.png" -destination "$packageDir/icon.png"
Copy-File -source "./bin/Release/$($projectInfo.TargetFramework)/$($projectInfo.AssemblyName).dll" -destination "$packageDir/$($projectInfo.AssemblyName).dll"
Copy-ReadmeFile -source "./README.md" -destination "$packageDir/README.md"

New-ZipPackage -sourceDir $packageDir -projectName $packageDir