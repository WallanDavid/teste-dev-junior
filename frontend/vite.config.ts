import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Em dev, /api é repassado para a API .NET (mesmo contrato do nginx no Docker),
// então o frontend nunca precisa de CORS nem de URL absoluta.
const apiUrl = process.env.API_URL ?? 'http://localhost:8080'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: apiUrl,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
