# RealmFoundry MudBlazor Migration — Status & Remaining Work

> **Last updated:** 2026-07-22
> **Overall progress:** ~95% complete (all ~30 pages migrated; only CSS final cleanup remaining)

---

## ✅ Completed Work

### Phase 1 — Foundation
| Task | File | Status |
|---|---|---|
| Add MudBlazor package | [`RealmFoundry.csproj`](RealmFoundry/RealmFoundry.csproj) | ✅ |
| Add RCL project reference | [`RealmFoundry.csproj`](RealmFoundry/RealmFoundry.csproj) | ✅ |
| Add `@using MudBlazor` | [`_Imports.razor`](RealmFoundry/_Imports.razor) | ✅ |
| Add `@using Veldrath.GameClient.Components.Themes` | [`_Imports.razor`](RealmFoundry/_Imports.razor) | ✅ |
| Move VeldrathTheme to shared RCL | [`VeldrathTheme.cs`](Veldrath.GameClient.Components/Themes/VeldrathTheme.cs) | ✅ |
| Update Veldrath.Web theme import | [`Veldrath.Web/App.razor`](Veldrath.Web/App.razor) | ✅ |
| Add MudBlazor CSS + JS | [`RealmFoundry/App.razor`](RealmFoundry/App.razor) | ✅ |
| Add MudThemeProvider + DialogProvider + SnackbarProvider | [`RealmFoundry/App.razor`](RealmFoundry/App.razor) | ✅ |
| Standardize reconnect modal | [`RealmFoundry/App.razor`](RealmFoundry/App.razor) | ✅ |
| Delete duplicate VDS `:root` block | [`app.css`](RealmFoundry/wwwroot/app.css) | ✅ |
| Fix `#blazor-error-ui` → `--mud-palette-error` | [`app.css`](RealmFoundry/wwwroot/app.css) | ✅ |

### Phase 2 — Core Layout
| Task | File | Status |
|---|---|---|
| Migrate to `<MudLayout>` + `<MudAppBar>` + `<MudDrawer>` + `<MudMainContent>` | [`MainLayout.razor`](RealmFoundry/Components/Layout/MainLayout.razor) | ✅ |
| Migrate to `<MudNavMenu>` + `<MudNavLink>` + `<MudChip>` + `<MudButton>` | [`NavMenu.razor`](RealmFoundry/Components/Layout/NavMenu.razor) | ✅ |

### Phase 3 — Auth Pages
| Page | File | `.btn` replaced | `<style>` deleted | Status |
|---|---|---|---|---|
| Login | [`Login.razor`](RealmFoundry/Components/Pages/Login.razor) | ✅ (4 buttons) | ✅ | ✅ |
| Register | [`Register.razor`](RealmFoundry/Components/Pages/Register.razor) | ✅ (4 buttons) | ✅ | ✅ |
| Confirm Email | [`ConfirmEmail.razor`](RealmFoundry/Components/Pages/ConfirmEmail.razor) | ✅ (1 button) | ✅ | ✅ |
| Forgot Password | [`ForgotPassword.razor`](RealmFoundry/Components/Pages/ForgotPassword.razor) | ✅ (1 button) | ✅ | ✅ |
| Reset Password | [`ResetPassword.razor`](RealmFoundry/Components/Pages/ResetPassword.razor) | ⬜ | ⬜ | ⬜ |
| Error | [`Error.razor`](RealmFoundry/Components/Pages/Error.razor) | N/A (no `.btn`) | N/A | ✅ |

### Phase 4 — Core Content Pages
| Page | File | `.btn` replaced | Other migrations | Status |
|---|---|---|---|---|
| Home | [`Home.razor`](RealmFoundry/Components/Pages/Home.razor) | ✅ (3 buttons) | `.card` → `<MudPaper>`, `.feature-cards` → `<MudGrid>` | ✅ |

### Phase 8 — CSS Cleanup
| Task | Details | Status |
|---|---|---|
| Initial CSS strip | 1077 → 220 lines (80% reduction) | ✅ |
| Batch 7 final cleanup | 220 → 179 lines (19% further reduction; 83% total from original) | ✅ |
| Deleted CSS (initial) | Layout (`.layout`, `.sidebar`, `.content`, `.top-bar`, `.page-body`), Cards (`.card`, `.card-elevated`, `.card-accent`, `.feature-cards`), Buttons (`.btn`, `.btn-primary`, `.btn-secondary`, `.btn-ghost`, `.btn-danger`, `.btn-red`, `.btn-orange`, `.btn-warn`, `.btn-sm`), Badges (`.badge-*`), Forms (`.form-group`, `.field-group`, `.form-actions`, `.form-check`, `.checkbox-label`, `.schema-form`), Tables (`.data-table`, `.content-list-table`), Pagination (`.pagination`), Alerts (`.alert-*`), Toasts (`.toast-*`), Typography utilities, Page header/filters, Auth card, Content browser, Nav elements, Mobile responsive overrides | ✅ |
| Deleted CSS (Batch 7) | VDS text utilities (`.text-lore`, `.text-mono`, `.text-muted`, `.text-caption`, `.text-accent`), Submission form shell (`.submission-form-shell`), `.required`, Vote score (`.vote-score`, `.vote-score.positive`, `.vote-score.negative`), Legacy `--color-accent` variable — all confirmed 0 references across all ~30 `.razor` files | ✅ |
| Kept CSS (final) | Legacy `--color-*` aliases (used by remaining CSS rules), OAuth buttons (`.btn-discord`, `.btn-google`, `.btn-microsoft` — preserved per policy), Content detail split-pane (all classes), `.detail-group`, `.field-list`, `.bool-true`, `.bool-false`, `.json-block`, `.vote-bar`, `.payload`, Blazor error UI, Universal reset, `body`/`a` base styles | ✅ |

