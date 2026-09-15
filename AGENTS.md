# Coding Style

- **General:** Follow the rules defined in .editorconfig
- **Instance field:** Do not use `_` prefix for member variables
- **Warnings:** Ensure there are no build warnings
- **Suppress warnings:** If warning suppression is needed, ask before applying the fix
- **Line endings:** Never change existing line endings, use CRLF for newly created text files

# Project Rules

- **Structure:** Monorepo. `server/Pos.Server.slnx` (ASP.NET Core) and `terminal/Pos.Terminal.slnx` (MAUI) are opened separately, both include the `shared/` projects. See `docs/architecture.md`
- **Layers:** SQL lives only in Accessors. Server: Endpoints and Blazor pages call `Services/` (`XxxService`), which use Accessors and `Pos.Domain`; Razor display formatting lives only in `ViewHelper` / `ViewExtensions`; `TimeProvider` is used only by Services and report builders (pages and endpoints never read the clock: today / default period come from `ReportService`, online state from `TerminalService.IsOnline`, timestamps are stamped by Services). Terminal: ViewModels call `Services/` (`XxxService`, a single function) and `Usecases/` (`XxxUsecase`, a sequence); `XxxBuilder` only builds text or images (data conversion is `XxxMapper` / `XxxCalculator`); no `IDbProvider` in ViewModels; state shared between the screens of one feature is a `[Scope]` property (Smart.Navigation Scope plugin), not a navigation parameter
- **Naming:** Communication data is `XxxRequest` / `XxxResponse` named after the endpoint class and method (`TransactionCreateRequest`, `ReportSalesSummaryResponse`); a list is `XxxResponse` whose `Items` are `XxxResponseItem` (nested elements append the element name: `TransactionResponseItemLine`). Server read models are `XxxView`, sort orders are enums in `Models/Enums`. Accessor methods are named after the database operation (`Query` / `Count` / `Insert` / `Update` / `Delete`, e.g. `UpdateVoidedAsync`, `UpdateClosedAsync`); business verbs such as Void / Close / Import belong to Services and Usecases only. Never use the word "DTO" in code, namespaces or documents
- **Source comments:** Source is the source of truth: do not reference design documents (section numbers, `§`, screen IDs, decision numbers) from source code
- **JSON:** camelCase, `null` properties omitted, UTC datetime as `yyyy-MM-ddTHH:mm:ss.fffZ`
- **Database:** SQLite with `Usa.Smart.Data.Accessor` (2-way SQL files), no ORM. Design in `docs/db-design.md`
- **SQL format:** `SELECT` / `FROM` / `WHERE` / `ORDER BY` / `UPDATE` / `SET` each on its own line, table names, columns and conditions indented on the following lines (`AND` at the start of the line). Sort columns come from an enum expanded inside the 2-way SQL (`/*# sort */`), never from a caller-built string
- **Lengths:** String lengths (contract `MaxLength`, form validators, terminal digit counts) are the constants in `Pos.Domain.Length`
- **No null guards:** Do not write `ArgumentNullException.ThrowIfNull`
- **Terminal input:** No physical keyboard; numbers are entered with the calculator popups in `PopupNavigatorExtensions` (one method per kind of input), reasons are chosen from presets; the software keyboard is only for text fields (customer, delivery, search) and settings. Popups are sheets anchored to the bottom of the screen (CommunityToolkit Popup with `VerticalOptions=End`, stackable); list selection uses the `Select` sheet (`IPopupNavigator.ChooseAsync`), never `IDialog.SelectAsync`
- **UI language:** Japanese only, no localization resources
- **Design docs:** Record decisions in `docs/decisions.md` and update the affected design document before closing a phase (`docs/implementation-plan.md`)
- **Guidelines:** `docs/guidelines.md` holds the rules learned from reviews (how things should be, no history). When a review comment comes in, add or update the rule there, then fix the affected design document and this file

# Documents

- **Line breaks:** In Markdown, end each sentence at `。` with two spaces so that it renders as a line break (not inside tables, headings or code)
- **Background:** Keep background and decision history only in `docs/decisions.md`, without dates. Design documents describe the current state only; deferred items go to `docs/implementation-plan.md`
- **README:** The root `README.md` has only the main screens and links to the documents (no screen IDs, no setup instructions)
- **References:** Do not link to external reference materials
