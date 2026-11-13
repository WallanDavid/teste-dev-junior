import { useState } from 'react'
import { api } from './api'
import { PedidosPage } from './components/PedidosPage'
import { TopClientesPage } from './components/TopClientesPage'
import { useAsync } from './hooks/useAsync'

type Aba = 'pedidos' | 'top'

export default function App() {
  const [aba, setAba] = useState<Aba>('pedidos')
  const clientes = useAsync((signal) => api.listarClientes(signal), [])

  return (
    <>
      <header className="topo">
        <h1>Mini Gestão de Pedidos</h1>
        <nav aria-label="Seções">
          <button type="button" aria-current={aba === 'pedidos' ? 'page' : undefined} onClick={() => setAba('pedidos')}>
            Pedidos
          </button>
          <button type="button" aria-current={aba === 'top' ? 'page' : undefined} onClick={() => setAba('top')}>
            Top clientes
          </button>
        </nav>
      </header>

      <main>
        {clientes.error && (
          <p className="alerta erro" role="alert">
            {clientes.error.message}
          </p>
        )}
        {aba === 'pedidos' ? <PedidosPage clientes={clientes.data ?? []} /> : <TopClientesPage />}
      </main>
    </>
  )
}
