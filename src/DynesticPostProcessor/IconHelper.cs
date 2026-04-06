using System.Drawing;
using System.Reflection;

namespace DynesticPostProcessor
{
    internal static class IconHelper
    {
        private static readonly Assembly _asm = typeof(IconHelper).Assembly;

        public static Bitmap Load(string name)
        {
            var stream = _asm.GetManifestResourceStream(
                "DynesticPostProcessor.Icons." + name + ".png");
            return stream != null ? new Bitmap(stream) : null;
        }
    }
}
