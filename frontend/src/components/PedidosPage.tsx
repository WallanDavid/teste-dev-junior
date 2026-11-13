import { Fragment, useState, type FormEvent } from 'react'
import { api, type Cliente, type FiltroPedidos } from '../api'
import { formatarDataHora, formatarMoeda } from '../format'
import { useAsync } from '../hooks/useAsync'
import { NovoPedidoModal } from './NovoPedidoModal'

const TAMANHO_PAGINA = 10

interface Props {
  clientes: Cliente[]
}

export function PedidosPage({ clientes }: Props) {
  const [filtro, setFiltro] = useState<FiltroPedidos>({ pagina: 1, tamanhoPagina: TAMANHO_PAGINA })
  const [recarregar, setRecarregar] = useState(0)
  const [modalAberto, setModalAberto] = useState(false)
  const [expandido, setExpandido] = useState<number | null>(null)
  const [mensagem, setMensagem] = useState<string | null>(null)

  const { data, error, loading } = useAsync(
    (signal) => api.listarPedidos(filtro, signal),
    [filtro, recarregar],
  )

  function aplicarFiltros(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = new FormData(e.currentTarget)
    const clienteId = Number(form.get('clienteId')) || undefined
    setFiltro({
      clienteId,
      dataInicio: (form.get('dataInicio') as string) || undefined,
      dataFim: (form.get('dataFim') as string) || undefined,
      pagina: 1,
      tamanhoPagina: TAMANHO_PAGINA,
    })
  }

  function limparFiltros(e: FormEvent<HTMLFormElement>) {
    e.currentTarget.reset()
    setFiltro({ pagina: 1, tamanhoPagina: TAMANHO_PAGINA })
  }

  const irParaPagina = (pagina: number) => setFiltro((f) => ({ ...f, pagina }))

  return (
    <section aria-labelledby="titulo-pedidos">
      <div className="cabecalho">
        <h2 id="titulo-pedidos">Pedidos</h2>
        <button type="button" className="primario" onClick={() => setModalAberto(true)}>
          + Novo pedido
        </button>
      </div>

      {mensagem && (
        <p className="alerta sucesso" role="status">
          {mensagem}
        </p>
      )}

      <form className="filtros" onSubmit={aplicarFiltros} onReset={limparFiltros} aria-label="Filtros">
        <label>
          Cliente
          <select name="clienteId" defaultValue="">
            <option value="">Todos</option>
            {clientes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nome} ({c.email})
              </option>
            ))}
          </select>
        </label>
        <label>
          Data inicial
          <input type="date" name="dataInicio" />
        </label>
        <label>
          Data final
          <input type="date" name="dataFim" />
        </label>
        <div className="acoes">
          <button type="submit">Filtrar</button>
          <button type="reset" className="secundario">
            Limpar
          </button>
        </div>
      </form>

      {error && (
        <p className="alerta erro" role="alert">
          {error.message}
        </p>
      )}

      <table aria-label="Lista de pedidos" aria-busy={loading}>
        <thead>
          <tr>
            <th scope="col">Nº</th>
            <th scope="col">Data</th>
            <th scope="col">Cliente</th>
            <th scope="col" className="num">
              Itens
            </th>
            <th scope="col" className="num">
              Total
            </th>
            <th scope="col">
              <span className="sr-only">Ações</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {loading && !data && (
            <tr>
              <td colSpan={6} className="vazio">
                Carregando…
              </td>
            </tr>
          )}
          {data?.itens.length === 0 && (
            <tr>
              <td colSpan={6} className="vazio">
                Nenhum pedido encontrado para os filtros informados.
              </td>
            </tr>
          )}
          {data?.itens.map((p) => (
            <Fragment key={p.id}>
              <tr data-testid={`pedido-${p.id}`}>
                <td>#{p.id}</td>
                <td>{formatarDataHora(p.dataPedido)}</td>
                <td>{p.clienteNome}</td>
                <td className="num">{p.itens.reduce((s, i) => s + i.quantidade, 0)}</td>
                <td className="num">{formatarMoeda(p.valorTotal)}</td>
                <td>
                  <button
                    type="button"
                    className="link"
                    aria-expanded={expandido === p.id}
                    onClick={() => setExpandido(expandido === p.id ? null : p.id)}
                  >
                    {expandido === p.id ? 'Ocultar itens' : 'Ver itens'}
                  </button>
                </td>
              </tr>
              {expandido === p.id && (
                <tr className="detalhe">
                  <td colSpan={6}>
                    <ul>
                      {p.itens.map((i) => (
                        <li key={i.produtoId}>
                          {i.quantidade} × {i.produtoNome} — {formatarMoeda(i.precoUnitario)} ={' '}
                          <strong>{formatarMoeda(i.subtotal)}</strong>
                        </li>
                      ))}
                    </ul>
                  </td>
                </tr>
              )}
            </Fragment>
          ))}
        </tbody>
      </table>

      {data && data.totalPaginas > 0 && (
        <nav className="paginacao" aria-label="Paginação">
          <button type="button" disabled={data.pagina <= 1 || loading} onClick={() => irParaPagina(data.pagina - 1)}>
            ← Anterior
          </button>
          <span>
            Página {data.pagina} de {data.totalPaginas} · {data.totalItens} pedido(s)
          </span>
          <button
            type="button"
            disabled={data.pagina >= data.totalPaginas || loading}
            onClick={() => irParaPagina(data.pagina + 1)}
          >
            Próxima →
          </button>
        </nav>
      )}

      {modalAberto && (
        <NovoPedidoModal
          clientes={clientes}
          onClose={() => setModalAberto(false)}
          onCreated={(pedido) => {
            setModalAberto(false)
            setMensagem(`Pedido #${pedido.id} criado com sucesso (${formatarMoeda(pedido.valorTotal)}).`)
            // Volta para a primeira página (o novo pedido é o mais recente) e força o recarregamento.
            setFiltro((f) => ({ ...f, pagina: 1 }))
            setRecarregar((n) => n + 1)
          }}
        />
      )}
    </section>
  )
}
