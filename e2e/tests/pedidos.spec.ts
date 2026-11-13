import { expect, test, type Page } from '@playwright/test'

// Ids vêm da carga database/03-seed.sql.
const CLIENTE_CARLA = '3'
const CLIENTE_DIEGO = '4'
const PRODUTO_MOUSE = '4' // R$ 129,90, estoque alto
const PRODUTO_HUB = '8' // R$ 239,00, estoque alto
const PRODUTO_MESA = '12' // estoque baixo (3)

async function abrirNovoPedido(page: Page) {
  await page.getByRole('button', { name: /novo pedido/i }).click()
  const dialog = page.getByRole('dialog', { name: 'Novo pedido' })
  await expect(dialog).toBeVisible()
  return dialog
}

test.beforeEach(async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Pedidos', exact: true })).toBeVisible()
})

test('cria um pedido com dois itens e o vê na listagem', async ({ page }) => {
  const dialog = await abrirNovoPedido(page)

  await dialog.getByLabel('Cliente').selectOption(CLIENTE_CARLA)
  await dialog.getByLabel('Produto 1').selectOption(PRODUTO_MOUSE)
  await dialog.getByLabel('Quantidade 1').fill('2')
  await dialog.getByRole('button', { name: /adicionar item/i }).click()
  await dialog.getByLabel('Produto 2').selectOption(PRODUTO_HUB)
  await dialog.getByLabel('Quantidade 2').fill('1')

  // 2 × 129,90 + 239,00 = 498,80
  await expect(dialog.getByTestId('total-pedido')).toHaveText(/498,80/)
  await dialog.getByRole('button', { name: 'Criar pedido' }).click()

  await expect(dialog).toBeHidden()
  const sucesso = page.getByRole('status')
  await expect(sucesso).toContainText(/Pedido #\d+ criado com sucesso/)
  const id = (await sucesso.textContent())!.match(/#(\d+)/)![1]

  const linha = page.getByTestId(`pedido-${id}`)
  await expect(linha).toBeVisible()
  await expect(linha).toContainText('Carla Mendes')
  await expect(linha).toContainText('498,80')
  // Não verificamos "é a primeira linha": com workers em paralelo outro pedido pode ser mais recente.
  // A ordenação (mais recente primeiro) é coberta pelos testes de integração (CA09).

  await linha.getByRole('button', { name: 'Ver itens' }).click()
  await expect(page.getByText(/2 × Mouse sem fio/)).toBeVisible()
  await expect(page.getByText(/1 × Hub USB-C/)).toBeVisible()
})

test('mostra erro de estoque insuficiente sem fechar o modal nem perder os dados', async ({ page }) => {
  const dialog = await abrirNovoPedido(page)

  await dialog.getByLabel('Cliente').selectOption(CLIENTE_CARLA)
  await dialog.getByLabel('Produto 1').selectOption(PRODUTO_MESA)
  await dialog.getByLabel('Quantidade 1').fill('999')
  await dialog.getByRole('button', { name: 'Criar pedido' }).click()

  await expect(dialog.getByRole('alert')).toContainText('Estoque insuficiente')
  await expect(dialog).toBeVisible()
  await expect(dialog.getByLabel('Quantidade 1')).toHaveValue('999')
})

test('filtra a listagem por cliente', async ({ page }) => {
  await page.getByRole('form', { name: 'Filtros' }).getByLabel('Cliente').selectOption(CLIENTE_DIEGO)
  await page.getByRole('button', { name: 'Filtrar' }).click()

  const tabela = page.getByRole('table', { name: 'Lista de pedidos' })
  await expect(tabela).toHaveAttribute('aria-busy', 'false')

  // Asserções com retry automático: nada de "fotografar" as linhas antes da resposta filtrada chegar.
  const linhas = tabela.locator('tbody tr[data-testid]')
  await expect(linhas).not.toHaveCount(0)
  await expect(linhas.filter({ hasNotText: 'Diego Ferreira' })).toHaveCount(0)
})
