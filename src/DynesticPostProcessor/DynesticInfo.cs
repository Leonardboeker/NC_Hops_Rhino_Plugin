using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace DynesticPostProcessor
{
    public class DynesticInfo : GH_AssemblyInfo
    {
        public override string Name        => "DYNESTIC Post-Processor";
        public override string Version     => "0.1.0";
        public override string Description => "Grasshopper plugin for generating NC-Hops .hop files for the DYNESTIC CNC machine. Operations, nesting, and export.";
        public override string AuthorName  => "DYNESTIC";
        public override string AuthorContact => "";
        public override Guid Id            => new Guid("60f3eecf-690b-440c-ba19-87e4823b3953");
        public override Bitmap Icon        => null;
    }
}
