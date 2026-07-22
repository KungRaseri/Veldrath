# RealmFoundry MudBlazor Migration — Status & Remaining Work

> **Last updated:** 2026-07-22  
> **Overall progress:** ~60% complete (foundation + layout + auth pages + CSS done; ~26 content/admin pages remain)

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
| Strip app.css | 1077 → 220 lines (80% reduction) | ✅ |
| Deleted CSS | Layout (`.layout`, `.sidebar`, `.content`, `.top-bar`, `.page-body`), Cards (`.card`, `.card-elevated`, `.card-accent`, `.feature-cards`), Buttons (`.btn`, `.btn-primary`, `.btn-secondary`, `.btn-ghost`, `.btn-danger`, `.btn-red`, `.btn-orange`, `.btn-warn`, `.btn-sm`), Badges (`.badge-*`), Forms (`.form-group`, `.field-group`, `.form-actions`, `.form-check`, `.checkbox-label`, `.schema-form`), Tables (`.data-table`, `.content-list-table`), Pagination (`.pagination`), Alerts (`.alert-*`), Toasts (`.toast-*`), Typography utilities, Page header/filters, Auth card, Content browser, Nav elements, Mobile responsive overrides | ✅ |
| Kept CSS | Legacy `--color-*` aliases, VDS text utilities (`.text-lore`, `.text-mono`, `.text-muted`, `.text-caption`, `.text-accent`), OAuth buttons (`.btn-discord`, `.btn-google`, `.btn-microsoft`), Content detail split-pane, Submission form shell, Vote bar, Payload display, Field-list, Detail groups, JSON block | ✅ |

### Cross-Project
| Task | Status |
|---|---|
| Veldrath.GameClient.Components CSS audit | ✅ (see [`plans/css-audit-game-client-components.md`](plans/css-audit-game-client-components.md)) |
| Veldrath.GameClient.Components Razor migration (11 files → MudButton + utilities) | ✅ |
| Veldrath.Web final sweep | ✅ (no issues found) |

---

## ⬜ Remaining Work

### ResetPassword.razor
- **File:** [`RealmFoundry/Components/Pages/ResetPassword.razor`](RealmFoundry/Components/Pages/ResetPassword.razor)
- **`.btn` count:** 1
- **Embedded `<style>`:** Yes — needs deletion
- **Migration:** `<MudContainer>`, `<MudPaper>`, `<MudTextField>`, `<MudButton>`, `<MudAlert>`

### Core Content Pages (6 files, ~15 `.btn` usages)
| Page | File | `.btn` count | Other patterns |
|---|---|---|---|
| Submissions | [`Submissions.razor`](RealmFoundry/Components/Pages/Submissions.razor) | 4 | `.page-header`, `.filters`, `.pagination` → MudBlazor |
| ContentBrowser | [`ContentBrowser.razor`](RealmFoundry/Components/Pages/ContentBrowser.razor) | 0 | `.content-type-grid`, `.content-type-card` → `<MudGrid>` + `<MudPaper>` |
| ContentDetail | [`ContentDetail.razor`](RealmFoundry/Components/Pages/ContentDetail.razor) | 1 | Complex split-pane (CSS kept intentionally) |
| NewSubmission | [`NewSubmission.razor`](RealmFoundry/Components/Pages/NewSubmission.razor) | 2 | `.form-actions` → `<MudButton>` |
| SubmissionDetail | [`SubmissionDetail.razor`](RealmFoundry/Components/Pages/SubmissionDetail.razor) | 7 | `.review-actions`, `.vote-bar` (CSS kept) |
| Notifications | [`Notifications.razor`](RealmFoundry/Components/Pages/Notifications.razor) | 2 | Simple |
| PlayerProfile | [`PlayerProfile.razor`](RealmFoundry/Components/Pages/PlayerProfile.razor) | 0 | Needs read |

