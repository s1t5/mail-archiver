using System;
using System.Diagnostics;
using System.Threading;

namespace MailArchiver.Services
{
    /// <summary>
    /// Monitors memory usage
    /// </summary>
    public static class MemoryMonitor
    {
        private static readonly object _lock = new object();
        private static long _lastMemoryCheck = 0;
        private static long _lastMemoryUsage = 0;
        private static long _peakMemoryUsage = 0;
        private static readonly TimeSpan _memoryCheckInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets the current memory usage in bytes
        /// </summary>
        /// <returns>Current memory usage in bytes</returns>
        public static long GetCurrentMemoryUsage()
        {
            // Check if we should refresh the memory usage
            long now = Environment.TickCount64;
            if (now - _lastMemoryCheck > _memoryCheckInterval.TotalMilliseconds)
            {
                lock (_lock)
                {
                    if (now - _lastMemoryCheck > _memoryCheckInterval.TotalMilliseconds)
                    {
                        // Get the current memory usage
                        long currentMemoryUsage = Process.GetCurrentProcess().WorkingSet64;
                        
                        _lastMemoryUsage = currentMemoryUsage;
                        _lastMemoryCheck = now;
                        
                        // Update peak memory usage
                        if (currentMemoryUsage > _peakMemoryUsage)
                        {
                            _peakMemoryUsage = currentMemoryUsage;
                        }
                    }
                }
            }
            
            return _lastMemoryUsage;
        }

        /// <summary>
        /// Gets the current memory usage as a formatted string
        /// </summary>
        /// <returns>Formatted string with current memory usage</returns>
        public static string GetMemoryUsageFormatted()
        {
            long currentMemory = GetCurrentMemoryUsage();
            return FormatMemorySize(currentMemory);
        }

        /// <summary>
        /// Gets the peak memory usage as a formatted string
        /// </summary>
        /// <returns>Formatted string with peak memory usage</returns>
        public static string GetPeakMemoryUsageFormatted()
        {
            return FormatMemorySize(_peakMemoryUsage);
        }

        /// <summary>
        /// Formats a memory size in bytes to a human-readable string
        /// </summary>
        /// <param name="bytes">Memory size in bytes</param>
        /// <returns>Human-readable string</returns>
        private static string FormatMemorySize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:n1} {suffixes[counter]}";
        }

        /// <summary>
        /// Forces garbage collection and waits for pending finalizers
        /// </summary>
        public static void ForceGarbageCollection()
        {
            try
            {
                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch (Exception)
            {
                // Ignore exceptions in garbage collection
            }
        }
    }
}
