#if VISTA
namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// A UI surface that can temporarily host the coordinator-owned review prompt.
    /// Implementations may be editor windows, inspectors, or other editor UI.
    /// </summary>
    public interface ICanShowReviewPrompt
    {
        bool isReviewPromptHostInFocus { get; }

        void AttachReviewPrompt(ReviewPrompt prompt);
    }
}
#endif
