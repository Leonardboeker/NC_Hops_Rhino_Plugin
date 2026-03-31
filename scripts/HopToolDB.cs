// HopToolDB -- Global tool database for DYNESTIC CNC
// Inputs:  toolNrs (List, int), toolTypes (List, string),
//          feedFactors (List, double), select (Item, int)
// Outputs: toolDB
//
// User defines one row per tool: toolNr / toolType / feedFactor.
// 'select' sets the active tool number read by op-components.
// Output is a Dictionary<string, object> wire format — the only type
// that can cross GH C# script component assembly boundaries.
// Phase 8 (compiled plugin) will replace this with an IGH_Goo type.
//
// GH canvas setup:
//   Input  toolNrs:      Type Hint -> Integer, Access = List
//   Input  toolTypes:    Type Hint -> Text, Access = List
//   Input  feedFactors:  Type Hint -> Number, Access = List
//   Input  select:       Type Hint -> Integer, Access = Item
//   Output toolDB:       rename to "toolDB"
//   Component display name: "HopToolDB"

using System;
using System.Collections.Generic;
using System.Globalization;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
    List<int> toolNrs,
    List<string> toolTypes,
    List<double> feedFactors,
    int select,
    ref object toolDB)
  {
    // ---------------------------------------------------------------
    // 1. DEFAULTS
    // ---------------------------------------------------------------
    toolDB = null;

    // ---------------------------------------------------------------
    // 2. GUARDS — list count validation
    // ---------------------------------------------------------------
    if (toolNrs == null || toolNrs.Count == 0)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        "HopToolDB: no tool numbers provided");
      return;
    }
    if (toolTypes == null || toolTypes.Count != toolNrs.Count)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        string.Format("HopToolDB: toolTypes count ({0}) must match toolNrs count ({1})",
          toolTypes == null ? 0 : toolTypes.Count, toolNrs.Count));
      return;
    }
    if (feedFactors == null || feedFactors.Count != toolNrs.Count)
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
        string.Format("HopToolDB: feedFactors count ({0}) must match toolNrs count ({1})",
          feedFactors == null ? 0 : feedFactors.Count, toolNrs.Count));
      return;
    }

    // ---------------------------------------------------------------
    // 3. BUILD DICTIONARY — wire format is Dictionary<string, object> (per D-11)
    //    Top-level key "activeToolNr" stores select (int)
    //    Per-tool keys: "tool_" + toolNr.ToString() -> inner Dictionary<string, object>
    // ---------------------------------------------------------------
    var db = new Dictionary<string, object>();
    db["activeToolNr"] = select;

    for (int i = 0; i < toolNrs.Count; i++)
    {
      string key = "tool_" + toolNrs[i].ToString();
      var entry = new Dictionary<string, object>();
      entry["toolNr"]     = toolNrs[i];
      entry["toolType"]   = string.IsNullOrEmpty(toolTypes[i]) ? "WZF" : toolTypes[i];
      entry["feedFactor"] = feedFactors[i] <= 0 ? 1.0 : feedFactors[i];
      db[key] = entry;
    }

    // ---------------------------------------------------------------
    // 4. VALIDATE ACTIVE TOOL EXISTS IN DB (per D-14)
    // ---------------------------------------------------------------
    string selectedKey = "tool_" + select.ToString();
    if (!db.ContainsKey(selectedKey))
    {
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
        string.Format("HopToolDB: selected tool {0} not found in defined tools", select));
      // Continue — still output the DB so user can see the issue without losing other tool data
    }

    // ---------------------------------------------------------------
    // 5. REMARK — tool inventory summary
    // ---------------------------------------------------------------
    this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
      string.Format("{0} tools defined, active toolNr={1}", toolNrs.Count, select));

    // ---------------------------------------------------------------
    // 6. OUTPUT
    // ---------------------------------------------------------------
    toolDB = db;
  }
}
