import { defineConfig, devices } from '@playwright/test'

/**
 * Roda contra a aplicação já no ar (docker compose up). Não sobe servidor próprio:
 * o objetivo é testar o mesmo artefato que vai para produção (nginx + API + SQL Server).
 */
export default defineConfig({
  testDir: './tests',
  fullyParallel: false, // os testes alteram estoque no mesmo banco
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:3000',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
