# Coding Style

- **General:** Follow the rules defined in .editorconfig
- **Instance field:** Do not use `_` prefix for member variables
- **Warnings:** Ensure there are no build warnings
- **Suppress warnings:** If warning suppression is needed, ask before applying the fix
- **Line endings:** Never change existing line endings, use CRLF for newly created text files

# Project Rules

- **Structure:** Monorepo. `server/Pos.Server.slnx` (ASP.NET Core) and `terminal/Pos.Terminal.slnx` (MAUI) are opened separately, both include the `shared/` projects. See `docs/architecture.md`
- **Layers:** SQL lives only in Accessors. Server: Endpoints and Blazor pages call `Services/` (`XxxService`), which use Accessors and `Pos.Domain`; Razor display formatting lives only in `ViewHelper` / `ViewExtensions`. Terminal: ViewModels call `Services/` (`XxxService` for a single function, `XxxUsecase` for a sequence, `XxxBuilder` for building); no `IDbProvider` in ViewModels
- **Naming:** Communication data is `XxxRequest` / `XxxResponse`; a list is `XxxResponse` whose `Items` are `XxxResponseItem` (nested elements append the element name: `TransactionResponseItemLine`). Never use the word "DTO" in code, namespaces or documents
- **Source comments:** Source is the source of truth: do not reference design documents (section numbers, `§`, screen IDs, decision numbers) from source code
- **JSON:** camelCase, `null` properties omitted, UTC datetime as `yyyy-MM-ddTHH:mm:ss.fffZ`
- **Database:** SQLite with `Usa.Smart.Data.Accessor` (2-way SQL files), no ORM. Design in `docs/db-design.md`
- **SQL format:** `SELECT` / `FROM` / `WHERE` / `ORDER BY` each on its own line, columns and conditions indented on the following lines
- **UI language:** Japanese only, no localization resources
- **Design docs:** Record decisions in `docs/decisions.md` and update the affected design document before closing a phase (`docs/implementation-plan.md`)

# Documents

- **Line breaks:** In Markdown, end each sentence at `。` with two spaces so that it renders as a line break (not inside tables, headings or code)
- **Background:** Keep background and decision history only in `docs/decisions.md`, without dates. Design documents describe the current state only; deferred items go to `docs/implementation-plan.md`
- **README:** The root `README.md` has only the main screens and links to the documents (no screen IDs, no setup instructions)
- **References:** Do not link to external reference materials
