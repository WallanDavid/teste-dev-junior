// Espelha os DTOs da API (Pedidos.Application). Mantenha em sincronia com docs/spec.md.

export interface ItemPedido {
  produtoId: number
  produtoNome: string
  quantidade: number
  precoUnitario: number
  subtotal: number
}

export interface Pedido {
  id: number
  clienteId: number
  clienteNome: string
  /** ISO 8601 em UTC (termina com "Z"). */
  dataPedido: string
  valorTotal: number
  itens: ItemPedido[]
}

export interface Pagina<T> {
  itens: T[]
  pagina: number
  tamanhoPagina: number
  totalItens: number
  totalPaginas: number
}

export interface FiltroPedidos {
  clienteId?: number
  /** yyyy-MM-dd, inclusiva */
  dataInicio?: string
  /** yyyy-MM-dd, inclusiva */
  dataFim?: string
  pagina?: number
  tamanhoPagina?: number
}

export interface CriarPedido {
  clienteId: number
  itens: { produtoId: number; quantidade: number }[]
}

export interface Cliente {
  id: number
  nome: string
  email: string
}

export interface Produto {
  id: number
  nome: string
  preco: number
  estoque: number
}

export interface TopCliente {
  clienteId: number
  clienteNome: string
  quantidadePedidos: number
  valorTotal: number
}

/** RFC 7807 + extensões da API. */
export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  code?: string
  errors?: Record<string, string[]>
}
