# ResolveHub Frontend

## Data modes

ResolveHub keeps page components independent from their temporary data source.
Administrator pages import data through `src/data/index.js`, so changing the
source does not require redesigning the UI.

### Development / Demo mode

`npm run dev` uses `VITE_DATA_MODE=demo` from `.env.development`. It loads the
realistic company dataset in `src/data/demo/` for presentations, portfolio
demos, UI testing, and feature development.

Use `npm run build:demo` when a deployable demo build is needed.

### Production mode

`npm run build` uses `VITE_DATA_MODE=production` from `.env.production`.
Production data collections start empty and contain no fictional users,
tickets, activity, notifications, assignments, or statistics. System ticket
lookups remain available while the corresponding API repositories are being
connected.

Future production integration will replace the exports behind
`src/data/index.js` with calls such as `GET /api/users` and `GET /api/tickets`.
The dashboard, tables, filters, and other UI components do not need to be
redesigned; only the data source changes.

---

# React + Vite

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the Oxlint configuration

If you are developing a production application, we recommend using TypeScript with type-aware lint rules enabled. Check out the [TS template](https://github.com/vitejs/vite/tree/main/packages/create-vite/template-react-ts) for information on how to integrate TypeScript and Oxlint's TypeScript related rules in your project.
