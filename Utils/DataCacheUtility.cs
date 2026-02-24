using System.Collections.Concurrent;

namespace OpennessCopy.Utils;

/// <summary>
/// Static utility class for caching TIA Portal objects across threads
/// Extracted from duplicate caching code to provide shared functionality
/// </summary>
public static class DataCacheUtility
{
    private static readonly ConcurrentDictionary<string, object> Cache = new();
    private static int _nextId = 1;

    /// <summary>
    /// Caches an object and returns a generated unique identifier
    /// </summary>
    /// <param name="obj">Object to cache</param>
    /// <returns>Generated unique identifier for the cached object</returns>
    public static string CacheObject(object obj)
    {
        if (obj == null)
        {
            Logger.LogWarning("Cannot cache null object");
            return null;
        }

        var id = $"obj_{_nextId++}";
        Cache[id] = obj;
        return id;
    }

    /// <summary>
    /// Retrieves a cached object by its identifier
    /// </summary>
    /// <typeparam name="T">Expected type of the cached object</typeparam>
    /// <param name="objectId">Unique identifier of the cached object</param>
    /// <returns>The cached object cast to type T, or null if not found</returns>
    public static T GetCachedObject<T>(string objectId) where T : class
    {
        if (string.IsNullOrEmpty(objectId))
        {
            Logger.LogWarning("Cannot retrieve cached object with null or empty objectId");
            return null;
        }

        if (Cache.TryGetValue(objectId, out var cachedObject))
        {
            if (cachedObject is T typedObject)
            {
                return typedObject;
            }

            Logger.LogWarning($"Cached object with ID {objectId} is not of expected type {typeof(T).Name} (actual type: {cachedObject.GetType().Name})");
            return null;
        }

        Logger.LogWarning($"No cached object found with ID: {objectId}");
        return null;
    }

    /// <summary>
    /// Clears all cached objects
    /// </summary>
    public static void ClearCache()
    {
        var count = Cache.Count;
        Cache.Clear();
        _nextId = 1;
        Logger.LogInfo($"Cleared cache - removed {count} objects");
    }

    /// <summary>
    /// Gets the number of objects currently in cache
    /// </summary>
    public static int CacheSize => Cache.Count;
}
