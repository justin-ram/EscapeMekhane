#if VISTA
using System;
using System.Collections.Generic;

namespace Pinwheel.Vista.Graph
{
    /// <summary>
    /// Represents one asynchronous graph execution run.
    /// </summary>
    /// <remarks>
    /// This handle is returned by <see cref="TerrainGraph.Execute(string[], TerrainGenerationConfigs, GraphInputContainer, FillArgumentsHandler)"/>.
    /// Callers can yield on it as a <see cref="ProgressiveTask"/>, inspect execution progress while it
    /// runs, extract outputs from <see cref="data"/> after completion, and then dispose it to stop any
    /// remaining coroutines and release still-owned transient resources.
    /// </remarks>
    public class ExecutionHandle : ProgressiveTask, IDisposable
    {
        /// <summary>Gets the exception that terminated graph execution.</summary>
        public Exception exception { get; private set; }

        /// <summary>Gets whether graph execution terminated because an exception occurred.</summary>
        public bool isFaulted => exception != null;

        private List<CoroutineHandle> m_coroutines;
        internal List<CoroutineHandle> coroutines
        {
            get
            {
                return m_coroutines;
            }
        }

        private DataPool m_data;
        /// <summary>
        /// Transient resource pool owned by this execution.
        /// </summary>
        /// <remarks>
        /// Completed graph outputs remain in this pool until the caller removes them, typically with
        /// <see cref="DataPool.RemoveRTFromPool(string)"/> or
        /// <see cref="DataPool.RemoveBufferFromPool(string)"/>. Anything left in the pool when
        /// <see cref="Dispose"/> is called is released automatically.
        /// </remarks>
        public DataPool data
        {
            get
            {
                return m_data;
            }
        }

        private TransientResourceRegistry m_transientResources;
        internal TransientResourceRegistry transientResources
        {
            get
            {
                return m_transientResources;
            }
        }

        private CancellationSignal m_cancellationSignal;
        internal CancellationSignal cancellation
        {
            get
            {
                return m_cancellationSignal;
            }
        }
        /// <summary>
        /// Gets whether cooperative cancellation has been requested for this execution.
        /// </summary>
        public bool isCancellationRequested
        {
            get
            {
                return m_cancellationSignal != null && m_cancellationSignal.isCancellationRequested;
            }
        }

        private ExecutionProgress m_progress;
        /// <summary>
        /// Progress state shared by the currently running graph execution.
        /// </summary>
        /// <remarks>
        /// This object is updated by the progressive execution loop so callers can inspect completed
        /// nodes, total work, or similar runtime progress information while the handle is active.
        /// </remarks>
        public ExecutionProgress progress
        {
            get
            {
                return m_progress;
            }
        }

        /// <summary>
        /// Creates a new execution handle with its own progress tracker, coroutine list, data pool,
        /// and transient-resource registry, observing the supplied cancellation signal.
        /// </summary>
        /// <param name="cancellation">Read-only cancellation state supplied by the caller.</param>
        /// <returns>
        /// A ready-to-use handle for one graph execution run.
        /// </returns>
        internal static ExecutionHandle Create(CancellationSignal cancellation)
        {
            ExecutionHandle handle = new ExecutionHandle();
            handle.m_data = new DataPool();
            handle.m_transientResources = new TransientResourceRegistry();
            handle.m_cancellationSignal = cancellation;
            handle.m_progress = new ExecutionProgress();
            handle.m_coroutines = new List<CoroutineHandle>();
            return handle;
        }

        /// <summary>
        /// Stops any tracked execution coroutines and releases resources still owned by this handle.
        /// </summary>
        /// <remarks>
        /// Call this after you have removed any outputs you want to keep from <see cref="data"/>.
        /// Disposing the handle without extracting outputs first will also dispose those pooled graph
        /// results.
        /// </remarks>
        public void Dispose()
        {
            if (m_coroutines != null)
            {
                foreach (CoroutineHandle c in m_coroutines)
                {
                    CoroutineUtility.StopCoroutine(c);
                }
            }
            try
            {
                DisposeTransientResources();
            }
            finally
            {
                if (m_data != null)
                {
                    m_data.Dispose();
                }
            }
        }

        internal void DisposeTransientResources()
        {
            if (m_transientResources != null)
            {
                m_transientResources.Dispose();
            }
        }

        internal void Fail(Exception failure)
        {
            exception = failure ?? new InvalidOperationException("Graph execution failed without an exception.");
            Complete();
        }
    }
}
#endif