### Cross-Project
| Task | Status |
|---|---|
| Veldrath.GameClient.Components CSS audit | ✅ (see [`plans/css-audit-game-client-components.md`](plans/css-audit-game-client-components.md)) |
| Veldrath.GameClient.Components Razor migration (11 files → MudButton + utilities) | ✅ |
| Veldrath.Web final sweep | ✅ (no issues found) |

## ✅ Migration Batches Summary

All ~30 RealmFoundry pages have been fully migrated from Bootstrap-style CSS to MudBlazor across 7 batches:

| Batch | Pages | Description |
|---|---|---|
| 1 | Login, Register, ConfirmEmail, ForgotPassword, Error | Auth pages |
| 2 | Home | Core content landing page |
| 3 | ResetPassword | Auth completion |
| 4 | Submissions, ContentBrowser, ContentDetail, NewSubmission, SubmissionDetail, Notifications, PlayerProfile | Core content pages (7) |
| 5 | AdminDashboard, UserManagement, UserDetail, RoleManagement, ActiveSessions, AuditLog, PlayerReports, ServerStatus, ServerCommands | Admin pages (9) |
| 6 | ModDashboard, ModReports, ModUserDetail, ModUserSearch, Announcements, AnnouncementDetail, LoreArticles, LoreArticleDetail, PatchNotes, PatchNoteDetail | Mod + Editorial pages (10) |
| 7 | LanguageBrowser, LanguageBuilder | Language pages (2) |

**Post-migration quality sweep:** All `.btn` classes, `<style>` blocks, and Bootstrap-era patterns replaced. Zero Bootstrap CSS dependencies remain.

## ⬜ Remaining Work

### Minimal
- **OAuth button classes** (`.btn-discord`, `.btn-google`, `.btn-microsoft`) — preserved in `app.css` for future re-authentication flows. Currently 0 references in Razor markup; may be removed in a future cleanup once auth flows are finalized.
- **Periodic CSS re-audit** — as MudBlazor component usage evolves, remaining custom CSS in `app.css` (179 lines) should be re-evaluated for further reduction opportunities.

---

## `.btn` → `<MudButton>` Migration Mapping

| CSS Class | MudBlazor Equivalent |
|---|---|
| `class="btn"` | `<MudButton Variant="Variant.Filled">` |
| `class="btn btn-primary"` | `<MudButton Variant="Variant.Filled" Color="Color.Primary">` |
| `class="btn btn-secondary"` | `<MudButton Variant="Variant.Outlined">` |
| `class="btn btn-sm"` | Add `Size="Size.Small"` |
| `class="btn btn-sm btn-primary"` | `<MudButton Variant="Variant.Filled" Color="Color.Primary" Size="Size.Small">` |
| `class="btn btn-sm btn-secondary"` | `<MudButton Variant="Variant.Outlined" Size="Size.Small">` |
| `class="btn btn-sm btn-warn"` / `btn-orange` | `<MudButton Variant="Variant.Filled" Color="Color.Warning" Size="Size.Small">` |
| `class="btn btn-sm btn-danger"` / `btn-red` | `<MudButton Variant="Variant.Filled" Color="Color.Error" Size="Size.Small">` |
| `class="btn btn-sm btn-green"` | `<MudButton Variant="Variant.Filled" Color="Color.Success" Size="Size.Small">` |
| `class="btn btn-danger"` | `<MudButton Variant="Variant.Filled" Color="Color.Error">` |
| `class="btn btn-warning"` | `<MudButton Variant="Variant.Filled" Color="Color.Warning">` |
| `class="btn btn-green"` | `<MudButton Variant="Variant.Filled" Color="Color.Success">` |
| `class="btn btn-red"` | `<MudButton Variant="Variant.Filled" Color="Color.Error">` |

**Attribute mapping:**
- `disabled="@expr"` → `Disabled="@expr"`
- `href="url"` → `Href="url"`
- `@onclick="handler"` → `OnClick="handler"` (or keep `@onclick` — both work)
- `type="submit"` → `ButtonType="ButtonType.Submit"`

**Important:** `<a class="btn...">` links must become `<MudButton Href="...">` (not `<a>`). `<button>` elements become `<MudButton>`. Closing tags change from `</button>` / `</a>` to `</MudButton>`.

---

## Other Patterns to Migrate (secondary priority)

| Pattern | MudBlazor Replacement |
|---|---|
| `.alert-*` | `<MudAlert Severity="...">` |
| `.badge-*` | `<MudChip T="string" Size="Size.Small" Color="...">` |
| `.data-table` | `<MudTable>` |
| `.pagination` | `<MudPagination>` or manual `<MudButton>` rows |
| `.form-group` + `.field-input` + `.field-label` | `<MudTextField>`, `<MudSelect>`, `<MudForm>` |
| `.page-header` | MudBlazor flex utilities (`mud-d-flex align-items-center gap-3`) |
| `.filters` | MudBlazor flex utilities + `<MudSelect>` / `<MudTextField>` |
| `<style>` blocks | Delete entirely (MudBlazor handles styling) |

---

## Build Status
| Project | Status |
|---|---|
| Veldrath.GameClient.Components | ✅ 0 errors, 0 warnings |
| Veldrath.Web | ✅ 0 errors, 0 warnings |
| RealmFoundry | ✅ 0 errors, 0 warnings |

---

## Related Documents
- [`plans/css-audit-game-client-components.md`](plans/css-audit-game-client-components.md) — Veldrath.GameClient.Components CSS + Razor audit
- [`plans/realmfoundry-web-audit.md`](plans/realmfoundry-web-audit.md) — Original RealmFoundry audit with line-by-line CSS inventory
