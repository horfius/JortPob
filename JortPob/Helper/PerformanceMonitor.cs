using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;
using JortPob.Common;
using System.IO;

#nullable enable

namespace JortPob
{
    public static class PerformanceMonitor
    {
        // Get the most 
        private static ConcurrentDictionary<int, PerformanceSection> ThreadSections { get; } = new();

        private static int ThreadId => Thread.CurrentThread.ManagedThreadId;

        public static PerformanceSection? CurrentSection
        {
            get
            {
                return ThreadSections.GetValueOrDefault(ThreadId);
            }
        }

        public static PerformanceSection TrackPerformance([CallerMemberName] string callingMethod = "", [CallerFilePath] string callingFile = "")
        {
            if (!string.IsNullOrWhiteSpace(callingFile))
                callingFile = Path.GetFileNameWithoutExtension(callingFile);
            else
                callingFile = "NONE";
            // may be null which means this is the top-most performance section of this thread
            var parent = CurrentSection;
            var newSection = new PerformanceSection($"{callingFile}.{callingMethod}", parent);
            // override the current thread section
            return ThreadSections.AddOrUpdate(ThreadId, newSection, (_, old) =>
            {
                // Pause the active timer until this returns to being the current section for the thread
                old.Pause();
                return newSection;
            });
        }

        public static void ReportResults(PerformanceSection section)
        {
            // TODO: report performance metrics
            var currentSection = CurrentSection;
            if (!ReferenceEquals(currentSection, section))
            {
                Lort.Log("WARNING: PerformanceSection reporting for non-current section", Lort.Type.Debug);
            }
            else if (section.ParentSection != null && !section.ParentSection.DisposedValue) // Parent should never be disposed of before child...
            {
                if (!ThreadSections.TryUpdate(ThreadId, section.ParentSection, section))
                {
                    Lort.Log("WARNING: PerformanceSection reporting failed to set new current section for thread", Lort.Type.Debug);
                }
                else
                    // Make sure to resume the parent section's active timer
                    section.ParentSection.Start();
            }
            else
            {
                if (!ThreadSections.TryRemove(ThreadId, out _))
                {
                    Lort.Log("WARNING: PerformanceSection reporting failed to clear final section for thread", Lort.Type.Debug);
                }
            }

            Lort.Log($"[{DateTime.UtcNow:G}] {section.Name}:\t\tElapsedTime={section.TotalElapsedTime}\t\tActiveTime={section.ActiveElapsedTime}\t\tStartingMemory={section.StartingMemory}\t\tEndingMemory={section.EndingMemory}\t\tStartTime={section.StartTime:u}\t\tEndTime={section.EndTime:u}",
                Lort.Type.Performance);
        }
    }

    public class PerformanceSection : IDisposable
    {
        // Measures the total time since this performance section was created
        private Stopwatch _stopwatch = new();

        // Measures the total time that this performance section was 'current'
        private Stopwatch _activeStopwatch = new();

        public bool DisposedValue { get; private set; }

        public PerformanceSection? ParentSection { get; init; } = null;

        public DateTime StartTime { get; init; }

        public DateTime EndTime { get; private set; }

        /// <summary>
        /// Gets the elapsed time on the timer in milliseconds
        /// </summary>
        public long TotalElapsedTime => _stopwatch.ElapsedMilliseconds;

        public long ActiveElapsedTime => _activeStopwatch.ElapsedMilliseconds;

        public long StartingMemory { get; init; }

        public long EndingMemory { get; private set; }

        /// <summary>
        /// Performance section name, may be either the caller method name or a specified name
        /// </summary>
        public string Name { get; init; }

        public PerformanceSection(string name, PerformanceSection? parentSection = null)
        {
            Name = name;
            ParentSection = parentSection;
            StartingMemory = Process.GetCurrentProcess().PrivateMemorySize64;
            StartTime = DateTime.Now;
            _stopwatch.Start();
            _activeStopwatch.Start();
        }

        public void Start()
        {
            _activeStopwatch.Start();
        }

        public void Pause()
        {
            _activeStopwatch.Stop();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!DisposedValue)
            {
                if (disposing)
                {
                    _activeStopwatch.Stop();
                    _stopwatch.Stop();
                    EndTime = DateTime.Now;
                    EndingMemory = Process.GetCurrentProcess().PrivateMemorySize64;
                    PerformanceMonitor.ReportResults(this);
                }

                DisposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
