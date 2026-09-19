#if VISTA
namespace Pinwheel.VistaEditor.Wizard
{
    public readonly struct ToolAvailability
    {
        public bool isAvailable { get; }
        public string reason { get; }

        private ToolAvailability(bool isAvailable, string reason)
        {
            this.isAvailable = isAvailable;
            this.reason = reason ?? string.Empty;
        }

        public static ToolAvailability Available()
        {
            return new ToolAvailability(true, string.Empty);
        }

        public static ToolAvailability Unavailable(string reason)
        {
            return new ToolAvailability(false, reason);
        }
    }
}
#endif
