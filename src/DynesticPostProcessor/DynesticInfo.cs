using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace WallabyHop
{
    public class WallabyHopInfo : GH_AssemblyInfo
    {
        public override string Name        => "Wallaby Hop";
        public override string Version     => "0.1.0";
        public override string Description => "Grasshopper plugin for generating NC-Hops .hop files for the HOLZ-HER DYNESTIC CNC. Parametric operations, nesting, cabinet generation, and .hop export.";
        public override string AuthorName  => "Wallaby Hop";
        public override string AuthorContact => "";
        public override Guid Id            => new Guid("60f3eecf-690b-440c-ba19-87e4823b3953");
        public override Bitmap Icon        => IconHelper.Load("WallabyHop");
    }
}
