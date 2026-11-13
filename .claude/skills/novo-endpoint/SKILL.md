---
name: novo-endpoint
description: Cria um novo endpoint HTTP na API de Pedidos seguindo o padrão do projeto (Domain → Application → Infrastructure → Api), com DTOs, Result/Error, mapeamento ProblemDetails, teste unitário e teste de integração. Use quando o usuário pedir "novo endpoint", "nova rota", "expor X na API" ou similar.
---

# Criar novo endpoint seguindo o padrão do projeto

Você vai adicionar um endpoint à API de Pedidos. Siga **todas** as etapas, nesta ordem. Não pule os testes.

## 0. Entenda o pedido antes de codar

1. Leia `docs/spec.md` e confirme se o endpoint já está especificado. Se **não** estiver, escreva primeiro a seção do endpoint na spec (rota, query/body, respostas com status e `code`, critérios de aceite `CAxx`) e mostre ao usuário antes de implementar.
2. Defina: verbo + rota (minúscula, plural), parâmetros, formato de resposta, erros possíveis.

## 1. Application (`src/Pedidos.Application`)

- DTOs de entrada/saída como `record` em `<Recurso>/<Recurso>Dtos.cs`.
- Se for leitura: adicione o método na interface `I<Recurso>Queries` (ex.: `IPedidoQueries`). Se for escrita: em `I<Recurso>Repository`.
- Caso de uso no service do recurso (`<Recurso>Service`), retornando `Result<T>`.
  - Validação de entrada → `Errors.Validacao(campo, mensagem)` (vira 400).
  - Regra de negócio → um `Error` novo em `Common/Errors.cs` com `code` em `snake_case` e `ErrorType.Regra` (vira 422) ou `ErrorType.NaoEncontrado` (404).
  - **Nunca** lance exceção para regra de negócio.

## 2. Infrastructure (`src/Pedidos.Infrastructure`)

- Implemente o método na classe concreta (`<Recurso>Queries` / `<Recurso>Repository`).
- Leitura: `AsNoTracking()` + projeção direta para o DTO (`Select(...)`), nunca carregue a entidade inteira só para mapear.
- Filtro de data: compare a coluna com intervalo semiaberto (`>= inicio && < fim.AddDays(1)`), nunca `.Date`/`.Year` na coluna.
- Se precisar de coluna/índice novo: altere `database/02-create-tables.sql` (de forma idempotente) **e** a `Configurations/<Entidade>Configuration.cs`.

## 3. Api (`src/Pedidos.Api/Endpoints/<Recurso>Endpoints.cs`)

```csharp
group.MapGet("/rota", async ([AsParameters] FiltroDto filtro, XService service, CancellationToken ct) =>
        (await service.MetodoAsync(filtro, ct)).ToHttpResult())
    .WithName("NomeUnico")
    .WithSummary("Descrição curta em português")
    .Produces<RespostaDto>()
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
```

- Se for um recurso novo, crie `Map<Recurso>Endpoints` e chame em `Program.cs`.
- Use `ToHttpResult()` / `ToCreatedResult(...)` de `ResultExtensions` — não monte ProblemDetails na mão.

## 4. Testes

- **Unitário** (`tests/Pedidos.UnitTests/Application/<Recurso>ServiceTests.cs`): um teste por regra/erro, com NSubstitute para os repositórios. Nome `Metodo_Cenario_Resultado`.
- **Integração** (`tests/Pedidos.IntegrationTests/<Recurso>EndpointsTests.cs`): use `ApiFactory` (Testcontainers) e o `HttpClient`; crie os próprios dados com `ApiFactory.SeedAsync` para não depender da ordem dos testes. Cubra o caminho feliz e cada status de erro.

## 5. Verificação final

```bash
dotnet build Pedidos.sln
dotnet test tests/Pedidos.UnitTests
dotnet test tests/Pedidos.IntegrationTests   # se o Docker estiver disponível
```

Se houver frontend consumindo o endpoint, adicione a função em `frontend/src/api/` e o tipo em `frontend/src/api/types.ts`.

Termine com um resumo: arquivos alterados, como testar manualmente (exemplo `curl`) e sugestão de mensagem de commit (`feat(api): ...`).
