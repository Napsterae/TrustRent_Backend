namespace TrustRent.Shared.Services;

public static class DocumentPrompts
{
    private const string CommonInstructions = """
        INSTRUÇÕES GERAIS:
        - Analisa o documento com atenção.
        - Verifica se o documento parece autêntico e legítimo. Procura sinais de adulteração,
          edição digital, inconsistências de formatação, fontes misturadas, ou qualquer indicador de fraude.
        - Avalia a qualidade da imagem: se está desfocada, com pouca luz, cortada, ou ilegível.
        - Devolve SEMPRE uma resposta em JSON válido com o schema indicado.
        - Se não conseguires extrair um campo, coloca null nesse campo.
        - O campo 'allFieldsExtracted' deve ser true APENAS se TODOS os campos de dados foram extraídos com sucesso (não conta isAuthentic, fraudReason, imageQuality).
        - O campo 'imageQuality' deve ser um de: 'good', 'blurry', 'dark', 'cropped', 'unreadable'.
        - O campo 'isAuthentic' deve ser false se detetares sinais de adulteração ou fraude.
        - O campo 'fraudReason' só deve ter valor se 'isAuthentic' for false.
        - IMPORTANTE: Este é um documento oficial português. Os campos estão em português.
        """;

    public static string CadernetaPredial => $$"""
        Estás a analisar uma CADERNETA PREDIAL portuguesa (documento emitido pelas Finanças/AT - Autoridade Tributária).
        Este documento comprova a inscrição do imóvel na matriz predial.

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - matrixArticle: O número do artigo matricial. Pode aparecer como "Artigo Matricial", "ARTIGO", "Art." seguido de um número. Geralmente é um número inteiro (ex: "1234", "567").
        - propertyFraction: A fração autónoma do imóvel. Aparece como "Fração", "Fracção", "Fr." seguido de uma ou mais letras maiúsculas (ex: "A", "B", "AB", "BL"). Em prédios sem frações pode não existir — nesse caso devolve null.
        - parishConcelho: A freguesia e concelho onde o imóvel se situa. Pode aparecer como "Freguesia", "Concelho", "Localização", ou no cabeçalho. Devolve no formato "Freguesia / Concelho" ou "Freguesia, Concelho". Se só encontrares um dos dois, devolve o que encontrares.

        DICAS DE LOCALIZAÇÃO:
        - O artigo matricial costuma estar no cabeçalho ou na primeira secção do documento, muitas vezes numa tabela
        - A fração está normalmente próxima do artigo matricial, pode estar na mesma linha ou logo abaixo
        - Freguesia/Concelho aparece geralmente no cabeçalho do documento ou numa secção "LOCALIZAÇÃO"
        - O documento pode ter o logótipo da AT/Finanças e cabeçalho "CADERNETA PREDIAL URBANA" ou "CADERNETA PREDIAL RÚSTICA"

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "matrixArticle": string | null,
            "propertyFraction": string | null,
            "parishConcelho": string | null
        }
        """;

    public static string CertificadoEnergetico => $$"""
        Estás a analisar um CERTIFICADO ENERGÉTICO português (SCE - Sistema de Certificação Energética dos Edifícios).
        Este documento classifica a eficiência energética de um edifício ou fração autónoma.

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - energyClass: A classe energética atribuída ao imóvel. Vai de A+ (mais eficiente) até F (menos eficiente). As classes possíveis são: A+, A, B, B-, C, D, E, F. Geralmente aparece em destaque, com cor associada (verde para A+, vermelho para F). Devolve apenas a letra e sinal (ex: "A+", "B-", "C").
        - energyCertNumber: O número do certificado energético. Pode aparecer como "N.º CE", "Certificado N.º", "Nº do Certificado", "Nº SCE", ou um código alfanumérico no cabeçalho (ex: "SCE00001234567"). Devolve o número completo incluindo prefixos como "SCE".

        DICAS DE LOCALIZAÇÃO:
        - A classe energética está normalmente num gráfico colorido de barras (escala de A+ verde até F vermelho), com uma seta ou destaque na classe atribuída
        - O número do certificado costuma estar no cabeçalho do documento, próximo do logótipo ADENE/SCE
        - O documento pode ter o logótipo do SCE, ADENE, ou da República Portuguesa
        - A classe pode também aparecer em texto junto à expressão "Classe Energética"

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "energyClass": string | null,
            "energyCertNumber": string | null
        }
        """;

