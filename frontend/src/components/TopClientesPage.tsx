import { useState, type FormEvent } from 'react'
import { api } from '../api'
import { formatarMoeda } from '../format'
import { useAsync } from '../hooks/useAsync'

export function TopClientesPage() {
  const [periodo, setPeriodo] = useState(() => {
    const ano = new Date().getFullYear()
    return { dataInicio: `${ano}-01-01`, dataFim: `${ano}-12-31` }
  })

  const { data, error, loading } = useAsync(
    (signal) => api.topClientes(periodo.dataInicio, periodo.dataFim, signal),
    [periodo],
  )

  function consultar(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = new FormData(e.currentTarget)
    setPeriodo({ dataInicio: String(form.get('dataInicio')), dataFim: String(form.get('dataFim')) })
  }

  return (
    <section aria-labelledby="titulo-top">
      <h2 id="titulo-top">Top 5 clientes</h2>

      <form className="filtros" onSubmit={consultar}>
        <label>
          Data inicial
          <input type="date" name="dataInicio" defaultValue={periodo.dataInicio} required />
        </label>
        <label>
          Data final
          <input type="date" name="dataFim" defaultValue={periodo.dataFim} required />
        </label>
        <div className="acoes">
          <button type="submit">Consultar</button>
        </div>
      </form>

      {error && (
        <p className="alerta erro" role="alert">
          {error.message}
        </p>
      )}

      <table aria-label="Ranking de clientes" aria-busy={loading}>
        <thead>
          <tr>
            <th scope="col">#</th>
            <th scope="col">Cliente</th>
            <th scope="col" className="num">
              Pedidos
            </th>
            <th scope="col" className="num">
              Valor total
            </th>
          </tr>
        </thead>
        <tbody>
          {data?.length === 0 && (
            <tr>
              <td colSpan={4} className="vazio">
                Nenhum pedido no período.
              </td>
            </tr>
          )}
          {data?.map((c, i) => (
            <tr key={c.clienteId}>
              <td>{i + 1}º</td>
              <td>{c.clienteNome}</td>
              <td className="num">{c.quantidadePedidos}</td>
              <td className="num">{formatarMoeda(c.valorTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
