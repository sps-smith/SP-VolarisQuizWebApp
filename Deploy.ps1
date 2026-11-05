# Deploy.ps1
# Deploys the Volaris 101 web app to Azure App Service using Azure CLI
# Run from the solution root: .\Deploy.ps1
# Requires: Azure CLI installed (https://aka.ms/installazurecliwindows)
#           Run 'az login' first if not already logged in

$ResourceGroup  = "volarisquizweb-rg"        # <-- update if different
$AppName        = "volarisquizweb-ade7e9gaawbfbdhg"
$ProjectPath    = ".\src\Volaris101.Web\Volaris101.Web.csproj"
$PublishFolder  = ".\publish_output"

Write-Host "Building and publishing..." -ForegroundColor Cyan

# Clean previous output
if (Test-Path $PublishFolder) { Remove-Item $PublishFolder -Recurse -Force }

# Build and publish
dotnet publish $ProjectPath -c Release -o $PublishFolder --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit 1 }

Write-Host "Build succeeded. Zipping..." -ForegroundColor Cyan

# Zip the output
$ZipPath = ".\publish_output.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path "$PublishFolder\*" -DestinationPath $ZipPath

Write-Host "Deploying to Azure..." -ForegroundColor Cyan

# Stop app to release file locks
az webapp stop --name $AppName --resource-group $ResourceGroup

Start-Sleep -Seconds 5

# Deploy
az webapp deploy `
    --name $AppName `
    --resource-group $ResourceGroup `
    --src-path $ZipPath `
    --type zip `
    --timeout 300

if ($LASTEXITCODE -ne 0) {
    Write-Host "Deployment failed!" -ForegroundColor Red
    az webapp start --name $AppName --resource-group $ResourceGroup
    exit 1
}

# Start app
az webapp start --name $AppName --resource-group $ResourceGroup

# Clean up
Remove-Item $PublishFolder -Recurse -Force
Remove-Item $ZipPath -Force

Write-Host "Deployment complete!" -ForegroundColor Green
Write-Host "URL: https://$AppName.canadacentral-01.azurewebsites.net" -ForegroundColor Cyan
