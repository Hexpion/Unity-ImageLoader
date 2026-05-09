using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Extensions.Unity.ImageLoader
{
    public abstract partial class Future<T>
    {
        // ConcurrentDictionary gives lock-free reads on the hot (cache-hit) path.
        // Writes still use the built-in fine-grained locking of ConcurrentDictionary.
        // Operations that must be atomic across check+modify (ClearMemoryCache / ClearMemoryCacheAll)
        // take the explicit _memoryCacheLock to prevent races between eviction and new references.
        internal static readonly ConcurrentDictionary<string, T> memoryCache
            = new ConcurrentDictionary<string, T>();
        private  static readonly object _memoryCacheLock = new object();

        // internal static void ClearMemoryCache()
        // {
        //     // Support for turning off domain reload in Project Settings/Editor/Enter Play Mode Settings
        //     // Sprites created with Sprite.Create gets destroyed when exiting play mode, so we need to clear the sprite cache, as otherwise the cache will be
        //     // filled with destroyed sprites when the user reenters play mode.
        //     lock (memoryCache) memoryCache.Clear();
        //         Reference<T>.Clear();
        // }

        /// <summary>
        /// Check the Memory cache contains sprite for the given url
        /// </summary>
        /// <param name="url">URL to the picture, web or local</param>
        /// <returns>Returns true if Sprite exists in Memory cache</returns>
        public static bool MemoryCacheContains(string url) => memoryCache.ContainsKey(url);

        /// <summary>
        /// Save sprite to Memory cache directly. Should be used for overloading cache system
        /// </summary>
        /// <param name="url">URL to the picture, web or local</param>
        /// <param name="obj">sprite which should be saved</param>
        /// <param name="replace">replace existed cached sprite if any</param>
        public static void SaveToMemoryCache(string url, T obj, bool replace = false, bool suppressMessage = false)
        {
            if (replace)
            {
                if (ImageLoader.settings.debugLevel.IsActive(DebugLevel.Trace) && !suppressMessage)
                    Debug.Log($"[ImageLoader] Save to Memory cache ({typeof(T).Name})\n{url}");
                memoryCache[url] = obj;
                return;
            }

            // TryAdd is atomic: returns false (without overwriting) if the key already exists.
            if (!memoryCache.TryAdd(url, obj))
            {
                if (ImageLoader.settings.debugLevel.IsActive(DebugLevel.Warning))
                    Debug.LogError($"[ImageLoader] Can't set to Memory cache ({typeof(T).Name}), because it already contains the key. Use 'replace = true' to replace\n{url}");
                return;
            }

            if (ImageLoader.settings.debugLevel.IsActive(DebugLevel.Trace) && !suppressMessage)
                Debug.Log($"[ImageLoader] Save to Memory cache ({typeof(T).Name})\n{url}");
        }

        /// <summary>
        /// Loads directly from Memory cache if exists and allowed
        /// </summary>
        /// <param name="url">URL to the picture, web or local</param>
        /// <returns>Returns null if not allowed to use Memory cache or if there is no cached Sprite</returns>
        public static Reference<T> LoadFromMemoryCacheRef(string url)
        {
            if (!memoryCache.TryGetValue(url, out var obj) || obj == null)
                return null;

            return new Reference<T>(url, obj);
        }

        /// <summary>
        /// Loads directly from Memory cache if exists and allowed
        /// </summary>
        /// <param name="url">URL to the picture, web or local</param>
        /// <returns>Returns null if not allowed to use Memory cache or if there is no cached Sprite</returns>
        public static T LoadFromMemoryCache(string url)
        {
            memoryCache.TryGetValue(url, out var value);
            return value;
        }

        /// <summary>
        /// Clear Memory cache for the given url
        /// </summary>
        /// <param name="url">URL to the picture, web or local</param>
        public static void ClearMemoryCache(string url, Action<T, DebugLevel> releaseMemory, DebugLevel logLevel = DebugLevel.Log)
        {
            if (ImageLoader.settings.debugLevel.IsActive(DebugLevel.Log))
                Debug.Log($"[ImageLoader] Clearing Memory cache ({typeof(T).Name})\n{url}");

            // The ref-count check and the cache remove must be atomic so that no new Reference
            // is created between the check and the removal.
            lock (_memoryCacheLock)
            {
                var refCount = Reference<T>.Counter(url);
                if (refCount > 0)
                    throw new Exception($"[ImageLoader] There are {refCount} references to the sprite, clear them first. URL={url}");

                if (memoryCache.TryRemove(url, out var cache))
                    Safe.Run(releaseMemory, cache, logLevel, logLevel);
            }
        }

        /// <summary>
        /// Clear Memory cache for all urls
        /// </summary>
        /// <param name="url">URL to the picture, web or local</param>
        public static void ClearMemoryCacheAll(Action<T, DebugLevel> releaseMemory, DebugLevel logLevel = DebugLevel.Log)
        {
            if (ImageLoader.settings.debugLevel.IsActive(DebugLevel.Log))
                Debug.Log($"[ImageLoader] Clearing Memory cache ({typeof(T).Name}) All");

            lock (_memoryCacheLock)
            {
                var toKeep = new List<KeyValuePair<string, T>>();
                foreach (var keyValue in memoryCache)
                {
                    var url = keyValue.Key;
                    var refCount = Reference<T>.Counter(url);
                    if (refCount > 0)
                    {
                        if (ImageLoader.settings.debugLevel.IsActive(DebugLevel.Error))
                            Debug.LogError($"[ImageLoader] There are {refCount} references to the object, clear them first. URL={url}");
                        toKeep.Add(keyValue);
                        continue;
                    }

                    Safe.Run(releaseMemory, keyValue.Value, logLevel, logLevel);
                }
                memoryCache.Clear();

                // Restore entries that still have live references.
                if (toKeep.Count > 0)
                {
                    foreach (var keyValue in toKeep)
                        memoryCache[keyValue.Key] = keyValue.Value;
                }
            }
        }
    }
}

