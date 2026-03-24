// HopExport -- Core .hop file generator for DYNESTIC CNC
// Inputs: folder (Item), fileName (Item), export (Item), dx (Item), dy (Item), dz (Item),
//         wzgv (Item), operationLines (List)
// Outputs: hopContent, statusMsg
//
// folder   -- output directory, e.g. D:/Projekte/test/
// fileName -- file name without extension, e.g. Tisch_bohren
//
// Assembles and writes syntactically valid .hop files from GH inputs.
// Follows the kukaprc_toolpath.cs guard-default-work-output pattern.
// Phase 3+ operation components wire their List<string> output into operationLines.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    string folder,
    string fileName,
    bool export,
    double dx, double dy, double dz,
    string wzgv,
    List<string> operationLines,
    ref object hopContent,
    ref object statusMsg)
  {
    // ---------------------------------------------------------------
    // 1. DEFAULTS -- downstream gets these if guards trigger
    // ---------------------------------------------------------------
    hopContent = "";
    statusMsg = "";

    // ---------------------------------------------------------------
    // 2. EXPORT GUARD -- silent return when not exporting
    // ---------------------------------------------------------------
    if (!export) return;

    // ---------------------------------------------------------------
    // 3. INPUT DEFAULTS -- fallback for disconnected inputs
    // ---------------------------------------------------------------
    if (dx <= 0) dx = 800.0;
    if (dy <= 0) dy = 400.0;
    if (dz <= 0) dz = 19.0;
    if (wzgv == null) wzgv = "7023K_681";

    // ---------------------------------------------------------------
    // 4. VALIDATION -- error messages for invalid required inputs
    // ---------------------------------------------------------------
    if (string.IsNullOrWhiteSpace(folder))
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "folder is empty");
      return;
    }

    if (string.IsNullOrWhiteSpace(fileName))
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "fileName is empty");
      return;
    }

    string directory = Path.GetFullPath(folder);

    if (!Directory.Exists(directory))
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "Directory does not exist: " + directory);
      return;
    }

    // Strip .hop extension if user typed it, then re-add -- always clean
    string stem = fileName.EndsWith(".hop", StringComparison.OrdinalIgnoreCase)
      ? fileName.Substring(0, fileName.Length - 4)
      : fileName;

    string fullPath = Path.Combine(directory, stem + ".hop");

    if (operationLines == null)
    {
      operationLines = new List<string>();
    }

    // ---------------------------------------------------------------
    // 5. BUILD HEADER -- match Muster_DXF_Import.hop order exactly
    // ---------------------------------------------------------------
    string ncName = stem;

    List<string> lines = new List<string>();
    lines.Add(";MAKROTYP=0");
    lines.Add(";INSTVERSION=");
    lines.Add(";EXEVERSION=");
    lines.Add(";BILD=");
    lines.Add(";INFO=");

    // WZGV is conditional -- omit line entirely if empty string
    if (!string.IsNullOrEmpty(wzgv))
    {
      lines.Add(";WZGV=" + wzgv);
    }

    lines.Add(";WZGVCONFIG=");
    lines.Add(";MASCHINE=HOLZHER");
    lines.Add(";NCNAME=" + ncName);
    lines.Add(";KOMMENTAR=");
    lines.Add(";DX=0.000");
    lines.Add(";DY=0.000");
    lines.Add(";DZ=0");
    lines.Add(";DIALOGDLL=Dialoge.Dll");
    lines.Add(";DIALOGPROC=StandardFormAnzeigen");
    lines.Add(";AUTOSCRIPTSTART=1");
    lines.Add(";BUTTONBILD=");
    lines.Add(";DIMENSION_UNIT=0");

    // ---------------------------------------------------------------
    // 6. BUILD VARS BLOCK -- 3-space indent, InvariantCulture decimals
    // ---------------------------------------------------------------
    lines.Add("VARS");
    lines.Add("   DX := " + dx.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension X");
    lines.Add("   DY := " + dy.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension Y");
    lines.Add("   DZ := " + dz.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension Z");

    // ---------------------------------------------------------------
    // 7. BUILD START SECTION -- Fertigteil + HH_Park
    // ---------------------------------------------------------------
    lines.Add("START");
    lines.Add("Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)");
    lines.Add("CALL HH_Park ( VAL PARK:=3,X:=0,Y:=0)");

    // ---------------------------------------------------------------
    // 8. INSERT OPERATION LINES -- Phase 3+ integration point
    // ---------------------------------------------------------------
    for (int i = 0; i < operationLines.Count; i++)
    {
      lines.Add(operationLines[i]);
    }

    // ---------------------------------------------------------------
    // 9. ASSEMBLE AND WRITE -- CRLF line endings, ASCII encoding
    // ---------------------------------------------------------------
    string content = string.Join("\r\n", lines) + "\r\n";
    File.WriteAllText(fullPath, content, Encoding.ASCII);

    // ---------------------------------------------------------------
    // 10. SUCCESS OUTPUT
    // ---------------------------------------------------------------
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark, "Exported: " + fullPath);
    hopContent = content;
    statusMsg = "Exported: " + fullPath;
  }
}
