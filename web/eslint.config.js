// @ts-check
import tseslint from "typescript-eslint";

export default tseslint.config(
  {
    ignores: ["node_modules/**", "dist/**", "coverage/**", "archive/**", "assets/**", ".svelte-kit/**"],
  },
  ...tseslint.configs.recommended,
);
