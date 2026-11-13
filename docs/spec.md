# Especificação — Mini Gestão de Pedidos

> Status: aprovada para implementação · Versão 1.0 · 13/11/2026

## 1. Objetivo

Permitir que um operador **cadastre pedidos de clientes** (com um ou mais itens de produtos em estoque) e **consulte** esses pedidos com filtros e paginação, além de obter um **relatório dos 5 clientes que mais compraram** num período.

Fora de escopo nesta versão: autenticação, edição/cancelamento de pedidos, cadastro de clientes e produtos pela interface (os dados vêm do script de carga), multi-moeda e fuso horário por usuário.

## 2. Glossário

| Termo | Significado |
|---|---|
| Cliente | Pessoa/empresa que faz pedidos. |
| Produto | Item vendável, com preço atual e quantidade em estoque. |
| Pedido | Compra de um cliente numa data, composta por itens. |
| Item do pedido | Produto + quantidade + **preço unitário congelado no momento da compra**. |
| Valor total do pedido | `Σ (Quantidade × PrecoUnitario)` dos itens. |

## 3. Regras de negócio

| ID | Regra |
|---|---|
| RN01 | Um pedido deve ter **pelo menos 1 item**. |
| RN02 | O **cliente** informado deve existir. |
| RN03 | Todo **produto** informado deve existir. |
| RN04 | A **quantidade** de cada item deve ser maior que zero (e no máximo 10.000, para evitar overflow e erros de digitação). |
| RN05 | Deve haver **estoque suficiente** para cada produto. Se qualquer item não tiver estoque, **nada** é gravado (o pedido é atômico). |
| RN06 | Ao criar o pedido, o estoque de cada produto é **baixado** na mesma transação. |
| RN07 | O preço unitário do item é **copiado do produto** no momento da criação (o cliente não informa preço). Alterações futuras de preço não afetam pedidos já feitos. |
| RN08 | Se o mesmo produto aparecer em mais de uma linha da requisição, as quantidades são **somadas num único item** (e o estoque é validado sobre a soma). |
| RN09 | A data do pedido é definida pelo **servidor** (UTC), não pelo cliente. |
| RN10 | Concorrência: pedidos simultâneos do mesmo produto **não podem vender mais do que o estoque** nem falhar à toa. A baixa é um `UPDATE` condicional atômico (`WHERE Estoque >= @quantidade`); se outro pedido levou o último item entre a validação e a gravação, nada é gravado e a resposta é **422 `estoque_insuficiente`**. Se ainda há estoque, todos os pedidos são gravados. |
| RN11 | Filtros de período usam datas **inclusivas** (`dataInicio` 00:00 até `dataFim` 23:59:59.999…), em UTC. `dataInicio` não pode ser maior que `dataFim`. |
| RN12 | O ranking de top clientes considera o valor total dos pedidos no período, ordenado do maior para o menor; empate é desempatado pelo menor `clienteId` (resultado determinístico). |

## 4. Modelo de dados (SQL Server)

```
Clientes (1) ───< (N) Pedidos (1) ───< (N) ItensPedido (N) >─── (1) Produtos
```

| Tabela | Coluna | Tipo | Restrições |
|---|---|---|---|
| **Clientes** | Id | INT IDENTITY | PK |
| | Nome | NVARCHAR(150) | NOT NULL |
| | Email | NVARCHAR(200) | NOT NULL, UNIQUE |
| | DataCadastro | DATETIME2(0) | NOT NULL, default `SYSUTCDATETIME()` |
| **Produtos** | Id | INT IDENTITY | PK |
| | Nome | NVARCHAR(150) | NOT NULL |
| | Preco | DECIMAL(18,2) | NOT NULL, CHECK ≥ 0 |
| | Estoque | INT | NOT NULL, CHECK ≥ 0 |
| **Pedidos** | Id | INT IDENTITY | PK |
| | ClienteId | INT | NOT NULL, FK → Clientes |
| | DataPedido | DATETIME2(3) | NOT NULL, default `SYSUTCDATETIME()` |
| **ItensPedido** | Id | INT IDENTITY | PK |
| | PedidoId | INT | NOT NULL, FK → Pedidos (ON DELETE CASCADE) |
| | ProdutoId | INT | NOT NULL, FK → Produtos |
| | Quantidade | INT | NOT NULL, CHECK > 0 |
| | PrecoUnitario | DECIMAL(18,2) | NOT NULL, CHECK ≥ 0 |

