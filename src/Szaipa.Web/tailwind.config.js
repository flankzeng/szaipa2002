/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Areas/**/*.cshtml",
    "./wwwroot/admin/**/*.js"
  ],
  theme: {
    extend: {
      colors: {
        // Visual tokens lifted from the public NewIndex page so the backend matches the site's aesthetic.
        brand: { DEFAULT: "#bf272d", dark: "#a01f24" },
        ink: "#1f1f1f",
        muted: "#939393",
        canvas: "#f7f7f7"
      },
      fontFamily: {
        sans: ["'Noto Sans SC'", "system-ui", "-apple-system", "Segoe UI", "sans-serif"],
        serif: ["'Noto Serif SC'", "Georgia", "serif"]
      }
    }
  },
  plugins: []
};
