// HopSheetExport -- Per-sheet .hop file export for nested DYNESTIC CNC workflow
// Inputs:  parts (List, object), ids (List, int), sheetCurve (Item, Curve),
//          sheetIndex (Item, int), folder (Item, string), fileName (Item, string),
//          wzgv (Item, string), dz (Item, double), export (Item, bool)
// Outputs: hopContent, statusMsg
//
// Receives HopPart dictionaries + OpenNest IDS output.
// Filters parts where ids[i] == sheetIndex.
// Extracts dx/dy from sheetCurve BoundingBox (same pattern as HopSheet.cs).
// Assembles .hop file identical to HopExport structure.
// Per D-08, D-09, D-10: one HopSheetExport per sheet, manual trigger, user names files.
//
// GH canvas setup:
//   Input  parts:      Type Hint -> NO TYPE HINT,  List Access
//   Input  ids:        Type Hint -> int,            List Access
//   Input  sheetCurve: Type Hint -> Curve
//   Input  sheetIndex: Type Hint -> int
//   Input  folder:     Type Hint -> string
//   Input  fileName:   Type Hint -> string
//   Input  wzgv:       Type Hint -> string
//   Input  dz:         Type Hint -> double
//   Input  export:     Type Hint -> bool
//   Output hopContent: rename to "hopContent"
//   Output statusMsg:  rename to "statusMsg"
//   Component display name: "HopSheetExport"

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    List<object> parts,
    List<int> ids,
    Curve sheetCurve,
    int sheetIndex,
    string folder,
    string fileName,
    string wzgv,
    double dz,
    bool export,
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
    if (wzgv == null) wzgv = "7023K_681";
    if (dz <= 0) dz = 19.0;

    // ---------------------------------------------------------------
    // 4. VALIDATION -- error messages for invalid required inputs
    // ---------------------------------------------------------------
    if (string.IsNullOrWhiteSpace(folder))
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "HopSheetExport: folder is empty");
      return;
    }

    if (string.IsNullOrWhiteSpace(fileName))
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error, "HopSheetExport: fileName is empty");
      return;
    }

    string directory = Path.GetFullPath(folder);

    if (!Directory.Exists(directory))
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error,
        "HopSheetExport: directory does not exist: " + directory);
      return;
    }

    // Strip .hop extension if user typed it, then re-add -- always clean
    string stem = fileName.EndsWith(".hop", StringComparison.OrdinalIgnoreCase)
      ? fileName.Substring(0, fileName.Length - 4)
      : fileName;

    string fullPath = Path.Combine(directory, stem + ".hop");

    if (parts == null || parts.Count == 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error,
        "HopSheetExport: no parts connected");
      return;
    }

    if (ids == null || ids.Count == 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error,
        "HopSheetExport: no IDS connected (connect OpenNest IDS output)");
      return;
    }

    if (parts.Count != ids.Count)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error,
        "HopSheetExport: parts count (" + parts.Count
        + ") != ids count (" + ids.Count + ") -- lists must match");
      return;
    }

    if (sheetCurve == null)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error,
        "HopSheetExport: no sheet curve connected");
      return;
    }

    if (sheetIndex < 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Error,
        "HopSheetExport: sheetIndex must be >= 0");
      return;
    }

    // ---------------------------------------------------------------
    // 5. SHEET DIMENSIONS -- BoundingBox pattern from HopSheet.cs
    // ---------------------------------------------------------------
    BoundingBox sheetBB = sheetCurve.GetBoundingBox(true);
    double sheetDx = sheetBB.Max.X - sheetBB.Min.X;
    double sheetDy = sheetBB.Max.Y - sheetBB.Min.Y;
    // dz comes from input parameter (cannot derive from 2D sheet curve)

    // ---------------------------------------------------------------
    // 6. FILTER PARTS BY SHEET INDEX -- OpenNest IDS semantics
    // ---------------------------------------------------------------
    List<string> allOpLines = new List<string>();
    int partsOnSheet = 0;
    int unfittedCount = 0;

    for (int i = 0; i < ids.Count; i++)
    {
      if (ids[i] == -1)
      {
        unfittedCount++;
        continue;
      }
      if (ids[i] != sheetIndex) continue;

      // Unwrap HopPart dictionary
      var wrapper = parts[i] as Grasshopper.Kernel.Types.GH_ObjectWrapper;
      if (wrapper == null) continue;
      var dict = wrapper.Value as Dictionary<string, object>;
      if (dict == null) continue;

      var opLineGroups = dict["operationLines"] as List<List<string>>;
      if (opLineGroups != null)
      {
        foreach (var group in opLineGroups)
        {
          foreach (string line in group)
          {
            allOpLines.Add(line);
          }
        }
      }
      partsOnSheet++;
    }

    if (unfittedCount > 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Warning,
        unfittedCount + " part(s) did not fit on any sheet (IDS=-1)");
    }

    if (partsOnSheet == 0)
    {
      this.Component.AddRuntimeMessage(
        GH_RuntimeMessageLevel.Warning,
        "HopSheetExport: no parts assigned to sheet " + sheetIndex);
      return;
    }

    // ---------------------------------------------------------------
    // 7. BUILD HEADER -- match HopExport.cs / Muster_DXF_Import.hop
    //    order exactly
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
    // 8. BUILD VARS BLOCK -- 3-space indent, InvariantCulture decimals
    // ---------------------------------------------------------------
    lines.Add("VARS");
    lines.Add("   DX := " + sheetDx.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension X");
    lines.Add("   DY := " + sheetDy.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension Y");
    lines.Add("   DZ := " + dz.ToString(CultureInfo.InvariantCulture) + ";*VAR*Dimension Z");

    // ---------------------------------------------------------------
    // 9. BUILD START SECTION -- Fertigteil + HH_Park
    // ---------------------------------------------------------------
    lines.Add("START");
    lines.Add("Fertigteil (DX,DY,DZ,0,0,0,0,0,'',0,0,0)");
    lines.Add("CALL HH_Park ( VAL PARK:=3,X:=0,Y:=0)");

    // ---------------------------------------------------------------
    // 10. INSERT OPERATION LINES -- all parts on this sheet
    // ---------------------------------------------------------------
    for (int i = 0; i < allOpLines.Count; i++)
    {
      lines.Add(allOpLines[i]);
    }

    // ---------------------------------------------------------------
    // 11. ASSEMBLE AND WRITE -- CRLF line endings, ASCII encoding
    // ---------------------------------------------------------------
    string content = string.Join("\r\n", lines) + "\r\n";
    File.WriteAllText(fullPath, content, Encoding.ASCII);

    // ---------------------------------------------------------------
    // 12. SUCCESS OUTPUT
    // ---------------------------------------------------------------
    this.Component.AddRuntimeMessage(
      GH_RuntimeMessageLevel.Remark,
      "HopSheetExport: exported sheet " + sheetIndex
      + " (" + partsOnSheet + " parts, " + allOpLines.Count + " op lines) -> " + fullPath);
    hopContent = content;
    statusMsg = "Exported sheet " + sheetIndex + ": " + fullPath;
  }
}
