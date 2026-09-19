#if VISTA
using System;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>Marks a static, parameterless method that returns a <see cref="WizardTool"/>.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class VistaWizardToolAttribute : Attribute
    {
    }
}
#endif
