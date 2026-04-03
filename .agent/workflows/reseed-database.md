---
description: Como fazer reset e reseed da base de dados para testes
---

# Reset e Reseed da Base de Dados

Este workflow elimina e recria as bases de dados e popula-as com dados de teste.

## Pré-requisitos
- Backend compilado sem erros
- PostgreSQL a correr

## Passos

1. Navega para a pasta do backend:
```
cd f:\AzureProjects\TrustRent\TrustRent_Backend
```

// turbo
2. Eliminar e recriar a base de dados do Identity:
```
dotnet ef database drop --force --context IdentityDbContext -p TrustRent.Modules.Identity/TrustRent.Modules.Identity.csproj -s TrustRent.Api/TrustRent.Api.csproj
```

// turbo
3. Eliminar e recriar a base de dados do Catalog:
```
dotnet ef database drop --force --context CatalogDbContext -p TrustRent.Modules.Catalog/TrustRent.Modules.Catalog.csproj -s TrustRent.Api/TrustRent.Api.csproj
```

// turbo
4. Aplicar migrações do Identity:
```
dotnet ef database update --context IdentityDbContext -p TrustRent.Modules.Identity/TrustRent.Modules.Identity.csproj -s TrustRent.Api/TrustRent.Api.csproj
```

// turbo
5. Aplicar migrações do Catalog:
```
dotnet ef database update --context CatalogDbContext -p TrustRent.Modules.Catalog/TrustRent.Modules.Catalog.csproj -s TrustRent.Api/TrustRent.Api.csproj
```

6. Iniciar o backend (o seeding é executado automaticamente em Development):
```
dotnet run --project TrustRent.Api
```

## Utilizadores de Teste

| Role | Email | Password | ID |
|------|-------|----------|----|
| Senhorio (verificado) | carlos.mendes@email.pt | TrustRent2026! | 11111111-... |
| Inquilina (parcial) | ana.ferreira@email.pt | TrustRent2026! | 22222222-... |
| Inquilino (sem docs) | miguel.costa@email.pt | TrustRent2026! | 33333333-... |

## Dados de Teste Criados

### Imóveis (propriedade do Carlos)
- **T2 Renovado no Chiado** — 1200€/mês, público, com 3 imagens
- **T1 Moderno em Arroios** — 850€/mês, público, com 1 imagem

### Candidaturas
- **Ana → T2 Chiado** — Estado: `Pending` (com 3 datas propostas)
- **Miguel → T1 Arroios** — Estado: `VisitCounterProposed` (senhorio já contra-propôs)
