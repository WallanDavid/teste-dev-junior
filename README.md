# Mini Gestão de Pedidos

[![CI](https://github.com/WallanDavid/teste-dev-junior/actions/workflows/ci.yml/badge.svg)](https://github.com/WallanDavid/teste-dev-junior/actions/workflows/ci.yml)

Cadastro e consulta de pedidos de clientes: **.NET 8 + SQL Server + React/TypeScript**, tudo no Docker, desenvolvido com o **Claude Code** em fluxo *spec-driven*.

| Documento | Conteúdo |
|---|---|
| [`docs/spec.md`](docs/spec.md) | Especificação: regras de negócio (RN01–RN12), modelo de dados, contratos da API, critérios de aceite (CA01–CA17) |
| [`docs/plan.md`](docs/plan.md) | Plano de implementação e **o que foi ajustado/descartado da proposta do Claude e por quê** |
| [`CLAUDE.md`](CLAUDE.md) | Regras do projeto para o Claude (arquitetura, convenções, comandos) |

---

## 1. Como rodar com Docker

Pré-requisito: Docker (Docker Desktop no Windows/macOS).

```bash
docker compose up --build
```

| Serviço | URL |
|---|---|
| Frontend | http://localhost:3000 |
| API + Swagger | http://localhost:8080/swagger |
| SQL Server | `localhost,1433` · usuário `sa` · senha `Pedidos_Dev2026` · banco `Pedidos` |

O que acontece, sem nenhum passo manual:

1. `sqlserver` sobe e só fica *healthy* quando responde a `SELECT 1`.
2. `db-init` (mesma imagem, vida curta) roda `database/01-create-database.sql`, `02-create-tables.sql` e `03-seed.sql` e encerra. Os scripts são idempotentes.
3. `api` só sobe depois que o `db-init` termina com sucesso, e fica *healthy* quando `/health` (que testa o banco) responde 200.
4. `frontend` (nginx) sobe por último e repassa `/api/*` para a API.

Para zerar o banco: `docker compose down -v && docker compose up`.

> A senha é só de desenvolvimento e pode ser trocada com a variável `MSSQL_SA_PASSWORD`. A imagem do SQL Server é x64; em Mac com Apple Silicon ela roda via Rosetta (`platform: linux/amd64` já está no compose).

## 2. Como rodar os testes

| Suíte | Comando | Requisitos |
|---|---|---|
| Unitários (37) | `dotnet test tests/Pedidos.UnitTests` | .NET 8 SDK |
| Integração (28) | `dotnet test tests/Pedidos.IntegrationTests` | .NET 8 SDK + Docker (Testcontainers sobe um SQL Server 2022 real) |
| Tudo do backend | `dotnet test Pedidos.sln` | idem |
| Frontend | `cd frontend && npm ci && npm run lint && npm run build` | Node 24 |
| E2E (Playwright) | `docker compose up -d --wait` e depois `cd e2e && npm ci && npx playwright install chromium && npx playwright test` | Node 24 + compose no ar |

- **Unitários**: regras do domínio (`Pedido.Criar`) e casos de uso (`PedidoService`, `RelatorioService`) com repositórios mockados (NSubstitute): sem itens, quantidade inválida, cliente inexistente, produto inexistente, estoque insuficiente (atômico: nenhum estoque muda), produto repetido somado, preço congelado, paginação e período inválidos, conversão de datas inclusivas.
- **Integração**: a API real (`WebApplicationFactory`) contra um SQL Server real em container. O schema é criado com **os mesmos scripts do compose**, então um mapeamento do EF divergente do banco quebra o teste. Cobrem CA01–CA15, incluindo **pedidos simultâneos** (5 requisições paralelas disputando o último item → exatamente 1 gravado; 5 paralelas com estoque sobrando → todas gravadas).
- **E2E**: criar pedido com 2 itens e vê-lo na listagem; erro de estoque insuficiente no modal sem perder os dados; filtro por cliente.
- **CI** ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)): a cada push/PR roda build + unitários + integração, lint + build do frontend e, por fim, `docker compose up --wait` + Playwright. Isso garante que o compose sobe do zero numa máquina limpa.

## 3. Estrutura

```
src/
  Pedidos.Domain/          entidades e regras puras (Pedido.Criar), Result/Error
  Pedidos.Application/     casos de uso, DTOs, interfaces de repositório/consulta
  Pedidos.Infrastructure/  EF Core (mapeia as tabelas dos scripts), repositórios, queries
  Pedidos.Api/             Minimal APIs, ProblemDetails, Swagger, health check
tests/                     Pedidos.UnitTests · Pedidos.IntegrationTests
frontend/                  React 19 + TypeScript + Vite (nginx no Docker)
e2e/                       Playwright
database/                  01-03 (schema + carga) · debug/ (Parte 4)
.claude/                   settings.json (hooks + permissões), hooks/, skills/
.mcp.json                  MCP do SQL Server (DBHub)
```

### Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/pedidos` | Cria pedido. 201 · 400 (`validacao`) · 422 (`cliente_nao_encontrado`, `produto_nao_encontrado`, `estoque_insuficiente`) |
| `GET` | `/pedidos?clienteId=&dataInicio=&dataFim=&pagina=&tamanhoPagina=` | Lista paginada, mais recentes primeiro |
| `GET` | `/pedidos/{id}` | Detalhe (alvo do header `Location`) |
| `GET` | `/relatorios/top-clientes?dataInicio=&dataFim=` | Top 5 por valor no período |
| `GET` | `/clientes`, `/produtos` | Apoio aos selects do frontend |
| `GET` | `/health` | API + banco |

## 4. Principais decisões técnicas

- **Scripts SQL como fonte da verdade do schema** (sem EF Migrations). O teste pede scripts explícitos, e índices pensados para as consultas são mais claros em SQL. O EF só mapeia, e os testes de integração usam os mesmos scripts, então divergência entre os dois quebra o build.
- **Camadas Domain → Application → Infrastructure → Api.** Regras de pedido ficam no domínio (`Pedido.Criar`), testáveis sem banco. Leitura (`IPedidoQueries`) separada de escrita (`IPedidoRepository`): leituras projetam direto para DTO com `AsNoTracking`.
- **`Result<T>`/`Error` em vez de exceções** para regra de negócio. Um único ponto (`ResultExtensions`) traduz para HTTP: validação → **400** com erros por campo; regra → **422**; inexistente na URL → **404**. Todo erro tem um `code` estável que o frontend usa.
- **Baixa de estoque atômica**: `UPDATE Produtos SET Estoque = Estoque - @q WHERE Id = @id AND Estoque >= @q` na mesma transação do pedido, mais `CHECK (Estoque >= 0)` como última barreira. Essa decisão mudou durante o desenvolvimento (ver seção 5).
- **Valor total calculado dos itens**, não armazenado (uma fonte da verdade). O preço unitário é **copiado** do produto no momento do pedido.
- **Datas em UTC e filtros inclusivos convertidos para intervalo semiaberto** `[início, fim + 1 dia)`. Inclui o último dia inteiro e mantém o predicado sargável (a mesma lição da Parte 4).
- **Paginação estável**: `ORDER BY DataPedido DESC, Id DESC`.
- **`TimeProvider` injetado** para testar a data do pedido de forma determinística.
- **Frontend sem biblioteca de UI**: cliente HTTP tipado que interpreta ProblemDetails, `useAsync` que cancela requisições obsoletas (trocar filtro rápido não mostra resultado velho), labels acessíveis (o Playwright usa `getByLabel`/`getByRole`).
- **nginx com proxy `/api`** no Docker e proxy equivalente no Vite em dev: sem CORS e sem URL da API embutida no bundle.

## 5. Como usei o Claude Code

### Fluxo

1. **Spec primeiro** (`docs/spec.md`): regras numeradas e critérios de aceite. Os testes referenciam esses números (`// CA06`), então dá para rastrear requisito → teste.
2. **Plano** (`docs/plan.md`) revisado antes de codar. A tabela "Ajustes e discordâncias" registra 10 pontos em que a proposta inicial foi alterada.
3. **Implementação em commits pequenos**, camada por camada, com testes junto. O histórico mostra a evolução, inclusive os commits `fix(...)` das correções descritas abaixo.

### Rules, hooks, skills e MCP

