Write-Host "Limpando Base de Dados TrustRent..." -ForegroundColor Cyan

# Parar o backend se estiver a correr (opcional, mas recomendado)
# Get-Process TrustRent.Api -ErrorAction SilentlyContinue | Stop-Process -Force

# Correr o drop da BD usando EF Tools (Um chega pois usam a mesma BD)
# Mas especificamos o contexto para evitar ambiguidades
dotnet ef database drop --force --project TrustRent.Modules.Identity --startup-project TrustRent.Api --context IdentityDbContext

# Correr as migrações para recriar as tabelas (Aqui é obrigatório o context se houver mais que um)
dotnet ef database update --project TrustRent.Modules.Identity --startup-project TrustRent.Api --context IdentityDbContext
dotnet ef database update --project TrustRent.Modules.Catalog --startup-project TrustRent.Api --context CatalogDbContext

Write-Host "Base de Dados Recriada com Sucesso!" -ForegroundColor Green
Write-Host "Agora, inicia o Backend para correr o Seeder automático." -ForegroundColor Yellow
