# AGENTS.md — FinanceCore

## Contexto

Projeto de portfólio: API REST de transações financeiras simuladas. Leia o `README.md` antes de qualquer alteração. O status atual é **planejamento**; nenhuma implementação foi iniciada.

## Regras de escopo

- Entregar primeiro a menor versão funcional; só adicionar tecnologia quando houver requisito que a justifique.
- Não integrar bancos, gateways de pagamento ou qualquer serviço financeiro real.
- Manter compatibilidade com o escopo funcional descrito no README; funcionalidades fora dele exigem registro prévio e justificativa.
- Não criar abstrações, camadas ou microsserviços sem necessidade concreta.

## Critérios técnicos obrigatórios

- Valores monetários usam `decimal` no C# e `NUMERIC` no PostgreSQL. Nunca `float` ou `double`.
- Débito e crédito de uma transferência ocorrem na mesma transação do banco (tudo ou nada).
- O saldo deve ser protegido contra concorrência com lock apropriado e/ou atualização condicional (`UPDATE ... WHERE saldo >= valor`).
- O banco deve ter restrição que impeça saldo negativo.
- Operações sujeitas a repetição (transferências/depósitos) devem aceitar chave de idempotência; a mesma chave não pode duplicar a operação.
- Movimentações devem ser persistidas como registro histórico; o saldo não é a única fonte de verdade.
- Autorização por recurso: cada usuário só acessa contas e operações às quais tem permissão.
- Senhas armazenadas apenas com hash forte (ex.: ASP.NET Core Identity, PBKDF2 ou Argon2).
- Segredos apenas via variáveis de ambiente ou User Secrets; nunca versionar chaves, senhas ou strings de conexão reais.
- Expor e manter a documentação OpenAPI/Swagger atualizada.

## Convenções do repositório

- Registrar no README a versão do .NET utilizada (uma versão suportada/LTS).
- C# com nullable reference types habilitado e avisos tratados como erro quando viável.
- Commits pequenos e descritivos; uma mudança lógica por commit.
- Testes em xUnit; testes de integração com PostgreSQL real via Testcontainers.
- Docker Compose para PostgreSQL e demais dependências locais.
- CI (GitHub Actions) deve compilar e rodar todos os testes.
- Comandos canônicos esperados: `dotnet restore`, `dotnet build`, `dotnet test` e `docker compose up -d`.

## Critérios de teste

- Cobrir todos os cenários da seção "Testes essenciais" do README.
- Testes de concorrência devem usar requisições/transações simultâneas reais, não simulações sequenciais.
- Testes de integração não podem depender de dados pré-existentes no banco.
- Um único comando deve executar a suíte completa.

## Definition of Done

- Build e testes passam localmente e na CI.
- Testes de integração comprovam que transferências concorrentes não geram saldo inconsistente ou negativo.
- README atualizado com execução, decisões técnicas e limitações conhecidas.
- Nenhum segredo ou dado sensível versionado.
- OpenAPI/Swagger refletindo os endpoints implementados.

## Proibido

- Usar `float`/`double` para dinheiro.
- Efetivar débito sem crédito (ou vice-versa) em caso de falha.
- Ignorar concorrência em operações de saldo.
- Commitar segredos, senhas ou arquivos `.env` reais.
- Adicionar mensageria, cache distribuído ou outras tecnologias sem requisito documentado.
