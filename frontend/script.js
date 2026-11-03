/* ============================================================
   GROWIA — Shared JavaScript Utilities  (script.js)
   Loaded by all pages: <script src="script.js"></script>

   FIX LOG (Group 2 Audit):
   • API_BASE_URL points to HTTPS port 7283 (matches launchSettings.json)
   • getOrCreateDeviceId() persists a stable device fingerprint
   • authFetch() wrapper attaches Bearer + X-Device-Fingerprint headers
     for the correct role — ALL Group 2 pages must use this instead of
     bare fetch() calls.
   • clearChildToken() is the canonical 401 handler for child pages —
     localStorage.removeItem('token') has been removed from all helpers.
   • logout() clears parentToken, childToken, adminToken, and legacy keys.
   • openModal() / closeModal() drive display flex/none (not .active class)
     so modals with inline style="display:none" open correctly.
   • Added childAuthFetch() convenience alias for child-only pages.
   ============================================================ */

'use strict';

/* ── API Base URL ──────────────────────────────────────────── */
// FIX: API_BASE_URL — change this to match your backend's running address.
// If running locally with Visual Studio / dotnet run, check launchSettings.json.
// Default: https://localhost:7283 (HTTPS dev cert required).
// If you get SSL errors, change to http://localhost:5283 for the HTTP profile.
const API_BASE_URL = 'https://localhost:7283/api';

/* ── Device Fingerprint ─────────────────────────────────────── */
function getOrCreateDeviceId() {
  let id = localStorage.getItem('grawiaa_device_id');
  if (!id) {
    id = 'dev-' + Math.random().toString(36).substring(2, 15) +
                  Math.random().toString(36).substring(2, 15);
    localStorage.setItem('grawiaa_device_id', id);
  }
  return id;
}

/* ── Authenticated fetch helper ─────────────────────────────── */
// Usage:
//   authFetch('/Children/profile', 'child')           — GET
//   authFetch('/Children/quick-check-in', 'child', { method: 'PATCH', body: JSON.stringify(dto) })
//
// Automatically attaches:
//   Authorization: Bearer <role-specific token>
//   X-Device-Fingerprint: <stable device id>
//
// Returns the raw Response so callers can check .ok / .status themselves.
async function authFetch(path, role = 'parent', options = {}) {
  const token = role === 'child'
    ? getChildToken()
    : role === 'admin'
      ? getAdminToken()
      : getParentToken();

  const headers = Object.assign({
    'Content-Type': 'application/json',
    'X-Device-Fingerprint': getOrCreateDeviceId()
  }, options.headers || {});

  if (token) headers['Authorization'] = `Bearer ${token}`;

  return fetch(`${API_BASE_URL}${path}`, Object.assign({}, options, { headers }));
}

/* ── Convenience alias for child-facing pages ───────────────── */
// Usage: childAuthFetch('/Children/stats')
// Identical to authFetch(path, 'child', options) but shorter.
// On 401/403 it automatically clears the child token and redirects.
async function childAuthFetch(path, options = {}) {
  const res = await authFetch(path, 'child', options);
  if (res.status === 401 || res.status === 403) {
    clearChildToken();
    window.location.href = 'child_login.html';
    return null;          // caller must guard: if (!res) return;
  }
  return res;
}

/* ── Validation helpers ─────────────────────────────────────── */
function isValidEmail(email) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
}

function isValidPassword(pass) {
  return pass.length >= 6;
}

/* ── Form helpers ───────────────────────────────────────────── */
function showFieldError(fieldId, message) {
  const field = document.getElementById(fieldId);
  const err   = document.getElementById('error-' + fieldId);

  if (field) {
    field.style.borderColor = '#ff4d4d';
    field.classList.add('error');
  }

  if (err) {
    err.textContent = message;
    err.style.color = '#ff4d4d';
    err.style.display = 'block';
  }
}

function clearFormErrors() {
  document.querySelectorAll('.error-msg').forEach(el => el.textContent = '');
  document.querySelectorAll('.form-input').forEach(el => {
    el.classList.remove('error');
    el.style.borderColor = '#e2e8f0';
  });
}

function showBanner(bannerId, message) {
  const el = document.getElementById(bannerId);
  if (!el) return;
  el.textContent = message;
  el.style.display = 'block';
}

function hideBanner(bannerId) {
  const el = document.getElementById(bannerId);
  if (el) el.style.display = 'none';
}

/* ── Password visibility toggle ─────────────────────────────── */
function togglePass(inputId) {
  const input = document.getElementById(inputId);
  if (!input) return;
  input.type = input.type === 'password' ? 'text' : 'password';
}

/* ── Modal helpers ──────────────────────────────────────────── */
// FIX: previous version toggled an ".active" CSS class.
// Many modals use inline style="display:none" which the .active class cannot
// override without !important. We now explicitly set display flex/none.
function openModal(id) {
  const el = document.getElementById(id);
  if (!el) return;
  el.style.display = 'flex';
  el.classList.add('active');
}

