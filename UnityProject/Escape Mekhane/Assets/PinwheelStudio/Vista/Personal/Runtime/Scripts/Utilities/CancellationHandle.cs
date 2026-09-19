#if VISTA
namespace Pinwheel.Vista
{
    /// <summary>
    /// Controls cancellation for one operation and exposes its read-only signal to that operation.
    /// </summary>
    public sealed class CancellationHandle
    {
        private readonly CancellationSignal m_signal;
        private bool m_isCancellationRequested;

        internal bool isCancellationRequested
        {
            get
            {
                return m_isCancellationRequested;
            }
        }

        /// <summary>
        /// Gets the read-only cancellation signal to pass to the operation and its subprocesses.
        /// </summary>
        public CancellationSignal signal
        {
            get
            {
                return m_signal;
            }
        }

        public CancellationHandle()
        {
            m_signal = new CancellationSignal(this);
        }

        /// <summary>
        /// Permanently requests cancellation. Repeated calls have no additional effect.
        /// </summary>
        public void Cancel()
        {
            m_isCancellationRequested = true;
        }
    }

    /// <summary>
    /// Read-only cancellation state shared with an operation and its subprocesses.
    /// </summary>
    public sealed class CancellationSignal
    {
        private readonly CancellationHandle m_handle;

        /// <summary>
        /// Gets whether cancellation has been requested.
        /// </summary>
        public bool isCancellationRequested
        {
            get
            {
                return m_handle.isCancellationRequested;
            }
        }

        internal CancellationSignal(CancellationHandle handle)
        {
            m_handle = handle;
        }
    }
}
#endif
