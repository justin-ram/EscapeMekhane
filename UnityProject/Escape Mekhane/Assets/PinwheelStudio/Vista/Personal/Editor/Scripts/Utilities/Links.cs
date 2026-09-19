#if VISTA
using System;
using System.Text;

namespace Pinwheel.VistaEditor
{
    public static class Links
    {
        public static string CONTACT_PAGE => "https://pinwheelstud.io/contact";
        public static string SUPPORT_EMAIL => "pinwheel.customer@gmail.com";
        public static string DISCORD => "https://discord.gg/D4VehsCQXb";
        public static string DOC => "https://docs.pinwheelstud.io/vista";
        public static string YOUTUBE => "https://www.youtube.com/channel/UCebwuk5CfIe5kolBI9nuBTg";
        public static string FACEBOOK => "https://www.facebook.com/polaris.terrain";

        public static string PINWHEEL_PUBLISHER => "https://assetstore.unity.com/publishers/17305";

        /// <summary>
        /// The Asset Store listing for the edition currently installed, so store links (reviews, ratings) open
        /// the page the user actually owns rather than a fixed one. Falls back to Personal.
        /// </summary>
        public static string STORE_PAGE
        {
            get
            {
                if (EditorCommon.IsProEdition())
                    return VISTA_PRO;
                if (EditorCommon.IsIndieEdition())
                    return VISTA_INDIE;
                return VISTA_PERSONAL;
            }
        }

        public static string VEGETATION_ASSETS => "https://api.pinwheelstud.io/aff/vegetation-assets";
        public static string TEXTURE_ASSETS => "https://api.pinwheelstud.io/aff/texture-assets";
        public static string PROPS_ASSETS => "https://api.pinwheelstud.io/aff/props-assets";

        public static string VISTA_COMPARE_EDITIONS = "https://www.pinwheelstud.io/vista#pricing";
        public static string VISTA_PERSONAL => "https://assetstore.unity.com/packages/tools/terrain/vista-personal-procedural-terrain-generator-with-biomes-297327";
        public static string VISTA_INDIE => "https://assetstore.unity.com/packages/tools/terrain/vista-indie-procedural-terrain-editor-with-biomes-258826";
        public static string VISTA_PRO => "https://assetstore.unity.com/packages/tools/terrain/vista-pro-procedural-terrain-generator-with-biomes-264414";
        public static string POLARIS => "https://assetstore.unity.com/packages/tools/terrain/polaris-summit-low-poly-mesh-terrain-editor-286886";

        public static string GetDefaultNodeDocumentationUrl(Type nodeType)
        {
            string typeName = nodeType.Name;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < typeName.Length; i++)
            {
                char c = typeName[i];
                if (i > 0 && char.IsUpper(c))
                {
                    sb.Append('-');
                }
                sb.Append(char.ToLower(c));
            }
            return $"{DOC}/docs/nodes-reference-{sb}.html";
        }
    }
}
#endif
