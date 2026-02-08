import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import basicSsl from "@vitejs/plugin-basic-ssl";

// Use HTTPS only when VITE_HTTPS=true (for remote access where WebGL requires secure context)
// Usage: VITE_HTTPS=true npm run dev
const useHttps = process.env.VITE_HTTPS === "true";

export default defineConfig({
  plugins: [react(), ...(useHttps ? [basicSsl()] : [])],
  base: "./",
  server: {
    port: 5203,
    host: true,
    allowedHosts: true,
    proxy: {
      "/api": {
        target: "http://localhost:5080",
        changeOrigin: true,
      },
      "/hubs": {
        target: "http://localhost:5080",
        changeOrigin: true,
        ws: true,
      },
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
});
