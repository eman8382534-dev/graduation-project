// ============================================================
//  ChildIdentityService.cs — Phase 10 Refactor: Privacy Adapter Layer
//
//  PURPOSE:
//  The system spec calls for a `hashed_child_id` privacy layer.
//  The PRODUCTION DATABASE IS LOCKED — no migrations, no new
//  columns, no `hashed_child_id` column anywhere.
//
//  This service provides an OPTIONAL, CODE-ONLY mapping:
//      GetSafeChildKey(int childId) -> string
//
//  - It does NOT persist anything.
//  - It does NOT replace `ChildId` as the database key.
//  - `ChildId` (int, FK to AspNetUsers.Id) remains the ONLY
//    identifier used for EF Core queries / joins / DB writes.
//  - The "safe key" is a deterministic, non-reversible-looking
//    string derived on the fly, intended ONLY for:
//      • external AI-team payloads (Phase 9 integration)
//      • log lines / telemetry where the raw int ID should not
//        be casually visible
//      • any future API surface that wants a privacy-friendly
//        identifier WITHOUT a DB migration
//
//  If/when the DB is later unlocked and a real `hashed_child_id`
//  column is added, this service's implementation can be swapped
//  to read that column instead — callers do not need to change.
// ============================================================

using System.Security.Cryptography;
using System.Text;

namespace GrawiaaApp.API.Services
{
    public interface IChildIdentityService
    {
        /// <summary>
        /// Returns a deterministic, privacy-friendly key for the given
        /// integer ChildId. NOT persisted, NOT a DB lookup — derived
        /// on demand. Safe to use in logs / external payloads where the
        /// raw AspNetUsers.Id should not be exposed.
        ///
        /// IMPORTANT: This is NOT the database key. All EF Core queries
        /// must continue to use the raw int ChildId.
        /// </summary>
        string GetSafeChildKey(int childId);
    }

    public class ChildIdentityService : IChildIdentityService
    {
        // Per-deployment salt. Read from configuration if present so the
        // derived key is stable across app restarts within one environment,
        // but does not require any DB or schema change.
        private readonly string _salt;

        public ChildIdentityService(IConfiguration configuration)
        {
            _salt = configuration["ChildIdentity:Salt"] ?? "grawiaa-phase10-default-salt";
        }

        public string GetSafeChildKey(int childId)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes($"{_salt}:{childId}");
            var hash = sha.ComputeHash(bytes);

            // Short, URL-safe, deterministic — e.g. "child_9f3a1c2b"
            var hex = Convert.ToHexString(hash).ToLowerInvariant();
            return $"child_{hex[..12]}";
        }
    }
}
