# Nome do ficheiro de saída
$outputFile = "ContextoBackend.txt"

# Pastas a ignorar (não queremos código compilado nem ficheiros do git/IDE)
$excludeFolders = @(".git", ".vs", "bin", "obj", ".idea", "node_modules", "Migrations")

# Extensões de ficheiros que nos interessam
$includeExtensions = @(".cs", ".csproj", ".sln", ".json", ".yml", ".yaml")

# Apagar o ficheiro anterior se existir
if (Test-Path $outputFile) { Remove-Item $outputFile }

# Função auxiliar para verificar se a rota deve ser ignorada
function Is-Excluded ($path) {
    foreach ($folder in $excludeFolders) {
        if ($path -match "\\$folder\\" -or $path -match "\\$folder$") {
            return $true
        }
    }
    return $false
}

Write-Host "A gerar o ficheiro de contexto..." -ForegroundColor Cyan

# 1. GERAR A HIERARQUIA DE PASTAS E FICHEIROS
Add-Content -Path $outputFile -Value "======================================================"
Add-Content -Path $outputFile -Value "ESTRUTURA DO PROJETO"
Add-Content -Path $outputFile -Value "======================================================"

Get-ChildItem -Recurse | Where-Object { -not (Is-Excluded $_.FullName) } | Select-Object FullName | ForEach-Object {
    $relativePath = $_.FullName.Replace($PWD.Path + "\", "")
    Add-Content -Path $outputFile -Value $relativePath
}

Add-Content -Path $outputFile -Value "`n`n"

# 2. OBTER O CONTEÚDO DOS FICHEIROS RELEVANTES
Add-Content -Path $outputFile -Value "======================================================"
Add-Content -Path $outputFile -Value "CONTEÚDO DOS FICHEIROS"
Add-Content -Path $outputFile -Value "======================================================"

Get-ChildItem -File -Recurse | Where-Object { 
    $ext = $_.Extension.ToLower()
    ($includeExtensions -contains $ext) -and (-not (Is-Excluded $_.FullName))
} | ForEach-Object {
    $relativePath = $_.FullName.Replace($PWD.Path + "\", "")
    
    Add-Content -Path $outputFile -Value "`n`n================================================"
    Add-Content -Path $outputFile -Value "FICHEIRO: .\$relativePath"
    Add-Content -Path $outputFile -Value "================================================"
    
    # Lemos o ficheiro e adicionamos ao txt
    $content = Get-Content $_.FullName -Raw
    if ($null -ne $content) {
        Add-Content -Path $outputFile -Value $content
    }
}

Write-Host "Sucesso! O ficheiro '$outputFile' foi criado na raiz do projeto." -ForegroundColor Green