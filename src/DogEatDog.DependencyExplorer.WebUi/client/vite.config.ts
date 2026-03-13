import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// https://vite.dev/config/
export default defineConfig({
  base: "/_content/DogEatDog.DependencyExplorer.WebUi/react/",
  build: {
    outDir: "../wwwroot/react",
    emptyOutDir: true,
  },
  plugins: [react()],
});