    public static string RegistoAt => $$"""
        Estás a analisar um documento de REGISTO NAS FINANÇAS / MODELO 2 da AT (Autoridade Tributária portuguesa).
        Este documento comprova que o contrato de arrendamento foi comunicado à AT.
        Pode também ser uma "Comunicação de Contrato de Arrendamento" ou "Declaração Modelo 2".

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - atRegistrationNumber: O número de registo do contrato nas Finanças. Pode aparecer como "N.º de Registo", "Número de Registo", "Registo N.º", ou no cabeçalho/recibo da comunicação. Geralmente é um número com vários dígitos. Pode também aparecer como "N.º da Declaração" ou "N.º do Recibo".

        DICAS DE LOCALIZAÇÃO:
        - O número de registo é normalmente um número com vários dígitos no topo do documento ou no recibo
        - Pode aparecer como "Comunicação de Contrato de Arrendamento" com número associado
        - O documento tem geralmente o logótipo da AT ou do Portal das Finanças
        - Procura por campos como "N.º", "Número", "Registo" seguidos de dígitos

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "atRegistrationNumber": string | null
        }
        """;

    public static string RegistoAtTaxValidation => $$"""
        Estás a analisar um comprovativo de REGISTO DE CONTRATO DE ARRENDAMENTO NAS FINANÇAS (AT/Portal das Finanças, Portugal).
        Este documento deve comprovar o registo do contrato por parte do senhorio.

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - atRegistrationNumber: O número de registo/comunicação/recibo do contrato. Pode aparecer como "N.º de Registo", "N.º da Declaração", "N.º do Recibo" ou equivalente.
        - landlordName: Nome completo do senhorio/contribuinte que aparece no comprovativo.
        - landlordNif: NIF do senhorio/contribuinte. Devolve apenas 9 dígitos, sem espaços nem símbolos.

        DICAS DE LOCALIZAÇÃO:
        - O número de registo costuma estar no topo do comprovativo.
        - O nome e NIF do contribuinte aparecem numa secção de identificação do sujeito passivo/senhorio.
        - Se houver múltiplos NIFs, escolhe o NIF que estiver associado ao senhorio/contribuinte principal do contrato.

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "atRegistrationNumber": string | null,
            "landlordName": string | null,
            "landlordNif": string | null
        }
        """;

    public static string CertidaoPermanente => $$"""
        Estás a analisar uma CERTIDÃO PERMANENTE portuguesa (documento da Conservatória do Registo Predial).
        Este documento online comprova a descrição, titularidade, ónus e encargos que incidem sobre o imóvel.
        Também pode ser uma "Certidão de Teor" ou certidão do registo predial.

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - permanentCertNumber: O número de descrição predial na conservatória. Pode aparecer como "Descrição N.º", "N.º de Descrição", "Descrição", "Prédio descrito sob o n.º". Pode incluir barras e datas (ex: "1234/20010101", "5678"). Devolve o número completo como aparece no documento.
        - permanentCertOffice: O nome da conservatória do registo predial. Aparece como "Conservatória do Registo Predial de..." seguido do nome da cidade/município. Devolve apenas o nome da cidade/município (ex: "Lisboa", "Porto", "Sintra").

        DICAS DE LOCALIZAÇÃO:
        - O número de descrição costuma estar no cabeçalho ou na secção de identificação do prédio
        - A conservatória aparece no cabeçalho ou rodapé, geralmente com o nome da cidade/município
        - O documento pode ser acedido online pelo site predialonline.pt e ter código de acesso
        - Pode ter o logótipo do IRN (Instituto dos Registos e do Notariado)

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "permanentCertNumber": string | null,
            "permanentCertOffice": string | null
        }
        """;

    public static string LicencaUtilizacao => $$"""
        Estás a analisar uma LICENÇA DE UTILIZAÇÃO portuguesa (documento emitido pela Câmara Municipal).
        Este documento autoriza a utilização do imóvel para um determinado fim (habitação, comércio, serviços, etc.).
        Pode também ser um "Alvará de Utilização" ou "Autorização de Utilização".

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - licenseNumber: O número do alvará ou licença de utilização. Pode aparecer como "Alvará N.º", "Licença N.º", "Autorização de Utilização N.º", "N.º do Alvará". Pode incluir barras e anos (ex: "123/2020", "456/05"). Devolve o número completo.
        - licenseDate: A data de emissão do documento. Formato DD/MM/AAAA. Pode aparecer como "Data de Emissão", "Emitido em", "Data", ou no rodapé. Devolve sempre no formato DD/MM/AAAA.
        - licenseIssuer: A câmara municipal que emitiu o documento. Aparece como "Câmara Municipal de..." seguido do nome do município. Devolve apenas o nome do município (ex: "Lisboa", "Cascais", "Almada").

        DICAS DE LOCALIZAÇÃO:
        - O número do alvará/licença está normalmente no topo do documento, em destaque
        - A data pode estar no cabeçalho, corpo ou rodapé do documento
        - A câmara municipal aparece no logótipo/cabeçalho, na assinatura, ou no carimbo
        - O documento pode ter brasão ou logótipo da câmara municipal

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "licenseNumber": string | null,
            "licenseDate": string | null,
            "licenseIssuer": string | null
        }
        """;