| Peça | Arquivo | Para quê |
|---|---|---|
| Rules | [`CLAUDE.md`](CLAUDE.md) | Arquitetura, convenções de nome, como rodar build/testes e regras do tipo "nunca use `YEAR()` no WHERE" e "baixa de estoque é sempre UPDATE condicional". |
| Hook `PostToolUse` | [`.claude/hooks/post-edit.mjs`](.claude/hooks/post-edit.mjs) | Depois de cada edição compila o projeto `.csproj` mais próximo (ou roda `tsc` no frontend/e2e). Se falhar, devolve **exit 2** com só as linhas de erro, e o Claude corrige na mesma interação. |
| Hook `PreToolUse` | [`.claude/hooks/guard-git.mjs`](.claude/hooks/guard-git.mjs) | Bloqueia `push --force`, `rebase`, `commit --amend`, `reset --hard`, `merge --squash`. O histórico é avaliado, então reescrever é proibido. |
| Skill | [`novo-endpoint`](.claude/skills/novo-endpoint/SKILL.md) | Passo a passo para um endpoint novo seguindo as camadas: spec → Application → Infrastructure → Api → teste unitário → teste de integração. |
| Skill | [`analisar-query`](.claude/skills/analisar-query/SKILL.md) | Roteiro de investigação de query lenta/errada: corretude primeiro, depois índices, STATISTICS IO/TIME e plano. Foi o roteiro usado na Parte 4. |
| MCP | [`.mcp.json`](.mcp.json) | [DBHub](https://github.com/bytebase/dbhub) apontando para o SQL Server do compose. Ferramentas `search_objects` e `execute_sql`. |

**Como o MCP foi usado.** O `.mcp.json` foi criado no meio da sessão de desenvolvimento, e o Claude Code só carrega servidores MCP no início de uma sessão. Por isso:

- a conexão foi **validada falando o protocolo MCP diretamente** com o servidor (`initialize` → `tools/list` → `execute_sql` listando os índices de `dbo.Pedidos`), o que confirmou o DSN, o banco e os índices criados pelos scripts;
- as **medições da Parte 4 foram feitas com `sqlcmd`** dentro do compose, porque `SET STATISTICS IO/TIME` e `SET SHOWPLAN_TEXT` dependem de mensagens do servidor e de lotes separados, que o `execute_sql` não devolve. A skill `analisar-query` documenta essa divisão;
- em sessões seguintes o servidor aparece em `/mcp` e o Claude usa `search_objects` para conferir schema/índices e `execute_sql` para consultas de verificação (ex.: achar homônimos, conferir estoque depois de um teste), sem precisar abrir outro cliente SQL.

### O que funcionou bem

- **O hook de build pegou erros de verdade na hora.** Exemplo: em `tests/Pedidos.UnitTests/Builders.cs`, `nameof(Domain.Entities.Produto.Id)` não compilava porque o namespace `Pedidos.UnitTests.Domain` sombreava `Pedidos.Domain`. O erro voltou no mesmo passo e foi corrigido antes de seguir.
- **O hook de git funcionou até demais**: bloqueou um comando de *teste do próprio hook* que só continha o texto `git push --force` dentro de uma string. Foi falso positivo, mas mostra que a guarda está ativa.
- **Testes de integração com os scripts reais** deram confiança para mexer no banco: a troca do mecanismo de estoque (abaixo) foi validada contra SQL Server de verdade em segundos.
- **Spec com critérios numerados**: cada teste de integração cita o critério que cobre (`// CA06`), então conferir cobertura é um `grep`.

### O que precisei corrigir no código gerado

| # | Problema | Como apareceu | Correção |
|---|---|---|---|
| 1 | **Concorrência de estoque.** A 1ª versão usava `ROWVERSION` (otimista) e devolvia 409 em conflito. | O E2E rodando com 3 workers recebeu **409 com estoque sobrando**: dois pedidos de "Mouse" (estoque 200) colidiram. Tentei *retry* com releitura (3 tentativas), e um teste de integração com 5 pedidos simultâneos falhou **5 de 5**, porque sob contenção eles colidem de novo a cada rodada. | `UPDATE ... WHERE Estoque >= @q` atômico na transação; 0 linhas → 422. Removidos `RowVersion` e o 409. Teste de regressão adicionado. Commit `fix(pedidos): troca concorrência otimista por baixa de estoque atômica`. |
| 2 | Data em formato inválido (`?dataInicio=abc`) e JSON malformado retornavam **500**. | Smoke test manual da API. | `BadRequestExceptionHandler` → 400 ProblemDetails. Coberto por teste de integração. |
| 3 | Valores monetários saíam como `399.0000`. | Leitura do JSON real: `int × decimal(18,2)` volta com escala 4 do SQL Server. | `DinheiroJsonConverter` normaliza para 2 casas. |
| 4 | Aviso do EF de precisão decimal no tipo do relatório (`SqlQuery`). | Saída dos testes de integração. | Convenção global `HavePrecision(18, 2)`. |
| 5 | Lint (oxlint): `setState` síncrono dentro de `useEffect` e `new Date()` durante o render. | `npm run lint`. | `useAsync` passou a **derivar** `loading` de uma chave; data inicial calculada no inicializador do `useState`. |
| 6 | Testes E2E frágeis: seletor de heading ambíguo (`"Pedidos"` casava com `"Mini Gestão de Pedidos"`), leitura das linhas antes da resposta filtrada chegar e suposição de "meu pedido é a 1ª linha" (falsa com workers paralelos). | Rodando `--repeat-each=3 --workers=3`. | `exact: true`, asserções com retry automático (`toHaveCount`) e remoção da suposição de ordem (já coberta na integração, CA09). |
| 7 | Carga de exemplo tinha um "pedido sem itens" para um cliente descrito como "sem pedidos". | Revisão do seed. | Removido; a API nem permite criar pedido sem itens. |
| 8 | Script de volume com `RETURN` para não rodar duas vezes, mas `RETURN` só sai do **lote** atual, e os `GO` seguintes rodariam mesmo assim. | Revisão antes de executar. | Um único lote com `IF ... ELSE BEGIN ... END`. |
| 9 | Modal com rolagem horizontal (nomes de produto longos). | Teste manual no navegador. | `min-width: 0` nos itens do grid. |

### Limitações percebidas

- O hook de build roda **a cada arquivo**. Ao criar vários arquivos interdependentes em sequência, os primeiros acusam erro porque o próximo ainda não existe. É ruído inofensivo (o último build passa), mas consome contexto. Uma melhoria seria rodar o build no evento `Stop` (fim do turno) em vez de por edição.
- O `guard-git` olha o texto do comando, então pode dar falso positivo quando o padrão aparece dentro de uma string.

## 6. Parte 4: debug da query

### Query original

```sql
SELECT c.Nome, SUM(i.Quantidade * i.PrecoUnitario) AS Total
FROM Clientes c
LEFT JOIN Pedidos p ON p.ClienteId = c.Id
LEFT JOIN ItensPedido i ON i.PedidoId = p.Id
WHERE YEAR(p.DataPedido) = 2026
GROUP BY c.Nome
ORDER BY Total
```

### O que estava errado

| # | Problema | Efeito |
|---|---|---|
| 1 | `GROUP BY c.Nome` | **Resultado incorreto.** Clientes diferentes com o mesmo nome viram uma linha só, com os valores somados. |
| 2 | `ORDER BY Total` (crescente) | **Resultado incorreto.** Num ranking de quem mais comprou, o 1º lugar aparece por último. Também não havia desempate determinístico. |
| 3 | `WHERE YEAR(p.DataPedido) = 2026` | **Lentidão.** Função aplicada na coluna não é *sargável*: o SQL Server precisa ler todos os pedidos, calcular `YEAR()` linha a linha e só então filtrar. Nenhum índice em `DataPedido` pode ser usado com *seek*. |
| 4 | `LEFT JOIN` + filtro no `WHERE` sobre a tabela da direita | **Intenção ambígua.** Para clientes sem pedido, `p.DataPedido` é `NULL` e o `WHERE` os elimina, então o `LEFT JOIN` vira, na prática, `INNER JOIN`. Para um ranking de "quem comprou" o `INNER` é o correto, e deixá-lo explícito evita que alguém "conserte" movendo o filtro para o `ON` e passe a listar clientes com total `NULL`. |

### Como investiguei

Segui o roteiro da skill [`analisar-query`](.claude/skills/analisar-query/SKILL.md): **corretude primeiro, depois performance**.

**1. Corretude, na carga de exemplo** (que tem um homônimo proposital: "Ana Souza", ids 1 e 9):

| | Original | Corrigida |
|---|---|---|
| Linhas | 8 | 9 |
| "Ana Souza" | 1 linha, **R$ 16.502,70** | id 1: R$ 9.543,90 · id 9: R$ 6.958,80 (16.502,70 = soma das duas) |
| Primeira linha | Felipe Andrade (o que **menos** comprou) | Ana Souza id 1 (o que mais comprou) |

**2. Volume.** Com 30 pedidos não dá para medir nada, então o [`database/debug/10-carga-volume.sql`](database/debug/10-carga-volume.sql) gera **20 mil clientes** (só 200 nomes distintos, ~100 homônimos por nome), **500 mil pedidos** entre 2020 e 2026 e **1,25 milhão de itens**.

**3. Medição** com [`database/debug/05-medicao.sql`](database/debug/05-medicao.sql) (`SET STATISTICS IO, TIME ON`; 2ª execução, cache quente; resultado gravado em tabela temporária para não medir o envio das linhas):

| | Original | Corrigida |
|---|---|---|
| Leituras lógicas em `Pedidos` | 2.293 | **342** (−85%) |
| Leituras lógicas em `ItensPedido` | 7.576 | 7.576 |
| `Workfile` (spill do hash para o tempdb) | 320 | **0** |
| CPU | 2.082 ms | **939 ms** (−55%) |
| Tempo decorrido | 397 ms | **279 ms** |
| Linhas retornadas | **208** (20 mil clientes colapsados em 208 nomes) | 19.426 |
| Soma de todos os totais | 486.470.861,70 | 486.470.861,70 (mesmo dinheiro, distribuído certo) |

**4. Plano de execução** (`SET SHOWPLAN_TEXT ON`):

- **Original**: `Index Scan` em todo `IX_Pedidos_ClienteId_DataPedido` (500 mil linhas) → `Compute Scalar(datepart(year, DataPedido))` → `Filter([Expr]=(2026))`. O filtro só acontece **depois** de ler tudo. Em seguida: `Hash Match (Partial Aggregate)` por `Nome`, `Sort` por `Nome` e `Stream Aggregate`, mais um `Sort` final por `Total`. A agregação por texto custou o spill em `Workfile`.
- **Corrigida**: `Index Seek` em `IX_Pedidos_DataPedido` com `SEEK: DataPedido >= '2026-01-01' AND DataPedido < '2027-01-01'`, lendo só os ~71 mil pedidos de 2026. O índice inclui `ClienteId`, então não há *key lookup*. Agrega por `ClienteId` (inteiro) e só depois junta com `Clientes` para pegar o nome.
- **`ItensPedido` continua com scan nas duas versões, e isso é o correto.** O otimizador usa *hash join* com *bitmap filter* (`PROBE(Opt_Bitmap)`) sobre o índice clusterizado. Como ~14% dos itens são de 2026, fazer ~71 mil *seeks* em `UX_ItensPedido_PedidoId_ProdutoId` sairia mais caro que uma varredura sequencial filtrada.

### Query corrigida

```sql
DECLARE @inicio DATETIME2(3) = '2026-01-01',
        @fim    DATETIME2(3) = '2027-01-01'; -- exclusivo

WITH TotaisPorCliente AS (
    SELECT p.ClienteId,
           COUNT(DISTINCT p.Id)                AS QuantidadePedidos,
           SUM(i.Quantidade * i.PrecoUnitario) AS Total
    FROM dbo.Pedidos p
    JOIN dbo.ItensPedido i ON i.PedidoId = p.Id
    WHERE p.DataPedido >= @inicio          -- sargável: usa IX_Pedidos_DataPedido
      AND p.DataPedido <  @fim             -- intervalo semiaberto: pega o dia 31/12 inteiro
    GROUP BY p.ClienteId                   -- agrupa pela chave, não pelo nome
)
SELECT DENSE_RANK() OVER (ORDER BY t.Total DESC) AS Posicao,
       c.Id, c.Nome, t.QuantidadePedidos, t.Total
FROM TotaisPorCliente t
JOIN dbo.Clientes c ON c.Id = t.ClienteId
ORDER BY t.Total DESC, c.Id;               -- maior primeiro, desempate determinístico
```

Índices usados (criados em `database/02-create-tables.sql`):

```sql
CREATE INDEX IX_Pedidos_DataPedido ON dbo.Pedidos (DataPedido DESC) INCLUDE (ClienteId);
CREATE UNIQUE INDEX UX_ItensPedido_PedidoId_ProdutoId
    ON dbo.ItensPedido (PedidoId, ProdutoId) INCLUDE (Quantidade, PrecoUnitario);
```

O endpoint `GET /relatorios/top-clientes` usa exatamente essa lógica (com `TOP (5)`), em `RelatorioQueries`. O teste de integração `TopClientes_ClientesHomonimos_NaoSaoSomadosJuntos` protege contra a regressão do item 1.

**Próximo passo, se o volume crescer:** o custo restante é a varredura de `ItensPedido`. Gravar `ValorTotal` em `Pedidos` (o pedido não é editável, então é seguro) e incluí-lo em `IX_Pedidos_DataPedido` eliminaria esse join. A expectativa é que a consulta leia só as páginas do *seek* em `Pedidos`. Não implementei porque hoje não há necessidade de performance que justifique desnormalizar (ver `docs/plan.md`, ajuste #2).

**Detalhe percebido no plano:** com literais (`'2026-01-01'`) o plano mostra `GetRangeWithMismatchedTypes`, porque o literal é `varchar` e é convertido. Com parâmetros `DATETIME2` (como na API e no script) a conversão some.

## 7. Diferenciais

- [x] Testes E2E com Playwright (`e2e/`)
- [x] GitHub Actions: build, testes unitários, integração e E2E com o compose (badge no topo)
- [ ] Deploy online: não feito
