import { useEffect, useId, useMemo, useState, type FormEvent } from 'react'
import { api, ApiError, type Cliente, type Pedido } from '../api'
import { formatarMoeda } from '../format'
import { useAsync } from '../hooks/useAsync'

interface Linha {
  chave: number
  produtoId: string
  quantidade: string
}

interface Props {
  clientes: Cliente[]
  onClose: () => void
  onCreated: (pedido: Pedido) => void
}

let proximaChave = 1
const novaLinha = (): Linha => ({ chave: proximaChave++, produtoId: '', quantidade: '1' })

export function NovoPedidoModal({ clientes, onClose, onCreated }: Props) {
  const tituloId = useId()
  const [clienteId, setClienteId] = useState('')
  const [linhas, setLinhas] = useState<Linha[]>(() => [novaLinha()])
  const [enviando, setEnviando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  // Produtos são recarregados a cada abertura do modal para refletir o estoque atual.
  const produtos = useAsync((signal) => api.listarProdutos(signal), [])
  const produtoPorId = useMemo(() => new Map((produtos.data ?? []).map((p) => [p.id, p])), [produtos.data])

  useEffect(() => {
    const fecharComEsc = (e: KeyboardEvent) => e.key === 'Escape' && !enviando && onClose()
    window.addEventListener('keydown', fecharComEsc)
    return () => window.removeEventListener('keydown', fecharComEsc)
  }, [enviando, onClose])

  const total = linhas.reduce((soma, l) => {
    const produto = produtoPorId.get(Number(l.produtoId))
    return soma + (produto ? produto.preco * (Number(l.quantidade) || 0) : 0)
  }, 0)

  const valido =
    clienteId !== '' &&
    linhas.length > 0 &&
    linhas.every((l) => l.produtoId !== '' && Number.isInteger(Number(l.quantidade)) && Number(l.quantidade) > 0)

  const atualizar = (chave: number, campo: 'produtoId' | 'quantidade', valor: string) =>
    setLinhas((ls) => ls.map((l) => (l.chave === chave ? { ...l, [campo]: valor } : l)))

  async function salvar(e: FormEvent) {
    e.preventDefault()
    if (!valido || enviando) return
    setEnviando(true)
    setErro(null)
    try {
      const pedido = await api.criarPedido({
        clienteId: Number(clienteId),
        itens: linhas.map((l) => ({ produtoId: Number(l.produtoId), quantidade: Number(l.quantidade) })),
      })
      onCreated(pedido)
    } catch (err) {
      // Mantém o formulário preenchido para o usuário corrigir (ex.: estoque insuficiente).
      setErro(err instanceof ApiError ? err.message : 'Erro inesperado ao criar o pedido.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="overlay" onMouseDown={(e) => e.target === e.currentTarget && !enviando && onClose()}>
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby={tituloId}>
        <h2 id={tituloId}>Novo pedido</h2>

        <form onSubmit={salvar}>
          <label>
            Cliente
            <select value={clienteId} onChange={(e) => setClienteId(e.target.value)} required autoFocus>
              <option value="">Selecione…</option>
              {clientes.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.nome} ({c.email})
                </option>
              ))}
            </select>
          </label>

          <fieldset>
            <legend>Itens</legend>
            {produtos.error && <p className="alerta erro">{produtos.error.message}</p>}

            {linhas.map((linha, indice) => {
              const produto = produtoPorId.get(Number(linha.produtoId))
              return (
                <div className="linha-item" key={linha.chave}>
                  <label>
                    Produto {indice + 1}
                    <select
                      value={linha.produtoId}
                      onChange={(e) => atualizar(linha.chave, 'produtoId', e.target.value)}
                      required
                    >
                      <option value="">Selecione…</option>
                      {produtos.data?.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.nome} — {formatarMoeda(p.preco)} (estoque: {p.estoque})
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="quantidade">
                    Quantidade {indice + 1}
                    <input
                      type="number"
                      min={1}
                      step={1}
                      value={linha.quantidade}
                      onChange={(e) => atualizar(linha.chave, 'quantidade', e.target.value)}
                      required
                    />
                  </label>
                  <span className="subtotal" aria-label={`Subtotal ${indice + 1}`}>
                    {produto ? formatarMoeda(produto.preco * (Number(linha.quantidade) || 0)) : '—'}
                  </span>
                  <button
                    type="button"
                    className="link perigo"
                    onClick={() => setLinhas((ls) => ls.filter((l) => l.chave !== linha.chave))}
                    disabled={linhas.length === 1}
                    aria-label={`Remover item ${indice + 1}`}
                  >
                    Remover
                  </button>
                </div>
              )
            })}

            <button type="button" className="secundario" onClick={() => setLinhas((ls) => [...ls, novaLinha()])}>
              + Adicionar item
            </button>
          </fieldset>

          <p className="total">
            Total: <strong data-testid="total-pedido">{formatarMoeda(total)}</strong>
          </p>

          {erro && (
            <p className="alerta erro" role="alert">
              {erro}
            </p>
          )}

          <div className="acoes">
            <button type="button" className="secundario" onClick={onClose} disabled={enviando}>
              Cancelar
            </button>
            <button type="submit" className="primario" disabled={!valido || enviando}>
              {enviando ? 'Salvando…' : 'Criar pedido'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
