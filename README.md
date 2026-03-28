# 🏢 TrustRent API - Backend

![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![Entity Framework](https://img.shields.io/badge/EF_Core-38B2AC?style=for-the-badge&logo=nuget&logoColor=white)
![Google Cloud Vision](https://img.shields.io/badge/Google_Vision_AI-4285F4?style=for-the-badge&logo=google-cloud&logoColor=white)
![Clean Architecture](https://img.shields.io/badge/Architecture-Clean_&_Modular-FF7139?style=for-the-badge)

> A API RESTful que alimenta a plataforma TrustRent. Construída com foco em segurança, escalabilidade e Clean Architecture.

## ✨ Funcionalidades Principais

* **🛡️ Identity & Segurança:** Autenticação JWT com controlo de permissões e perfis de utilizador.
* **🧠 KYC & Validação de Documentos (OCR):** Integração com a Google Cloud Vision API para leitura automática, validação e extração de datas de Cartões de Cidadão e Certidões da AT.
* **☁️ Cloud Storage:** Serviço agnóstico de armazenamento de ficheiros (preparado para Cloudinary, AWS S3 ou Cloudflare R2) com conversão automática de imagens para WebP via `ImageSharp`.
* **🏗️ Arquitetura Modular:** Separação clara de responsabilidades através de módulos (Identity, Shared, Catalog).

## 🛠️ Tecnologias Utilizadas

* **Framework:** [.NET 8 / 9](https://dotnet.microsoft.com/) (ASP.NET Core Web API)
* **Linguagem:** C#
* **ORM:** Entity Framework Core
* **Processamento de Imagem:** SixLabors.ImageSharp
* **Integrações Externas:** Google.Cloud.Vision.V1, CloudinaryDotNet

## 🚀 Como Executar o Projeto Localmente

### 1. Pré-requisitos
* [.NET SDK](https://dotnet.microsoft.com/download) instalado.
* Uma base de dados SQL Server (ou configurada para SQLite localmente).
* Credenciais da Google Cloud Vision API e Cloudinary/S3.

### 2. Configuração de Variáveis de Ambiente (`appsettings.json`)
Cria ou atualiza o teu ficheiro `appsettings.Development.json` (não incluído no repositório por segurança) com as tuas chaves:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=TrustRentDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "JwtSettings": {
    "Secret": "A_TUA_CHAVE_SUPER_SECRETA_AQUI"
  },
  "GoogleCloud": {
    "CredentialsPath": "C:\\Caminho\\Seguro\\Para\\google-credentials.json"
  },
  "CloudinarySettings": {
    "CloudName": "teu_cloud_name",
    "ApiKey": "tua_api_key",
    "ApiSecret": "teu_api_secret"
  }
}