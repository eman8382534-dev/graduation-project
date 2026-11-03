using Microsoft.Extensions.Caching.Memory;

namespace GrawiaaApp.API.Services
{
    /// <summary>
    /// خدمة الكاشينج المركزية - تسرع جميع عمليات قراءة الداتابيز
    /// </summary>
    public interface ICacheService
    {
        Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
        void Remove(string key);
        void RemoveByPrefix(string prefix);
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly HashSet<string> _keys = new();
        private readonly object _lock = new();

        // مدة الكاشينج الافتراضية 5 دقايق
        private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

        public CacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            if (_cache.TryGetValue(key, out T? cached))
                return cached;

            T? result;
            try
            {
                result = await factory();
            }
            catch
            {
                return default;
            }

            // Don't cache null results
            if (result == null) return default;

            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(expiry ?? DefaultExpiry)
                .SetSlidingExpiration(TimeSpan.FromMinutes(2));

            // FIX: Register a post-eviction callback so that when MemoryCache
            // automatically evicts an entry (via sliding/absolute expiry), we also
            // remove it from _keys. Without this, _keys grew indefinitely because
            // only explicit Remove() calls cleaned it up.
            options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
            {
                lock (_lock) { _keys.Remove(evictedKey.ToString()!); }
            });

            _cache.Set(key, result, options);

            lock (_lock)
            {
                _keys.Add(key);
            }

            return result;
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            lock (_lock) { _keys.Remove(key); }
        }

        public void RemoveByPrefix(string prefix)
        {
            lock (_lock)
            {
                var toRemove = _keys.Where(k => k.StartsWith(prefix)).ToList();
                foreach (var key in toRemove)
                {
                    _cache.Remove(key);
                    _keys.Remove(key);
                }
            }
        }
    }

    /// <summary>
    /// مفاتيح الكاشينج الثابتة - تنظيم مركزي لتجنب التكرار
    /// </summary>
    public static class CacheKeys
    {
        // Admin
        public static string AdminDashboard => "admin:dashboard";
        public static string AllUsers => "admin:users:all";
        public static string AllHobbies => "admin:hobbies:all";
        public static string AllBadges => "admin:badges:all";
        public static string AllCharacters => "admin:characters:all";
        public static string AllLeafyTips => "admin:leafytips:all";
        public static string AllLogFormItems => "admin:logformitems:all";
        public static string AllPersonalityClusters => "admin:personalityclusters:all";

        // Parent
        public static string ParentDashboard(int parentId) => $"parent:{parentId}:dashboard";
        public static string ParentChildren(int parentId) => $"parent:{parentId}:children";
        public static string ParentChildProfile(int parentId, int childId) => $"parent:{parentId}:child:{childId}:profile";
        public static string ParentTasks(int parentId) => $"parent:{parentId}:tasks";
        public static string ParentRewards(int parentId) => $"parent:{parentId}:rewards";
        public static string ParentNotifications(int parentId) => $"parent:{parentId}:notifications";

        // Child
        public static string ChildProfile(int childId) => $"child:{childId}:profile";
        public static string ChildTasks(int childId) => $"child:{childId}:tasks";
        public static string ChildRewards(int childId) => $"child:{childId}:rewards";
        public static string ChildBadges(int childId) => $"child:{childId}:badges";
        public static string ChildLogs(int childId) => $"child:{childId}:logs";
        public static string ChildHobbies(int childId) => $"child:{childId}:hobbies";

        // Prefixes for bulk invalidation
        public static string ParentPrefix(int parentId) => $"parent:{parentId}:";
        public static string ChildPrefix(int childId) => $"child:{childId}:";
        public const string AdminPrefix = "admin:";
    }
}
