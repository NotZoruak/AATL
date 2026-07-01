using ColorDocument.Avalonia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Markdown.Avalonia
{
    /// <summary>
    /// Markdown 文档缓存管理器
    /// 提供高效的文档缓存、LRU 淘汰策略和内存管理
    /// </summary>
    public sealed class MarkdownDocumentCache
    {
        #region 单例模式

        private static readonly Lazy<MarkdownDocumentCache> s_instance =
            new Lazy<MarkdownDocumentCache>(() => new MarkdownDocumentCache());

        /// <summary>
        /// 获取缓存管理器的单例实例
        /// </summary>
        public static MarkdownDocumentCache Instance => s_instance.Value;

        #endregion

        #region 缓存配置

        private int _maxCacheSize = 50;
        private long _maxMemoryBytes = 100 * 1024 * 1024; // 100MB
        private TimeSpan _defaultExpiration = TimeSpan.FromMinutes(30);
        private bool _enableMemoryPressureCleanup = true;

        /// <summary>
        /// 最大缓存条目数
        /// </summary>
        public int MaxCacheSize
        {
            get => _maxCacheSize;
            set => _maxCacheSize = Math.Max(1, value);
        }

        /// <summary>
        /// 最大内存使用量（字节）
        /// </summary>
        public long MaxMemoryBytes
        {
            get => _maxMemoryBytes;
            set => _maxMemoryBytes = Math.Max(1024 * 1024, value); // 最小 1MB
        }

        /// <summary>
        /// 默认过期时间
        /// </summary>
        public TimeSpan DefaultExpiration
        {
            get => _defaultExpiration;
            set => _defaultExpiration = value;
        }

        /// <summary>
        /// 是否启用内存压力清理
        /// </summary>
        public bool EnableMemoryPressureCleanup
        {
            get => _enableMemoryPressureCleanup;
            set => _enableMemoryPressureCleanup = value;
        }

        #endregion

        #region 缓存数据结构

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly ReaderWriterLockSlim _cleanupLock = new();
        private long _estimatedMemoryUsage;
        private DateTime _lastCleanupTime = DateTime.UtcNow;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 缓存条目
        /// </summary>
        private class CacheEntry
        {
            public DocumentElement Document { get; }
            public string MarkdownContent { get; }
            public DateTime CreatedAt { get; }
            public DateTime LastAccessedAt { get; set; }
            public DateTime ExpiresAt { get; }
            public int AccessCount { get; set; }
            public long EstimatedSize { get; }

            public CacheEntry(DocumentElement document, string markdownContent, TimeSpan expiration)
            {
                Document = document;
                MarkdownContent = markdownContent;
                CreatedAt = DateTime.UtcNow;
                LastAccessedAt = DateTime.UtcNow;
                ExpiresAt = DateTime.UtcNow.Add(expiration);
                AccessCount = 1;
                // 估算内存大小：Markdown 内容大小 + 文档对象估算大小
                EstimatedSize = Encoding.UTF8.GetByteCount(markdownContent) * 3; // 粗略估算
            }

            public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 计算内容的哈希值
        /// </summary>
        public static string ComputeContentHash(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// 尝试从缓存获取文档
        /// </summary>
        public bool TryGet(string contentHash, out DocumentElement? document)
        {
            document = null;

            if (string.IsNullOrEmpty(contentHash))
                return false;

            if (_cache.TryGetValue(contentHash, out var entry))
            {
                if (entry.IsExpired)
                {
                    // 过期了，移除
                    TryRemove(contentHash);
                    return false;
                }

                // 更新访问信息
                entry.LastAccessedAt = DateTime.UtcNow;
                entry.AccessCount++;
                document = entry.Document;

                System.Diagnostics.Debug.WriteLine($"[MarkdownCache] 缓存命中: {contentHash.Substring(0, 8)}..., 访问次数: {entry.AccessCount}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 尝试从缓存获取文档（使用原始内容）
        /// </summary>
        public bool TryGetByContent(string markdownContent, out DocumentElement? document)
        {
            var hash = ComputeContentHash(markdownContent);
            return TryGet(hash, out document);
        }

        /// <summary>
        /// 添加文档到缓存
        /// </summary>
        public void Add(string contentHash, DocumentElement document, string markdownContent)
        {
            Add(contentHash, document, markdownContent, _defaultExpiration);
        }

        /// <summary>
        /// 添加文档到缓存（指定过期时间）
        /// </summary>
        public void Add(string contentHash, DocumentElement document, string markdownContent, TimeSpan expiration)
        {
            if (string.IsNullOrEmpty(contentHash) || document == null)
                return;

            // 检查是否需要清理
            TryCleanup();

            var entry = new CacheEntry(document, markdownContent, expiration);

            // 如果已存在，更新
            if (_cache.TryGetValue(contentHash, out var existingEntry))
            {
                Interlocked.Add(ref _estimatedMemoryUsage, -existingEntry.EstimatedSize);
            }

            _cache[contentHash] = entry;
            Interlocked.Add(ref _estimatedMemoryUsage, entry.EstimatedSize);

            System.Diagnostics.Debug.WriteLine($"[MarkdownCache] 添加缓存: {contentHash.Substring(0, 8)}..., 大小: {entry.EstimatedSize / 1024}KB, 总缓存: {_cache.Count}");

            // 检查是否超出限制
            EnforceLimits();
        }

        /// <summary>
        /// 移除缓存条目
        /// </summary>
        public bool TryRemove(string contentHash)
        {
            if (_cache.TryRemove(contentHash, out var entry))
            {
                Interlocked.Add(ref _estimatedMemoryUsage, -entry.EstimatedSize);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            Interlocked.Exchange(ref _estimatedMemoryUsage, 0);
            System.Diagnostics.Debug.WriteLine("[MarkdownCache] 缓存已清空");
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            var entries = _cache.Values.ToList();
            return new CacheStatistics
            {
                TotalEntries = entries.Count,
                EstimatedMemoryUsage = _estimatedMemoryUsage,
                OldestEntry = entries.Count > 0 ? entries.Min(e => e.CreatedAt) : DateTime.MinValue,
                NewestEntry = entries.Count > 0 ? entries.Max(e => e.CreatedAt) : DateTime.MinValue,
                MostAccessedCount = entries.Count > 0 ? entries.Max(e => e.AccessCount) : 0,
                ExpiredEntries = entries.Count(e => e.IsExpired)
            };
        }

        /// <summary>
        /// 当前缓存条目数
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// 估算的内存使用量
        /// </summary>
        public long EstimatedMemoryUsage => _estimatedMemoryUsage;

        #endregion

        #region 私有方法

        private void TryCleanup()
        {
            // 检查是否需要定期清理
            if (DateTime.UtcNow - _lastCleanupTime < _cleanupInterval)
                return;

            if (!_cleanupLock.TryEnterWriteLock(0))
                return;

            try
            {
                // 移除过期条目
                var expiredKeys = _cache
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    TryRemove(key);
                }

                if (expiredKeys.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[MarkdownCache] 清理过期条目: {expiredKeys.Count}");
                }

                _lastCleanupTime = DateTime.UtcNow;
            }
            finally
            {
                _cleanupLock.ExitWriteLock();
            }
        }

        private void EnforceLimits()
        {
            // 检查条目数限制
            while (_cache.Count > _maxCacheSize)
            {
                RemoveLeastRecentlyUsed();
            }

            // 检查内存限制
            while (_estimatedMemoryUsage > _maxMemoryBytes && _cache.Count > 0)
            {
                RemoveLeastRecentlyUsed();
            }

            // 检查内存压力
            if (_enableMemoryPressureCleanup)
            {
                CheckMemoryPressure();
            }
        }

        private void RemoveLeastRecentlyUsed()
        {
            // 找到最少使用的条目（综合考虑访问时间和访问次数）
            var lruEntry = _cache
                .OrderBy(kvp => kvp.Value.LastAccessedAt)
                .ThenBy(kvp => kvp.Value.AccessCount)
                .FirstOrDefault();

            if (lruEntry.Key != null)
            {
                TryRemove(lruEntry.Key);
                System.Diagnostics.Debug.WriteLine($"[MarkdownCache] LRU 淘汰: {lruEntry.Key.Substring(0, 8)}...");
            }
        }

        private void CheckMemoryPressure()
        {
            try
            {
                // 获取当前进程的内存使用情况
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64;
                var privateMemory = process.PrivateMemorySize64;

                // 如果私有内存超过 1GB，开始清理缓存
                if (privateMemory > 1024L * 1024 * 1024)
                {
                    int targetCount = _cache.Count / 2;
                    while (_cache.Count > targetCount && _cache.Count > 0)
                    {
                        RemoveLeastRecentlyUsed();
                    }
                    System.Diagnostics.Debug.WriteLine($"[MarkdownCache] 内存压力清理，剩余: {_cache.Count}");
                }
            }
            catch
            {
                // 忽略内存检查错误
            }
        }

        #endregion

        #region 构造函数

        private MarkdownDocumentCache()
        {
            // 私有构造函数，单例模式
        }

        #endregion
    }

    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public long EstimatedMemoryUsage { get; set; }
        public DateTime OldestEntry { get; set; }
        public DateTime NewestEntry { get; set; }
        public int MostAccessedCount { get; set; }
        public int ExpiredEntries { get; set; }

        public override string ToString()
        {
            return $"缓存统计: 条目数={TotalEntries}, 内存≈{EstimatedMemoryUsage / 1024}KB, 过期={ExpiredEntries}";
        }
    }
}