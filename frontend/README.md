# Growia — Cleaned Frontend

Pure static HTML/CSS/JS project. No frameworks, no build tools.
Open `index.html` in any browser to start.

## Project Structure

```
growia-clean/
├── index.html                  Landing page (role selector)
├── hero-hub.html               Child hero picker
├── child_login.html            Child username/password login
├── child-signup.html           Multi-step child registration
├── child-new-password.html     Child password reset (with parent code)
├── reset-password.html         Email-based password reset (parent or child)
├── parent-login.html           Parent login + signup
├── parent-dashboard.html       Parent home — alerts & hero cards
├── parent-child-insights.html  Weekly analytics per child
├── parent-child-history.html   Day-by-day activity archive
├── parent-edit-profile.html    Manage chores / subjects / sports
├── admin-login.html            Admin portal login
├── admin-dashboard.html        Full content engine (6 sections)
├── styles.css                  Shared design system (tokens, components)
└── script.js                   Shared utilities (auth, validation, modals)
```

## API Base URL
All endpoints point to: `https://localhost:7283/api` (matches `script.js` and `launchSettings.json`)

| Page | Endpoint(s) |
|---|---|
| admin-login | `/Auth/admin-login` |
| admin-dashboard | `/admin/...` |
| parent-login | `/Auth/login/parent`, `/Auth/register/gradient` |
| hero-hub | `/Auth/device-children`, `/Auth/login-child` |
| child-signup | `/Auth/signup-child` |
| child_login | `/Auth/login-child`, `/Auth/forgot-password` |
| child-new-password | `/Auth/reset-child-password-final` |
| reset-password | `/Auth/reset-password` |
| parent-dashboard | `/Parent/main-dashboard` |
| parent-child-insights | `/Parent/hero-insights/:id`, `/Parent/allow-child-reset`, `/Parent/child-last-message/:id` |
| parent-child-history | `/Parent/hero-history/:id` |
| parent-edit-profile | `/Parent/hero-management/:id` (GET + PATCH) |
| badges | `/Children/profile`, `/Children/journey-progress`, `/Children/pillar-badges`, `/Children/my-badges`, `/Children/stats` |

## Phase 10 — Badge + Level Integration

### New file: `badges.html`
Dedicated Badges & Level page with full backend integration:
- **Level Summary card** — badge count (`/Children/my-badges`), trust score (`/Children/profile`), day streak (`/Children/stats`)
- **Level Journey card** — DB-driven level milestones + active challenges + EXP bar (`/Children/journey-progress`)
- **Pillar Badges card** — 8 pillar badges with tier rings, progress bars, tooltips (`/Children/pillar-badges`)
- **All Unlocked Badges card** — every challenge-based badge earned, sorted newest first (`/Children/my-badges`)
- **Journal Scoring card** — how each trait maps to journal sections (`/Children/profile` traits)

### Updated: `heroprof.html`
- Trust score now colour-coded (green ≥70, amber ≥40, red &lt;40) and visible in Journey card
- "See all badges →" button links to `badges.html`
- Level milestones use DB `levelDefinitions` names/emojis

### Updated: `leafy.html`
- "More" dropdown now includes "🏅 My Badges & Level" → `badges.html`
- Level title pill shown next to MY BADGES header (dynamic from API)
- Journey zigzag uses DB-driven `levelDefinitions` instead of hardcoded array
- "See all badges →" deep-link added

### API endpoints used for badge/level system
| Endpoint | What it provides |
|---|---|
| `GET /Children/profile` | `level`, `trustScore`, `characterType`, all trait/pillar pct values |
| `GET /Children/journey-progress` | `currentLevel`, `currentLevelName`, `progressPercentage`, `levelDefinitions[]`, `nextLevelRequirements[]`, `totalLogs`, `currentStreak` |
| `GET /Children/pillar-badges` | `pillars[]` with `tier`, `pct`, `nextTarget`, `color`, `icon`, `label`, `suggestion`; `suggestedMessage` |
| `GET /Children/my-badges` | Flat array of `{ name, description, iconUrl, unlockedAt }` |
| `GET /Children/stats` | `currentStreak`, `totalLogs` |


## Changes Made
- Removed all Next.js/React files (`app/`, `components/`, `node_modules/`, `package.json`, etc.)
- Extracted shared tokens and components into `styles.css`
- Moved all shared utilities into `script.js`
- Cleaned and standardized all HTML (semantic tags, proper aria, consistent indentation)
- Moved all inline `<style>` blocks → page-scoped `<style>` at top of each file
- All API endpoints, form `name`/`id` attributes, and `fetch()` bodies preserved exactly
- All pages link via relative paths — works with `file://` or any static server