### Índices (e a consulta que justifica cada um)

| Índice | Colunas | Atende |
|---|---|---|
| `UX_Clientes_Email` | (Email) UNIQUE | unicidade de e-mail |
| `IX_Pedidos_ClienteId_DataPedido` | (ClienteId, DataPedido DESC) | `GET /pedidos?clienteId=&dataInicio=&dataFim=` (seek por cliente + range de data) e FK |
| `IX_Pedidos_DataPedido` | (DataPedido DESC) INCLUDE (ClienteId) | `GET /pedidos` só por período e relatório de top clientes (range seek + cobre ClienteId) |
| `UX_ItensPedido_PedidoId_ProdutoId` | (PedidoId, ProdutoId) UNIQUE INCLUDE (Quantidade, PrecoUnitario) | RN08 no banco + soma dos itens por pedido sem lookup |
| `IX_ItensPedido_ProdutoId` | (ProdutoId) | FK (evita scan ao validar deletes em Produtos) |

O valor total **não é armazenado** em `Pedidos`: é sempre calculado a partir dos itens (uma única fonte da verdade). Com o índice coberto em `ItensPedido`, a soma é barata. Se o volume crescer muito, uma coluna `ValorTotal` desnormalizada é a evolução natural.

## 5. API

Base: `http://localhost:8080` (no Docker o frontend acessa via `/api`). Erros seguem **RFC 7807 (`application/problem+json`)**, com uma propriedade `code` estável que o frontend pode usar.

### 5.1 `POST /pedidos`

Request:
```json
{ "clienteId": 1, "itens": [ { "produtoId": 3, "quantidade": 2 } ] }
```

Respostas:

| Status | Quando | `code` |
|---|---|---|
| 201 Created | Pedido criado. Header `Location: /pedidos/{id}`. Body = pedido criado (formato do item de listagem). | — |
| 400 Bad Request | Sem itens (RN01), quantidade inválida (RN04), `clienteId`/`produtoId` ≤ 0. Body `ValidationProblemDetails` com `errors` por campo. | `validacao` |
| 422 Unprocessable Entity | Cliente inexistente (RN02) | `cliente_nao_encontrado` |
| 422 Unprocessable Entity | Produto inexistente (RN03) | `produto_nao_encontrado` |
| 422 Unprocessable Entity | Estoque insuficiente (RN05), inclusive quando esgota durante a gravação (RN10). Mensagem cita o produto. | `estoque_insuficiente` |

> Por que 422 e não 404 para cliente inexistente? O recurso da URL (`/pedidos`) existe; o que está errado é o **conteúdo** da requisição, que é sintaticamente válido mas viola uma regra. 400 fica reservado para requisição malformada.

### 5.2 `GET /pedidos/{id}`

Retorna um pedido (200) ou 404. Existe para dar sentido ao header `Location`.

### 5.3 `GET /pedidos`

Query string:

| Parâmetro | Tipo | Padrão | Regra |
|---|---|---|---|
| `clienteId` | int | — | opcional |
| `dataInicio` | date (`yyyy-MM-dd`) | — | opcional, inclusiva |
| `dataFim` | date (`yyyy-MM-dd`) | — | opcional, inclusiva; ≥ `dataInicio` |
| `pagina` | int | 1 | ≥ 1 |
| `tamanhoPagina` | int | 10 | 1..100 |

Ordenação: `dataPedido DESC, id DESC` (mais recentes primeiro, estável entre páginas).