function closeModal(id) {
  const el = document.getElementById(id);
  if (!el) return;
  el.style.display = 'none';
  el.classList.remove('active');
}

/* Close any open modal on Escape */
document.addEventListener('keydown', e => {
  if (e.key === 'Escape') {
    document.querySelectorAll('.modal-overlay').forEach(m => {
      if (m.style.display !== 'none' && m.classList.contains('active')) {
        closeModal(m.id);
      }
    });
  }
});

/* Close modal when clicking the overlay backdrop (not the modal itself) */
document.addEventListener('click', e => {
  if (e.target.classList.contains('modal-overlay')) {
    closeModal(e.target.id);
  }
});

/* ── Auth token helpers ─────────────────────────────────────── */
// Separate keys for each role to prevent cross-role token misuse (403 bugs).
// IMPORTANT: Child tokens are stored under 'childToken'.
// Do NOT call localStorage.removeItem('token') anywhere in Group 2 pages —
// that key does NOT hold the child token. Use clearChildToken() instead.

function getParentToken()       { return localStorage.getItem('parentToken'); }
function setParentToken(token)  { localStorage.setItem('parentToken', token); }
function clearParentToken()     { localStorage.removeItem('parentToken'); }

function getChildToken()        { return localStorage.getItem('childToken'); }
function setChildToken(token)   { localStorage.setItem('childToken', token); }
function clearChildToken()      {
  localStorage.removeItem('childToken');
  // Also purge legacy 'token' key so old code paths don't revive a stale session
  localStorage.removeItem('token');
}

function getAdminToken()        { return localStorage.getItem('adminToken'); }
function setAdminToken(token)   { localStorage.setItem('adminToken', token); }
function clearAdminToken()      { localStorage.removeItem('adminToken'); }

// Legacy aliases — kept so pages not yet updated continue to compile.
// New code should always call the role-specific helpers above.
function getToken()  { return getParentToken() || getChildToken(); }
function setToken(t) { setParentToken(t); }
function clearToken(){ clearParentToken(); }

function requireAuth(redirectTo) {
  if (!getToken()) {
    window.location.href = redirectTo || 'index.html';
    return false;
  }
  return true;
}

function requireParentAuth(redirectTo) {
  if (!getParentToken()) {
    window.location.href = redirectTo || 'parent-login.html';
    return false;
  }
  return true;
}

function requireChildAuth(redirectTo) {
  if (!getChildToken()) {
    window.location.href = redirectTo || 'child_login.html';
    return false;
  }
  return true;
}

function requireAdminAuth(redirectTo) {
  if (!getAdminToken()) {
    window.location.href = redirectTo || 'admin-login.html';
    return false;
  }
  return true;
}

/* ── Logout ─────────────────────────────────────────────────── */
function logout(redirectTo) {
  localStorage.removeItem('parentToken');
  localStorage.removeItem('childToken');
  localStorage.removeItem('adminToken');
  localStorage.removeItem('token');         // legacy key cleanup
  localStorage.removeItem('parentEmail');
  localStorage.removeItem('userId');
  localStorage.removeItem('parentFullName');
  window.location.href = redirectTo || 'index.html';
}

/* ── Archetype helper (used by heroprof.html) ───────────────── */
// Personality Representation (presentation slide 13): derived from the 5 Core
// Traits synthesised by TraitSynthesisEngine on the backend — NOT from raw
// structured pillar percentages. Returns { label, description }.
function computeArchetype(profile) {
  const dims = {
    Spiritual : profile.spiritualTrait  ?? 0,
    Kindness  : profile.kindnessTrait   ?? 0,
    Curiosity : profile.curiosityTrait  ?? 0,
    Vitality  : profile.vitalityTrait   ?? 0,
    Resilience: profile.resilienceTrait ?? 0
  };

  const dominant = Object.entries(dims).reduce((a, b) => b[1] > a[1] ? b : a, ['', -1])[0];

  const archetypeMap = {
    Spiritual : { label: 'The Gentle Light',     description: 'A soul filled with compassion and faith' },
    Kindness  : { label: 'The Heart Connector',  description: 'A spirit that lights up every room with kindness' },
    Curiosity : { label: 'The Curious Explorer', description: 'A mind that never stops asking why' },
    Vitality  : { label: 'The Mighty Champion',  description: 'Strength and energy in every step' },
    Resilience: { label: 'The Wise Thinker',     description: 'Calm, focused, and always resilient' }
  };

  const allZero = Object.values(dims).every(v => v === 0);
  if (allZero) return { label: 'The Rising Star', description: 'Your adventure is just beginning!' };

  return archetypeMap[dominant] || { label: 'The Rising Star', description: 'Your adventure is just beginning!' };
}