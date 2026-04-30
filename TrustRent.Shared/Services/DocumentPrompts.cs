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

      public static string ComprovativoMorada => $$"""
        Estás a analisar um COMPROVATIVO DE MORADA português.
        O documento pode ser uma fatura de serviços essenciais (luz, água, gás, internet/telecomunicações),
        uma certidão/comprovativo de morada fiscal da AT, uma declaração bancária, extrato bancário,
        contrato/documento oficial de uma entidade regulada, ou outro documento oficial que associe uma pessoa
        a uma morada residencial.

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - holderName: Nome completo da pessoa titular/destinatária do documento. Pode aparecer como "Cliente", "Titular", "Destinatário", "Contribuinte", "Nome" ou no cabeçalho.
        - nif: NIF da pessoa titular/destinatária, se existir. Devolve apenas 9 dígitos. Se não existir, devolve null.
        - address: Morada completa sem código postal, incluindo rua, número, andar/fração/localidade quando disponível. Deve ser a morada do titular/destinatário, não a morada da entidade emissora.
        - postalCode: Código postal português no formato 0000-000, se existir.
        - documentType: Tipo normalizado do documento. Usa uma destas categorias quando possível: "utility_bill", "tax_address", "bank_document", "insurance_document", "public_authority_document", "other_official".
        - issuerName: Nome da entidade emissora, por exemplo E-REDES, MEO, Vodafone, Portal das Finanças, banco, seguradora, câmara municipal.
        - issueDate: Data de emissão ou data do documento, no formato DD/MM/AAAA. Se houver período de faturação e data de emissão, usa a data de emissão.

        REGRAS IMPORTANTES:
        - O documento só é válido como comprovativo de morada se tiver uma entidade emissora identificável e uma morada associada ao titular/destinatário.
        - Não uses moradas de rodapé, sede social da empresa emissora, lojas, balcões ou contactos da entidade emissora.
        - Se houver várias moradas, escolhe a morada do titular/cliente/contribuinte, não a da entidade emissora.
        - Para faturas, a morada pode aparecer como "morada de faturação", "local de consumo" ou "morada do cliente". Prefere a morada do cliente/titular quando existir; se só existir local de consumo e estiver associado ao cliente, usa essa.
        - Se o documento parecer uma captura informal sem entidade oficial, marca isAuthentic=false ou allFieldsExtracted=false conforme apropriado.

        SINAIS DE ADULTERAÇÃO A PROCURAR:
        - Nome ou morada com fonte/alinhamento diferente do resto do documento.
        - Sobreposições, cortes, páginas incompletas, valores cobertos ou áreas editadas.
        - Ausência de logótipo/cabeçalho/identificação da entidade emissora.
        - Morada incompleta sem rua/localidade ou sem qualquer ligação ao titular.

        SCHEMA JSON DE RESPOSTA:
        {
          "isAuthentic": boolean,
          "fraudReason": string | null,
          "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
          "allFieldsExtracted": boolean,
          "holderName": string | null,
          "nif": string | null,
          "address": string | null,
          "postalCode": string | null,
          "documentType": string | null,
          "issuerName": string | null,
          "issueDate": string | null
        }
        """;

    public static string ReciboVencimento => $$"""
        Estás a analisar um RECIBO DE VENCIMENTO português (folha de remunerações mensal emitida pela entidade empregadora).

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - employeeName: Nome completo do trabalhador (titular do recibo). Aparece numa secção identificada como "Trabalhador", "Funcionário", "Colaborador" ou similar.
        - employeeNif: NIF do trabalhador (9 dígitos, sem espaços nem pontos). Está na secção do trabalhador, identificado como "NIF", "Contribuinte" ou "Nº Contribuinte".
        - employerName: Nome/razão social da entidade empregadora. Costuma estar no cabeçalho do recibo.
        - employerNif: NIF da entidade empregadora (9 dígitos). Está no cabeçalho, junto à identificação da empresa.
        - referenceMonth: Mês/ano a que o recibo se refere, formato MM/AAAA. Pode aparecer como "Mês de Referência", "Período", "Outubro/2025", "10/2025", etc.
        - issueDate: Data de emissão do recibo, formato DD/MM/AAAA.
        - grossSalary: Vencimento ilíquido total em euros (número decimal, ponto como separador, sem símbolo €). Pode aparecer como "Total Ilíquido", "Total Bruto", "Remunerações Brutas".
        - netSalary: Vencimento líquido a receber em euros (número decimal, ponto como separador, sem símbolo €). Aparece como "Líquido a Receber", "Total Líquido", "Valor Líquido", "A Receber".
        - currency: Moeda detectada (esperado "EUR" para recibos portugueses).

        DICAS DE LOCALIZAÇÃO:
        - Recibos portugueses têm normalmente: cabeçalho com a empresa, secção do trabalhador, tabela de remunerações (vencimento base, subsídios, prémios), tabela de descontos (IRS, Segurança Social), e total líquido em destaque.
        - O líquido a receber está normalmente no fim do documento, em destaque ou negrito.
        - O ilíquido é a soma de todas as remunerações ANTES dos descontos.
        - Se houver várias colunas (mês corrente / acumulado anual), usa SEMPRE a coluna do mês corrente.
        - Os valores em euros usam vírgula como separador decimal em PT — converte para ponto antes de devolver (ex: "1.234,56" → 1234.56).

        SINAIS DE ADULTERAÇÃO A PROCURAR:
        - Fontes diferentes nos números do líquido/ilíquido vs resto do documento.
        - Sobreposições de texto, alinhamento estranho, pixels visíveis em redor de números.
        - Datas inconsistentes (ex: data de emissão anterior ao mês de referência).
        - NIF da empresa ou do trabalhador com menos/mais de 9 dígitos.

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "employeeName": string | null,
            "employeeNif": string | null,
            "employerName": string | null,
            "employerNif": string | null,
            "referenceMonth": string | null,
            "issueDate": string | null,
            "grossSalary": number | null,
            "netSalary": number | null,
            "currency": string | null
        }
        """;

    public static string DeclaracaoEntidadeEmpregadora => $$"""
        Estás a analisar uma DECLARAÇÃO DE EFETIVIDADE / DECLARAÇÃO DA ENTIDADE EMPREGADORA portuguesa.
        É um documento emitido pelo empregador (em papel timbrado), normalmente assinado e carimbado,
        que comprova que o trabalhador está ao seu serviço, indicando funções, tipo de contrato e data
        de início. Usa-se para validar emprego ativo quando o trabalhador ainda não tem 3 recibos.

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - employeeName: Nome completo do trabalhador. Aparece após "vem por este meio declarar que",
          "que o(a) Sr(a).", "que o nosso colaborador" ou semelhante.
        - employeeNif: NIF do trabalhador (9 dígitos). Pode aparecer como "NIF", "Contribuinte n.º".
          Pode estar ausente se a declaração só identificar pelo nome — devolve null nesse caso.
        - employerName: Nome / razão social da entidade empregadora. Costuma estar no cabeçalho
          (papel timbrado), assinatura final ou carimbo.
        - employerNif: NIF da entidade empregadora (9 dígitos). Cabeçalho, rodapé ou carimbo.
        - position: Cargo ou função do trabalhador (ex: "Programador Junior", "Assistente Administrativa").
          Procura "exerce as funções de", "categoria profissional", "cargo".
        - contractType: Tipo de contrato. Devolve uma das categorias normalizadas:
          "Sem termo" (efetivo / contrato sem termo / por tempo indeterminado),
          "Termo certo", "Termo incerto", "Estágio", "Prestação de serviços". Se não for explícito, devolve null.
        - employmentStartDate: Data de início do vínculo laboral, formato DD/MM/AAAA.
          Procura "admitido em", "ao serviço desde", "desde".
        - issueDate: Data de emissão da declaração, formato DD/MM/AAAA. Costuma estar no cabeçalho ou rodapé.
        - hasSignatureAndStamp: true se o documento tem assinatura E carimbo / selo da empresa visíveis.
          false se faltar algum dos dois.

        DICAS DE LOCALIZAÇÃO:
        - Documento curto (geralmente 1 página) com cabeçalho da empresa.
        - A frase típica é: "X, Lda., NIPC YYY, declara que o(a) Sr(a). NOME, NIF ZZZ, exerce as
          funções de CARGO desde DATA, com contrato sem termo."
        - O carimbo costuma estar junto à assinatura, no rodapé.

        SINAIS DE ADULTERAÇÃO:
        - Falta de carimbo ou assinatura.
        - NIF da empresa com menos/mais de 9 dígitos.
        - Datas inconsistentes (ex: emissão anterior à data de admissão).
        - Tipo de letra do nome/NIF do trabalhador diferente do resto do texto.

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "employeeName": string | null,
            "employeeNif": string | null,
            "employerName": string | null,
            "employerNif": string | null,
            "position": string | null,
            "contractType": string | null,
            "employmentStartDate": string | null,
            "issueDate": string | null,
            "hasSignatureAndStamp": boolean | null
        }
        """;

    public static string DeclaracaoInicioAtividade => $$"""
        Estás a analisar uma DECLARAÇÃO DE INÍCIO DE ATIVIDADE ou comprovativo de "Consultar Atividade"
        emitido pelo Portal das Finanças (AT). Comprova que um trabalhador independente está coletado
        para efeitos fiscais, identificando o(s) CAE da atividade exercida e a data de início.

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - taxpayerName: Nome do contribuinte / sujeito passivo (a pessoa coletada).
        - taxpayerNif: NIF do contribuinte (9 dígitos).
        - caeCodes: Lista de códigos CAE (Classificação das Atividades Económicas) declarados no documento.
          Cada código tem 5 dígitos (ex: "62010", "70220"). Se o documento listar CAE principal e
          secundários, devolve TODOS por ordem (principal primeiro). Devolve só os 5 dígitos sem descrição.
        - caePrincipalDescription: Descrição textual da atividade principal (ex: "Atividades de programação informática").
        - activityStartDate: Data de início da atividade, formato DD/MM/AAAA. Procura "Início de Atividade",
          "Data de Início".
        - activityStatus: Estado atual da atividade. Devolve uma das categorias:
          "Activa" (em atividade), "Cessada" (cessou atividade), "Suspensa" ou null se ambíguo.
        - issueDate: Data em que o documento foi consultado/emitido, formato DD/MM/AAAA.
          Costuma estar no rodapé como "Documento emitido em".

        DICAS DE LOCALIZAÇÃO:
        - Documento do Portal das Finanças tem cabeçalho com brasão da República e logótipo da AT.
        - O CAE costuma estar numa secção "Atividade" ou "CAE" com formato "XXXXX - Descrição".
        - A data de início está numa secção de identificação fiscal ou histórico.
        - O estado "Activa" / "Cessada" pode estar como rótulo destacado.

        SINAIS DE ADULTERAÇÃO:
        - Ausência de cabeçalho oficial AT / República.
        - CAE com formato inválido (não 5 dígitos).
        - NIF com menos/mais de 9 dígitos.
        - Status "Cessada" mas o documento parece ser apresentado como prova de atividade — sinaliza.

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "taxpayerName": string | null,
            "taxpayerNif": string | null,
            "caeCodes": [string] | null,
            "caePrincipalDescription": string | null,
            "activityStartDate": string | null,
            "activityStatus": string | null,
            "issueDate": string | null
        }
        """;

    public static string ReciboVerde => $$"""
        Estás a analisar um RECIBO VERDE / FATURA-RECIBO ELETRÓNICA emitido no Portal das Finanças
        por um trabalhador independente (categoria B). Tem cabeçalho da AT, identificação do
        prestador (emitente) e do adquirente (cliente), montante e data.

        {{CommonInstructions}}

        CAMPOS A EXTRAIR:
        - issuerName: Nome do PRESTADOR de serviços (quem emite o recibo — o trabalhador independente).
          Aparece numa secção "Prestador" ou "Emitente".
        - issuerNif: NIF do prestador (9 dígitos).
        - acquirerName: Nome do ADQUIRENTE (cliente que pagou). Pode ser empresa ou pessoa.
        - acquirerNif: NIF do adquirente (9 dígitos). Pode estar ausente para clientes particulares — devolve null nesse caso.
        - issueDate: Data de emissão do recibo, formato DD/MM/AAAA.
        - referenceMonth: Mês a que se refere o serviço, formato MM/AAAA. Se não estiver explícito,
          usa o mês da data de emissão (ex: emitido em 15/03/2026 → "03/2026").
        - baseAmount: Valor base / honorários antes de IVA e retenção, em euros (decimal com ponto, sem €).
          Aparece como "Base de Incidência em IRS", "Valor Base", "Honorários".
        - totalAmount: Valor total a receber pelo prestador (decimal com ponto). Pode ser igual ao base
          se não houver IVA nem retenções.
        - currency: Moeda detectada (esperado "EUR").

        DICAS DE LOCALIZAÇÃO:
        - Documento eletrónico vertical com cabeçalho "FATURA-RECIBO" ou "RECIBO".
        - O número da fatura-recibo costuma estar no topo (ex: "FR XXX/AAAA").
        - Os valores em euros usam vírgula decimal em PT — converte para ponto (ex: "1.200,00" → 1200.00).
        - Tem um código de validação / hash AT no rodapé.

        SINAIS DE ADULTERAÇÃO:
        - Ausência de hash de validação AT.
        - NIF do prestador ou adquirente com formato inválido.
        - Inconsistência entre base e total que não se explique por IVA/retenções razoáveis.

        SCHEMA JSON DE RESPOSTA:
        {
            "isAuthentic": boolean,
            "fraudReason": string | null,
            "imageQuality": "good" | "blurry" | "dark" | "cropped" | "unreadable",
            "allFieldsExtracted": boolean,
            "issuerName": string | null,
            "issuerNif": string | null,
            "acquirerName": string | null,
            "acquirerNif": string | null,
            "issueDate": string | null,
            "referenceMonth": string | null,
            "baseAmount": number | null,
            "totalAmount": number | null,
            "currency": string | null
        }
        """;

    public static string GetPromptForDocType(string docType) => docType switch
    {
        "caderneta" => CadernetaPredial,
        "certificado" => CertificadoEnergetico,
        "modelo2" => RegistoAt,
        "certidao" => CertidaoPermanente,
        "licenca" => LicencaUtilizacao,
        "recibo" => ReciboVencimento,
        "declaracao-empregador" => DeclaracaoEntidadeEmpregadora,
        "declaracao-atividade" => DeclaracaoInicioAtividade,
        "recibo-verde" => ReciboVerde,
        "comprovativo-morada" => ComprovativoMorada,
        _ => throw new ArgumentException($"Tipo de documento desconhecido: {docType}")
    };
}