### Admin Pages (8 files, ~30 `.btn` usages)
| Page | File | `.btn` count | Notes |
|---|---|---|---|
| AdminDashboard | [`AdminDashboard.razor`](RealmFoundry/Components/Pages/Admin/AdminDashboard.razor) | ~2 | Simple |
| UserManagement | [`UserManagement.razor`](RealmFoundry/Components/Pages/Admin/UserManagement.razor) | 7 | Table + pagination + filters |
| UserDetail | [`UserDetail.razor`](RealmFoundry/Components/Pages/Admin/UserDetail.razor) | 12 | Most complex admin page |
| RoleManagement | [`RoleManagement.razor`](RealmFoundry/Components/Pages/Admin/RoleManagement.razor) | ~2 | Needs read |
| ActiveSessions | [`ActiveSessions.razor`](RealmFoundry/Components/Pages/Admin/ActiveSessions.razor) | 4 | Table + filters |
| AuditLog | [`AuditLog.razor`](RealmFoundry/Components/Pages/Admin/AuditLog.razor) | 4 | Table + pagination |
| PlayerReports | [`PlayerReports.razor`](RealmFoundry/Components/Pages/Admin/PlayerReports.razor) | 4 | Table + pagination |
| ServerStatus | [`ServerStatus.razor`](RealmFoundry/Components/Pages/Admin/ServerStatus.razor) | 1 | Simple |
| ServerCommands | [`ServerCommands.razor`](RealmFoundry/Components/Pages/Admin/ServerCommands.razor) | 1 | Simple |

### Mod Pages (4 files, ~12 `.btn` usages)
| Page | File | `.btn` count | Notes |
|---|---|---|---|
| ModDashboard | [`ModDashboard.razor`](RealmFoundry/Components/Pages/Mod/ModDashboard.razor) | ~2 | Needs read |
| ModReports | [`ModReports.razor`](RealmFoundry/Components/Pages/Mod/ModReports.razor) | 4 | Table + pagination |
| ModUserDetail | [`ModUserDetail.razor`](RealmFoundry/Components/Pages/Mod/ModUserDetail.razor) | 10 | Complex mod actions |
| ModUserSearch | [`ModUserSearch.razor`](RealmFoundry/Components/Pages/Mod/ModUserSearch.razor) | 4 | Table + pagination |

### Editorial Pages (6 files, ~18 `.btn` usages)
| Page | File | `.btn` count | Notes |
|---|---|---|---|
| Announcements | [`Announcements.razor`](RealmFoundry/Components/Pages/Editorial/Announcements.razor) | 5 | Table + pagination |
| AnnouncementDetail | [`AnnouncementDetail.razor`](RealmFoundry/Components/Pages/Editorial/AnnouncementDetail.razor) | 3 | Form + publish toggle |
| LoreArticles | [`LoreArticles.razor`](RealmFoundry/Components/Pages/Editorial/LoreArticles.razor) | 5 | Table + pagination |
| LoreArticleDetail | [`LoreArticleDetail.razor`](RealmFoundry/Components/Pages/Editorial/LoreArticleDetail.razor) | 3 | Form + publish toggle |
| PatchNotes | [`PatchNotes.razor`](RealmFoundry/Components/Pages/Editorial/PatchNotes.razor) | 5 | Table + pagination |
| PatchNoteDetail | [`PatchNoteDetail.razor`](RealmFoundry/Components/Pages/Editorial/PatchNoteDetail.razor) | 3 | Form + publish toggle |

### Language Pages (2 files, ~25 `.btn` usages)
| Page | File | `.btn` count | Notes |
|---|---|---|---|
| LanguageBrowser | [`LanguageBrowser.razor`](RealmFoundry/Components/Pages/LanguageBrowser.razor) | 1 | Simple |
| LanguageBuilder | [`LanguageBuilder.razor`](RealmFoundry/Components/Pages/LanguageBuilder.razor) | 25 | **Most complex page** — multi-step wizard with add/remove buttons per section |

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
