using System;
using System.Linq;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(bool Show, string Nickname)
  {
    var doc = GrasshopperDocument;
    if (doc == null) return;

    var objs = doc.Objects
      .Where(o => o.NickName == Nickname)
      .ToList();

    foreach (var obj in objs)
    {
      if (obj is IGH_PreviewObject previewObj)
      {
        previewObj.Hidden = !Show;
      }
    }

    RhinoDoc.ActiveDoc.Views.Redraw();
  }
}