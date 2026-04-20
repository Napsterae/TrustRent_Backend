# 🏢 TrustRent — Backend API

![.NET 8](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)
![EF Core](https://img.shields.io/badge/EF_Core-38B2AC?style=for-the-badge&logo=nuget&logoColor=white)
![Hangfire](https://img.shields.io/badge/Hangfire-Jobs-FF7139?style=for-the-badge)
![Stripe](https://img.shields.io/badge/Stripe-635BFF?style=for-the-badge&logo=stripe&logoColor=white)
![Gemini AI](https://img.shields.io/badge/Gemini_AI-4285F4?style=for-the-badge&logo=google&logoColor=white)

> API RESTful da plataforma TrustRent. Arquitetura modular com .NET 8, PostgreSQL, Hangfire e integrações com Stripe, Gemini AI e Cloudinary.

---

## ✨ Funcionalidades Principais

### 🛡️ Identity & Autenticação
- Registo e login com JWT (Bearer Token)
- Perfis de utilizador com Trust Score
- Verificação KYC: Cartão de Cidadão e Certidão de Não Dívida via Gemini AI (extração de NIF, nome, validade)
- Upload de avatar via Cloudinary com conversão automática para WebP (ImageSharp)

### 🏡 Catálogo de Imóveis
- CRUD completo de imóveis com imagens, comodidades e periodicidades
- Listagem pública com filtros (tipologia, preço, localização, regime)
- Candidaturas com histórico de eventos e sistema de visitas
- Jobs recorrentes (Hangfire) para expiração automática de candidaturas pendentes

### 📄 Arrendamento (Leasing)
- Ciclo de vida completo do contrato: criação → assinatura → ativo → renovação/rescisão
- Assinatura digital via Chave Móvel Digital (CMD)
- Registo AT: validação do documento de registo nas Finanças por Gemini AI (NIF + nome do senhorio)
- Notificações de renovação com prazos legais (NRAU)
- Denúncia antecipada e atualização de renda (coeficiente INE)
- Histórico completo de eventos por contrato

### 💳 Pagamentos (Stripe)
- Pagamento de rendas via Stripe PaymentIntents
- Stripe Connect para recebimento direto por senhorios
- Histórico de pagamentos e geração de recibos
- Divisão de pagamento entre co-inquilinos

### 🔧 Manutenção & Tickets
- Criação e gestão de tickets por imóvel/contrato
- Comentários e anexos em tickets
- Estados: aberto, em progresso, resolvido, fechado

### 💬 Comunicações
- Chat em tempo real via SignalR
- Centro de notificações persistentes
- Comunicações legais com registo de data, hora e IP (valor probatório)

### 🤖 IA (Gemini)
- Serviço genérico `GeminiDocumentService` para extração de dados de documentos
- Prompts especializados: Cartão de Cidadão, Certidão de Não Dívida, Registo AT
- Validação de autenticidade, qualidade de imagem e campos extraídos

---

## 🏗️ Arquitetura

Solução modular com separação clara de responsabilidades:

```
TrustRent.sln
├── TrustRent.Api                    # Minimal API — endpoints, DI, middleware
│   └── Endpoints/
│       ├── AuthEndpoints.cs
│       ├── UserEndpoints.cs
│       ├── PropertyEndpoints.cs
│       ├── ApplicationEndpoints.cs
│       ├── LeaseEndpoints.cs
│       ├── TicketEndpoints.cs
│       ├── StripeEndpoints.cs
│       └── ReviewEndpoints.cs
│
├── TrustRent.Modules.Identity       # Utilizadores, KYC, JWT
├── TrustRent.Modules.Catalog        # Imóveis, candidaturas, jobs Hangfire
├── TrustRent.Modules.Leasing        # Contratos, pagamentos, tickets, reviews
├── TrustRent.Modules.Communications # Notificações, chat (SignalR), emails
├── TrustRent.Shared                 # Serviços transversais (Gemini, Cloudinary, R2, email)
└── TrustRent.Tests                  # Testes unitários por módulo
```

Cada módulo tem o seu próprio `DbContext`, `Migrations`, `Models`, `Services` e `Repositories`.

---

## 🛠️ Stack Tecnológico

| Componente | Tecnologia |
|---|---|
| Framework | ASP.NET Core 8 (Minimal API) |
| Base de Dados | PostgreSQL |
| ORM | Entity Framework Core 8 |
| Jobs Agendados | Hangfire (PostgreSQL) |
| Tempo Real | SignalR |
| IA / OCR | Google Gemini API |
| Pagamentos | Stripe (PaymentIntents + Connect) |
| Storage de Imagens | Cloudinary + Cloudflare R2 |
| Processamento de Imagem | SixLabors.ImageSharp |
| Autenticação | JWT Bearer |

---

## 🚀 Como Executar Localmente

### Pré-requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/) a correr localmente
- Conta Cloudinary (para uploads de imagens)
- Chave API do Google Gemini
- Conta Stripe (para pagamentos)

### 1. Configurar `appsettings.Development.json`

Cria o ficheiro `TrustRent.Api/appsettings.Development.json` (não versionado) com:

```json
{
  "ConnectionStrings": {
    "PostgresConnection": "Host=localhost;Database=trustrent_db;Username=trustrent_admin;Password=<PASSWORD>"
  },
  "JwtSettings": {
    "SecretKey": "<CHAVE_JWT_LONGA>",
    "Issuer": "TrustRentApi",
    "Audience": "TrustRentFrontend"
  },
  "CloudinarySettings": {
    "CloudName": "<CLOUD_NAME>",
    "ApiKey": "<API_KEY>",
    "ApiSecret": "<API_SECRET>"
  },
  "Gemini": {
    "ApiKey": "<GEMINI_API_KEY>",
    "Model": "gemini-2.0-flash"
  },
  "Stripe": {
    "SecretKey": "<STRIPE_SECRET_KEY>",
    "WebhookSecret": "<STRIPE_WEBHOOK_SECRET>"
  }
}
```

### 2. Aplicar Migrações

```bash
cd TrustRent_Backend
dotnet ef database update --project TrustRent.Modules.Identity --startup-project TrustRent.Api
dotnet ef database update --project TrustRent.Modules.Catalog --startup-project TrustRent.Api
dotnet ef database update --project TrustRent.Modules.Leasing --startup-project TrustRent.Api
dotnet ef database update --project TrustRent.Modules.Communications --startup-project TrustRent.Api
```

### 3. Executar a API

```bash
cd TrustRent_Backend
dotnet run --project TrustRent.Api\TrustRent.Api.csproj
```

A API fica disponível em `http://localhost:5281`.

Os dados de seed são aplicados automaticamente na primeira execução (20 utilizadores, 70 imóveis, 5 contratos).

---

## 🧪 Testes

```bash
cd TrustRent_Backend
dotnet test TrustRent.Tests\TrustRent.Tests.csproj
```

Os testes estão organizados por módulo em `TrustRent.Tests/` (Api, Catalog, Communications, Identity, Leasing, Shared).