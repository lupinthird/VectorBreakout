# Rebuild checked-in MonoGame content (Content/Prebuilt/*.xnb).
# Run after editing Content.mgcb or effect shaders.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

dotnet tool restore
dotnet build -t:BuildPrebuiltContent -p:UsePrebuiltContent=false

Write-Host "Prebuilt content updated under Content/Prebuilt"
