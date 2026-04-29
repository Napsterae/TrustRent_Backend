$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot

Write-Host "Limpando Base de Dados TrustRent..." -ForegroundColor Cyan

# Parar o backend se estiver a correr (opcional, mas recomendado)
# Get-Process TrustRent.Api -ErrorAction SilentlyContinue | Stop-Process -Force

function Invoke-DotNetEf {
	param(
		[Parameter(Mandatory = $true)]
		[string[]]$Arguments
	)

	& dotnet ef @Arguments
	if ($LASTEXITCODE -ne 0) {
		throw "dotnet ef falhou com exit code $LASTEXITCODE. Argumentos: $($Arguments -join ' ')"
	}
}

$contexts = @(
	@{ Name = "IdentityDbContext"; Project = "TrustRent.Modules.Identity" },
	@{ Name = "CatalogDbContext"; Project = "TrustRent.Modules.Catalog" },
	@{ Name = "LeasingDbContext"; Project = "TrustRent.Modules.Leasing" },
	@{ Name = "CommunicationsDbContext"; Project = "TrustRent.Modules.Communications" },
	@{ Name = "AdminDbContext"; Project = "TrustRent.Modules.Admin" }
)

# Um drop chega porque todos os DbContexts usam a mesma base de dados PostgreSQL.
Invoke-DotNetEf -Arguments @(
	"database", "drop", "--force",
	"--project", "TrustRent.Modules.Identity",
	"--startup-project", "TrustRent.Api",
	"--context", "IdentityDbContext"
)

foreach ($context in $contexts) {
	Write-Host "Aplicando migrações: $($context.Name)..." -ForegroundColor Cyan
	Invoke-DotNetEf -Arguments @(
		"database", "update",
		"--project", $context.Project,
		"--startup-project", "TrustRent.Api",
		"--context", $context.Name
	)
}

Write-Host "Base de Dados Recriada com Sucesso!" -ForegroundColor Green
Write-Host "Agora, inicia o Backend para correr o Seeder automático." -ForegroundColor Yellow
