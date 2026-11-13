import { http } from './client'
import type { Cliente, CriarPedido, FiltroPedidos, Pagina, Pedido, Produto, TopCliente } from './types'

export { ApiError } from './client'
export type * from './types'

export const api = {
  listarPedidos: (filtro: FiltroPedidos, signal?: AbortSignal) =>
    http.get<Pagina<Pedido>>('/pedidos', { ...filtro }, signal),

  criarPedido: (pedido: CriarPedido) => http.post<Pedido>('/pedidos', pedido),

  topClientes: (dataInicio: string, dataFim: string, signal?: AbortSignal) =>
    http.get<TopCliente[]>('/relatorios/top-clientes', { dataInicio, dataFim }, signal),

  listarClientes: (signal?: AbortSignal) => http.get<Cliente[]>('/clientes', undefined, signal),

  listarProdutos: (signal?: AbortSignal) => http.get<Produto[]>('/produtos', undefined, signal),
}
