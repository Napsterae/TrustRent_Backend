# Define o caminho base como a pasta onde o script reside
$basePath = $PSScriptRoot
# O ficheiro de saída será criado na raiz do projeto (um nível acima)
$outputFile = Join-Path $basePath "..\ContextoBackend.txt"

# Apagar o ficheiro anterior se existir
if (Test-Path $outputFile) { Remove-Item $outputFile }

# Padrões a ignorar (exclusão agressiva para poupar contexto)
$excludePatterns = @("*node_modules*", "*.git*", "*.vs*", "*bin*", "*obj*", "*.idea*", "*Migrations*", "ContextoBackend.txt")

# Extensões de ficheiros que nos interessam
$includeExtensions = @(".cs", ".csproj", ".sln", ".json", ".yml", ".yaml")

Write-Host "A gerar o ficheiro de contexto Backend em: $basePath" -ForegroundColor Cyan

# 1. ESTRUTURA
Add-Content -Path $outputFile -Value "======================================================"
Add-Content -Path $outputFile -Value "ESTRUTURA DO PROJETO"
Add-Content -Path $outputFile -Value "======================================================"

Get-ChildItem -Path $basePath -Recurse | Where-Object { 
    $path = $_.FullName
    $exclude = $false
    foreach ($pattern in $excludePatterns) {
        if ($path -like $pattern) { $exclude = $true; break }
    }
    !$exclude
} | ForEach-Object {
    $relativePath = $_.FullName.Replace($basePath, "TrustRent_Backend")
    Add-Content -Path $outputFile -Value $relativePath
}

Add-Content -Path $outputFile -Value "`n`n"

# 2. CONTEÚDO
Add-Content -Path $outputFile -Value "======================================================"
Add-Content -Path $outputFile -Value "CONTEÚDO DOS FICHEIROS"
Add-Content -Path $outputFile -Value "======================================================"

Get-ChildItem -Path $basePath -File -Recurse | Where-Object { 
    $path = $_.FullName
    $ext = $_.Extension.ToLower()
    
    $exclude = $false
    foreach ($pattern in $excludePatterns) {
        if ($path -like $pattern) { $exclude = $true; break }
    }
    
    ($includeExtensions -contains $ext) -and (!$exclude)
} | ForEach-Object {
    $relativePath = $_.FullName.Replace($basePath, "TrustRent_Backend")
    
    Add-Content -Path $outputFile -Value "`n`n================================================"
    Add-Content -Path $outputFile -Value "FICHEIRO: $relativePath"
    Add-Content -Path $outputFile -Value "================================================"
    
    $content = Get-Content $_.FullName -Raw
    if ($null -ne $content) {
        Add-Content -Path $outputFile -Value $content
    }
}

Write-Host "Sucesso! Ficheiro gerado em: $outputFile" -ForegroundColor Green
