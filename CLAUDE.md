# CLAUDE.md — Mini Gestão de Pedidos

Contexto para o Claude Code neste repositório. Antes de implementar algo novo, leia `docs/spec.md` (regras RNxx e critérios CAxx) e `docs/plan.md`.

## Stack

- Backend: .NET 8, ASP.NET Core Minimal APIs, EF Core 8 (SQL Server), xUnit, NSubstitute, Testcontainers.
- Banco: SQL Server 2022. **O schema é definido pelos scripts em `database/`**, não por migrations.
- Frontend: React 18 + TypeScript + Vite (sem biblioteca de UI; CSS em `src/styles.css`).
- E2E: Playwright (`e2e/`). Infra: Docker Compose. CI: GitHub Actions.

## Arquitetura (backend)

```
Pedidos.Api  ──>  Pedidos.Application  ──>  Pedidos.Domain
     └──> Pedidos.Infrastructure ──┘ (implementa interfaces da Application)
```

- **Domain**: entidades e regras puras. Não referencia EF nem ASP.NET. Regras de criação de pedido ficam em `Pedido.Criar`.
- **Application**: casos de uso (`PedidoService`), DTOs (`record`), `Result<T>`/`Error`, interfaces `I*Repository`/`I*Queries`.
- **Infrastructure**: `PedidosDbContext`, `Configurations/*Configuration.cs` (Fluent API), repositórios e queries.
- **Api**: um arquivo `Endpoints/*Endpoints.cs` por recurso, com método de extensão `Map*Endpoints`. Erros viram ProblemDetails em `ResultExtensions.ToHttpResult`.

### Regras que não podem ser quebradas

- Violação de regra de negócio → `Error` (nunca `throw`). Exceção só para falha de infraestrutura/concorrência.
- Códigos de erro (`Error.Code`) em `snake_case` e estáveis (o frontend depende deles). Catálogo em `Pedidos.Application/Common/Errors.cs`.
- Validação de entrada → 400 (`validacao`); regra de negócio → 422; concorrência → 409.
- Datas em UTC. Filtros de data são inclusivos e convertidos para `[inicio, fim + 1 dia)` — **nunca** use `YEAR()`/`CAST` em coluna no `WHERE` (não é sargável).
- Toda mudança de schema: alterar `database/02-create-tables.sql` (idempotente) **e** a `*Configuration.cs` correspondente. Os testes de integração rodam o script, então divergência quebra o teste.
- Não adicionar pacotes NuGet/npm sem necessidade clara.

## Convenções de nomenclatura

- Código de domínio em **português** (`Pedido`, `ItemPedido`, `ClienteId`), seguindo a linguagem do negócio; termos técnicos em inglês (`Repository`, `Service`, `Dto`, `Configuration`).
- C#: `PascalCase` para tipos/membros, `_camelCase` para campos privados, `Async` no sufixo de métodos assíncronos, namespaces *file-scoped*, `record` para DTOs.
- Tabelas no plural (`Pedidos`), PK `Id`, FK `<Entidade>Id`, índices `IX_<Tabela>_<Colunas>`, únicos `UX_...`.
- Rotas em minúsculas e no plural (`/pedidos`, `/relatorios/top-clientes`); JSON em `camelCase`.
- Testes: `Metodo_Cenario_ResultadoEsperado` (ex.: `Criar_SemItens_RetornaErroDeValidacao`).
- Frontend: componentes `PascalCase.tsx`, hooks `useAlgo.ts`, chamadas HTTP só em `src/api/`.
- Commits: Conventional Commits em português (`feat(api): ...`, `test: ...`, `fix(db): ...`), pequenos e frequentes. **Nunca** reescrever histórico (sem squash, rebase, amend ou push --force).

## Como rodar

```bash
# tudo no Docker (banco + carga + API + frontend)
docker compose up --build            # frontend: http://localhost:3000  ·  API/Swagger: http://localhost:8080/swagger

# backend
dotnet build Pedidos.sln
dotnet test tests/Pedidos.UnitTests
dotnet test tests/Pedidos.IntegrationTests   # requer Docker (Testcontainers)

# frontend
cd frontend && npm ci && npm run dev  # usa proxy /api -> http://localhost:8080
npm run lint && npm run build

# e2e (com o compose no ar)
cd e2e && npm ci && npx playwright install chromium && npx playwright test
```

## Ferramentas do Claude neste repo

- **Hooks** (`.claude/settings.json`):
  - `PostToolUse` em `Edit|Write` → `.claude/hooks/post-edit.mjs`: compila o projeto .NET afetado ao editar `.cs`/`.csproj` e roda `tsc --noEmit` ao editar `.ts`/`.tsx`. Erros voltam para o Claude corrigir.
  - `PreToolUse` em `Bash` → `.claude/hooks/guard-git.mjs`: bloqueia comandos que reescrevem histórico.
- **Skills** (`.claude/skills/`): `novo-endpoint` (cria endpoint seguindo o padrão das camadas + testes) e `analisar-query` (investiga query lenta via MCP).
- **MCP** (`.mcp.json`): `sqlserver` (DBHub) conectado ao SQL Server do compose (`localhost:1433`, banco `Pedidos`). Use para inspecionar schema, conferir dados de carga e analisar planos de execução. **Somente leitura na prática**: não use o MCP para alterar schema — altere os scripts.
