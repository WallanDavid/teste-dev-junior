# Plano de implementação

Plano gerado com o Claude Code a partir de [`spec.md`](spec.md), revisado antes de começar a codar. A primeira seção é o plano como ficou; a segunda registra **o que foi ajustado ou descartado da proposta inicial e por quê**.

## 1. Arquitetura

```
src/
  Pedidos.Domain/          entidades + regras puras (sem EF, sem ASP.NET)
  Pedidos.Application/     casos de uso (PedidoService), DTOs, Result/Error, interfaces de repositório
  Pedidos.Infrastructure/  EF Core (DbContext mapeando as tabelas criadas pelos scripts SQL), repositórios
  Pedidos.Api/             Minimal APIs, mapeamento Result → HTTP (ProblemDetails), Swagger, health check
tests/
  Pedidos.UnitTests/         xUnit + NSubstitute — domínio e PedidoService
  Pedidos.IntegrationTests/  xUnit + WebApplicationFactory + Testcontainers (SQL Server real)
frontend/                  React 19 + TypeScript + Vite; servido por nginx no Docker
e2e/                       Playwright contra o docker compose
database/                  scripts SQL (schema, carga, debug da query)
```

Dependências apontam para dentro: `Api → Application → Domain`, `Infrastructure → Application`. A Api referencia Infrastructure só para registrar DI.

## 2. Passos (cada passo = 1 ou mais commits pequenos)

1. **Base do repositório** — `.gitignore`, `.editorconfig`, spec e plano.
2. **Configuração AI First** — `CLAUDE.md`, hooks (build/lint pós-edição e bloqueio de reescrita de histórico), skills (`novo-endpoint`, `analisar-query`), `.mcp.json` com DBHub apontando para o SQL Server do compose.
3. **Banco** — `database/01-create-database.sql`, `02-create-tables.sql` (PK, FK, CHECK, índices da spec), `03-seed.sql` (idempotente). Scripts são a **fonte da verdade do schema**; EF só mapeia.
4. **Solution .NET** — projetos, referências, pacotes.
5. **Domínio** — `Pedido.Criar(...)` com RN01, RN04, RN05–RN08; testes unitários primeiro (TDD nas regras).
6. **Aplicação** — `PedidoService.CriarAsync` (RN02, RN03, orquestração), `ListarAsync`, `TopClientesAsync`; validação de filtros; testes unitários com repositórios mockados.
7. **Infraestrutura** — `PedidosDbContext`, configurações de entidade, repositórios, baixa de estoque atômica (`UPDATE ... WHERE Estoque >= @q`) na mesma transação do pedido.
8. **API** — endpoints, mapeamento de erros, Swagger, `/health`.
9. **Testes de integração** — fixture Testcontainers que executa os **mesmos scripts** de `database/`; cenários CA01–CA15.
10. **Frontend** — cliente HTTP tipado, listagem com filtros/paginação, modal de criação, top clientes.
11. **Docker** — Dockerfiles multi-stage, `docker-compose.yml` com `sqlserver` → `db-init` (roda scripts) → `api` → `frontend`.
12. **CI** — GitHub Actions: build + testes .NET, lint + build frontend, e2e com compose.
13. **E2E** — Playwright: criar pedido e vê-lo na listagem; erro de estoque insuficiente.
14. **Parte 4** — reproduzir a query lenta com volume, analisar plano de execução, corrigir, documentar.
15. **README**.

## 3. Ajustes e discordâncias em relação à proposta inicial do Claude

| # | Proposta inicial | Decisão final | Por quê |
|---|---|---|---|
| 1 | Criar o schema com **EF Core Migrations** e gerar os scripts a partir delas. | Scripts SQL escritos à mão em `database/` são a fonte da verdade; EF só mapeia. | O teste pede scripts de criação explícitos e índices pensados para as consultas. Manter migrations *e* scripts seria duas fontes da verdade. Os testes de integração executam os mesmos scripts, então qualquer divergência entre script e mapeamento EF quebra o teste. |
| 2 | Coluna `ValorTotal` em `Pedidos`. | Não armazenar; calcular a partir dos itens com índice coberto. | Evita inconsistência entre total e itens. Pedido não é editável, então seria seguro, mas não há necessidade de performance que justifique agora. Registrado como evolução. |
| 3 | Validar estoque com `SELECT` e depois `UPDATE` sem controle de concorrência. | **1ª versão:** `ROWVERSION` + concorrência otimista do EF → 409. **Versão final:** `UPDATE Produtos SET Estoque = Estoque - @q WHERE Id = @id AND Estoque >= @q` na transação do pedido; 0 linhas afetadas → 422. | A proposta tinha race condition (vender o último item duas vezes). A 1ª correção (otimista) resolvia isso, mas o **e2e rodando em paralelo revelou falsos 409**: dois pedidos do mesmo produto com estoque sobrando conflitavam no RowVersion. Tentei *retry* com releitura (3 tentativas) e um teste de integração com 5 pedidos simultâneos falhou 5/5 — sob contenção eles colidem de novo a cada rodada. O UPDATE condicional serializa no lock da linha e só falha quando realmente não há estoque. `CHECK (Estoque >= 0)` continua como última barreira. Ver commits `fix(pedidos)`. |
| 4 | Retornar **404** para cliente inexistente no `POST /pedidos`. | **422** com `code`. | O recurso `/pedidos` existe; o problema é o conteúdo. 404 confundiria o frontend com "rota não existe". |
| 5 | Lançar exceções de domínio e tratar num middleware. | `Result<T>`/`Error` explícitos; exceção só para o que é excepcional (estoque esgotado no meio da gravação, infra). | Regras de negócio violadas são fluxo esperado, não exceção. Fica mais fácil testar e o mapeamento para HTTP fica num único lugar. |
| 6 | `DateTime.UtcNow` direto no domínio. | `TimeProvider` injetado. | Permite testar a data do pedido de forma determinística. |
| 7 | Usar **FluentAssertions** nos testes. | `Assert` do xUnit. | A partir da v8 o FluentAssertions tem licença comercial; para um projeto pequeno o xUnit basta e evita a dependência. |
| 8 | Testcontainers com `EnsureCreated()` do EF para criar o schema. | Rodar `database/02-create-tables.sql` no container. | `EnsureCreated` criaria um schema diferente do real (sem os índices/CHECKs dos scripts) e o teste não provaria nada sobre o banco de verdade. |
| 9 | SQL Server inicializado via script de entrypoint customizado na própria imagem do SQL Server. | Serviço `db-init` separado (mesma imagem, só roda `sqlcmd`) com `depends_on: service_healthy`, e a API com `service_completed_successfully`. | Mais simples e previsível que sobrescrever o entrypoint do SQL Server; a ordem de subida fica declarada no compose. |
| 10 | Validar Docker e testes de integração localmente. | Validação via **GitHub Actions**. | Durante o desenvolvimento o Docker Desktop da máquina estava indisponível (serviço do WSL desativado). O pipeline de CI, que roda em runner com Docker, passou a ser a verificação de que `docker compose up` e Testcontainers funcionam. Ver README. |

## 4. Riscos e como foram tratados

- **SQL Server demora a subir** → healthcheck com `sqlcmd` e `start_period`; `db-init` só roda com o banco saudável.
- **Imagem do SQL Server é só x64** → documentado; em Apple Silicon usar Rosetta.
- **Datas/fuso** → tudo em UTC, filtros inclusivos convertidos para intervalo semiaberto `[inicio, fim + 1 dia)` (sargável).
