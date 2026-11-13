import { useEffect, useState } from 'react'

interface Estado<T> {
  /** Último dado carregado (mantido durante um novo carregamento para a tela não "piscar"). */
  data?: T
  error?: Error
  loading: boolean
}

interface Resposta<T> {
  chave: string
  data?: T
  error?: Error
}

/**
 * Executa `fn` sempre que `deps` mudar (comparação por valor), cancelando a chamada anterior
 * para que uma resposta antiga não sobrescreva a mais nova ao trocar filtros rapidamente.
 *
 * `loading` é derivado (a resposta guardada é de outra chave?) em vez de ser setado dentro do
 * effect, o que evita um render extra a cada mudança.
 */
export function useAsync<T>(fn: (signal: AbortSignal) => Promise<T>, deps: readonly unknown[]): Estado<T> {
  const chave = JSON.stringify(deps)
  const [resposta, setResposta] = useState<Resposta<T> | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    fn(controller.signal)
      .then((data) => setResposta({ chave, data }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        setResposta({ chave, error: error instanceof Error ? error : new Error(String(error)) })
      })
    return () => controller.abort()
    // `fn` é recriada a cada render; quem decide quando recarregar é `chave` (derivada de deps).
    // oxlint-disable-next-line react-hooks/exhaustive-deps
  }, [chave])

  const loading = resposta?.chave !== chave
  return { data: resposta?.data, error: loading ? undefined : resposta?.error, loading }
}
