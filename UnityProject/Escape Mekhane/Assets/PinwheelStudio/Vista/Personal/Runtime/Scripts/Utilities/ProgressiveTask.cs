#if VISTA
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Describes the lifecycle state of one <see cref="VistaManager"/> generation request.
    /// </summary>
    public enum GenerationStatus
    {
        /// <summary>The request is waiting for active generation to finish.</summary>
        Pending,
        /// <summary>The request is active, including any cooperative cancellation unwind.</summary>
        Running,
        /// <summary>The request finished successfully.</summary>
        Completed,
        /// <summary>The request was superseded before starting or observed cancellation while active.</summary>
        Cancelled
    }

    /// <summary>
    /// Yieldable handle representing one Vista Manager generation request.
    /// </summary>
    /// <remarks>
    /// A task identifies the exact request returned by <see cref="VistaManager.Generate"/>. Yielding it waits for that
    /// request to reach either successful completion or completed cancellation cleanup; it does not wait for later requests.
    /// Task state is controlled by the Manager coordinator and cannot be changed by callers.
    /// </remarks>
    public sealed class GenerationTask : CustomYieldInstruction
    {
        private static readonly GenerationTask s_completedTask = CreateCompletedTask();

        /// <summary>Gets an immutable, already-completed task for operations that have nothing to wait for.</summary>
        public static GenerationTask completedTask => s_completedTask;

        /// <summary>Gets the current lifecycle state of this request.</summary>
        public GenerationStatus status { get; private set; } = GenerationStatus.Pending;

        /// <summary>Gets whether this request reached either terminal state.</summary>
        public bool isCompleted => status == GenerationStatus.Completed || status == GenerationStatus.Cancelled;

        /// <summary>Gets whether this request reached the cancelled terminal state.</summary>
        public bool isCancelled => status == GenerationStatus.Cancelled;

        /// <summary>Returns <see langword="true"/> until this request reaches a terminal state.</summary>
        public override bool keepWaiting => !isCompleted;

        internal void MarkRunning()
        {
            if (status == GenerationStatus.Pending)
                status = GenerationStatus.Running;
        }

        internal void MarkCompleted()
        {
            if (!isCompleted)
                status = GenerationStatus.Completed;
        }

        internal void MarkCancelled()
        {
            if (!isCompleted)
                status = GenerationStatus.Cancelled;
        }

        private static GenerationTask CreateCompletedTask()
        {
            GenerationTask task = new GenerationTask();
            task.MarkCompleted();
            return task;
        }
    }

    /// <summary>
    /// Coroutine-backed task handle that can be yielded until some Vista process reports completion.
    /// </summary>
    public class ProgressiveTask : CustomYieldInstruction
    {
        /// <summary>
        /// Indicates whether the task has been marked as finished.
        /// </summary>
        public bool isCompleted { get; private set; }
        /// <summary>
        /// Returns <see langword="true"/> while the task is still running so Unity keeps yielding on it.
        /// </summary>
        public override bool keepWaiting
        {
            get
            {
                return !isCompleted;
            }
        }        

        /// <summary>
        /// Marks the task as completed so any waiter stops yielding on it.
        /// </summary>
        public void Complete()
        {
            isCompleted = true;
        }
    }
}
#endif


