const moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const dataHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export const formatarMoeda = (valor: number) => moeda.format(valor)

/** A API devolve UTC; exibimos no fuso do navegador. */
export const formatarDataHora = (iso: string) => dataHora.format(new Date(iso))

/** yyyy-MM-dd da data local (para inputs type="date"). */
export function hojeIso(deslocamentoDias = 0): string {
  const d = new Date()
  d.setDate(d.getDate() + deslocamentoDias)
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${mm}-${dd}`
}