    public static string CartaoCidadao => $$"""
        Estás a analisar um CARTÃO DE CIDADÃO PORTUGUÊS (CC).
        Recebes DUAS imagens: a FRENTE e o VERSO do mesmo cartão.

        {{CommonInstructions}}

        ESTRUTURA DO CARTÃO DE CIDADÃO:
        - FRENTE: Contém o Nome (Apelidos e Nome Próprio em blocos separados), número do documento (8 dígitos principais + dígito de controlo + letras de versão),
          data de nascimento, data de validade, fotografia, sexo, nacionalidade.
        - VERSO: Contém o NIF (Número de Identificação Fiscal, 9 dígitos), NISS (Segurança Social),
          número de utente SNS, nomes dos pais, e zona de leitura automática (MRZ).

        CAMPOS A EXTRAIR:
        - firstNames: O(s) Nome(s) Próprio(s) do titular. Estão na FRENTE do cartão. Devolve na ordem natural.
        - lastNames: O(s) Apelido(s) (Surnames) do titular. Estão na FRENTE do cartão, geralmente num bloco separado acima ou ao lado do nome próprio.
        - fullName: O nome completo ordenado corretamente: [Nomes Próprios] [Apelidos].
        - citizenCardNumber: Os 8 dígitos PRINCIPAIS do número do documento. Na FRENTE do cartão, aparece como "Nº de Documento" ou "Document No." seguido de um número no formato "12345678 X ZZ" (8 dígitos + dígito de controlo + letras). Devolve APENAS os primeiros 8 dígitos, sem espaços nem letras.
        - nif: O NIF do titular (9 dígitos). Está no VERSO do cartão, identificado como "NIF" ou "Nº de Identificação Fiscal". Devolve apenas os 9 dígitos.
        - expiryDate: A data de validade do cartão. Na FRENTE, aparece como "Válido até", "Validade", "Date of Expiry" ou "Expiry". Devolve no formato DD/MM/AAAA.

        DICAS DE LOCALIZAÇÃO:
        - No CC Português, os Apelidos (Surnames) aparecem num bloco e os Nomes Próprios (Given Names) noutro. Identifica-os bem.
        - O nome completo final deve ser a junção: firstNames + " " + lastNames.
        - O número do documento na FRENTE está normalmente numa linha própria, perto do topo ou do fundo
        - O NIF no VERSO está claramente identificado com a legenda "NIF"
        - A data de validade na FRENTE está perto do fundo do cartão ou perto da data de nascimento
        - Se uma das imagens estiver invertida ou rodada, tenta ler na mesma

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "firstNames": string | null,
            "lastNames": string | null,
            "fullName": string | null,
            "citizenCardNumber": string | null,
            "nif": string | null,
            "expiryDate": string | null
        }
        """;

    public static string CertidaoNaoDivida => $$"""
        Estás a analisar uma CERTIDÃO DE NÃO DÍVIDA portuguesa (também chamada "Certidão de Situação Tributária Regularizada").
        Este documento é emitido pela AT (Autoridade Tributária e Aduaneira) e comprova que o contribuinte
        não tem dívidas fiscais, ou seja, que a sua situação tributária está regularizada.
        Pode ser obtido no Portal das Finanças.

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - nif: O NIF (Número de Identificação Fiscal) do contribuinte que consta no documento. São 9 dígitos. Pode aparecer como "NIF", "Contribuinte N.º", "N.º de Contribuinte", ou no cabeçalho/corpo do documento. Devolve apenas os 9 dígitos.
        - isTaxRegularized: Se o documento confirma que a situação tributária está regularizada. Deve ser true se o documento contiver frases como "SITUAÇÃO TRIBUTÁRIA REGULARIZADA", "situação regularizada", "não é devedor", "não existem dívidas" ou equivalente. Deve ser false se indicar dívidas ou situação irregular.
        - expiryDate: A data de validade da certidão. Pode aparecer como "Válida até", "Data de validade", "A presente certidão é válida até" ou no rodapé. Devolve no formato DD/MM/AAAA.

        DICAS DE LOCALIZAÇÃO:
        - O NIF aparece normalmente no cabeçalho ou na identificação do contribuinte
        - A frase "SITUAÇÃO TRIBUTÁRIA REGULARIZADA" aparece normalmente em destaque, muitas vezes em maiúsculas ou a negrito, no corpo do documento
        - A data de validade costuma estar no rodapé ou no final do texto principal
        - O documento pode ter o logótipo da AT ou do Portal das Finanças, brasão da República
        - Pode conter um código de verificação/autenticação no rodapé

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "nif": string | null,
            "isTaxRegularized": boolean,
            "expiryDate": string | null
        }
        """;

    public static string GetPromptForDocType(string docType) => docType switch
    {
        "caderneta" => CadernetaPredial,
        "certificado" => CertificadoEnergetico,
        "modelo2" => RegistoAt,
        "certidao" => CertidaoPermanente,
        "licenca" => LicencaUtilizacao,
        _ => throw new ArgumentException($"Tipo de documento desconhecido: {docType}")
    };
}