Response 200:
```json
{
  "itens": [
    {
      "id": 42, "clienteId": 1, "clienteNome": "Ana Souza",
      "dataPedido": "2026-11-13T10:00:00Z", "valorTotal": 199.80,
      "itens": [ { "produtoId": 3, "produtoNome": "Teclado", "quantidade": 2, "precoUnitario": 99.90, "subtotal": 199.80 } ]
    }
  ],
  "pagina": 1, "tamanhoPagina": 10, "totalItens": 57, "totalPaginas": 6
}
```

400 se `pagina`/`tamanhoPagina` fora da faixa ou `dataInicio > dataFim`.

### 5.4 `GET /relatorios/top-clientes`

Query: `dataInicio` e `dataFim` (**obrigatórios**, inclusivos, `dataInicio ≤ dataFim`).

Response 200 (no máximo 5 linhas, RN12):
```json
[ { "clienteId": 1, "clienteNome": "Ana Souza", "quantidadePedidos": 4, "valorTotal": 1520.50 } ]
```

400 se faltar data ou o intervalo for inválido.

### 5.5 Endpoints de apoio (para o frontend)

- `GET /clientes` → `[{ id, nome, email }]` ordenado por nome.
- `GET /produtos` → `[{ id, nome, preco, estoque }]` ordenado por nome.
- `GET /health` → 200 quando a API está de pé e o banco responde.

## 6. Frontend

1. **Listagem de pedidos**: tabela com data, cliente, nº de itens e valor total; filtros por cliente (select), data início e data fim; paginação (anterior/próxima + "página X de Y"); estados de carregando, vazio e erro; linha expansível mostra os itens.
2. **Modal "Novo pedido"**: select de cliente; linhas de item (produto + quantidade, com estoque disponível visível); adicionar/remover linha; total calculado em tempo real; botão desabilitado enquanto inválido ou enviando; erros da API (ex.: estoque insuficiente) exibidos no modal sem perder o que foi digitado. Ao salvar com sucesso: fecha o modal e recarrega a listagem (o novo pedido aparece no topo).
3. **Top clientes** (extra): seleção de período e tabela com o ranking.

## 7. Critérios de aceite

### Criação de pedido
- **CA01** — Dado um cliente existente e produtos com estoque, quando envio um pedido válido, então recebo 201, o pedido aparece em `GET /pedidos` e o estoque de cada produto diminui na quantidade pedida.
- **CA02** — Dado um pedido sem itens, então recebo 400 e nada é gravado.
- **CA03** — Dado um item com quantidade 0 ou negativa, então recebo 400.
- **CA04** — Dado um `clienteId` inexistente, então recebo 422 com `code = cliente_nao_encontrado`.
- **CA05** — Dado um `produtoId` inexistente, então recebo 422 com `code = produto_nao_encontrado`.
- **CA06** — Dado um produto com estoque 5 e pedido de 6, então recebo 422 `estoque_insuficiente`, e o estoque **de todos os produtos** do pedido permanece inalterado.
- **CA07** — Dado o mesmo produto em duas linhas (2 + 3), então é gravado um único item com quantidade 5.
- **CA08** — O preço unitário gravado é o do produto no momento do pedido, independentemente do que o cliente envie.

### Listagem
- **CA09** — Sem filtros, retorna página 1 com até 10 pedidos, do mais recente ao mais antigo, e o total correto.
- **CA10** — Com `clienteId`, retorna apenas pedidos daquele cliente.
- **CA11** — Com `dataInicio`/`dataFim`, retorna pedidos dentro do intervalo, **inclusive** os do último dia.
- **CA12** — `tamanhoPagina = 0`, `> 100` ou `dataInicio > dataFim` → 400.

### Relatório
- **CA13** — Retorna no máximo 5 clientes, ordenados por valor total decrescente, considerando apenas pedidos no período.
- **CA14** — Clientes com o mesmo nome **não** são somados juntos (agrupamento por Id).
- **CA15** — Sem `dataInicio` ou `dataFim` → 400.

### Não funcionais
- **CA16** — `docker compose up` sobe banco (com schema e carga), API e frontend sem nenhum passo manual.
- **CA17** — Testes unitários cobrem RN01–RN08; testes de integração cobrem os 3 endpoints principais contra SQL Server real.
