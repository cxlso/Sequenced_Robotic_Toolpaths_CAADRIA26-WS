using System;
using System.IO;

using Rhino;
using Rhino.Geometry;
using Rhino.FileIO;

using Grasshopper;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
  private static string lastMessage = "No export yet";
  private static int versionCounter = 0;

  private void RunScript(Mesh mesh, string Path, bool Export, ref object Log)
  {
    Log = lastMessage;

    if (!Export) return;

    if (mesh == null)
    {
      lastMessage = "Mesh is null";
      Log = lastMessage;
      return;
    }

    if (string.IsNullOrEmpty(Path) || !Directory.Exists(Path))
    {
      lastMessage = "Invalid directory";
      Log = lastMessage;
      return;
    }

    try
    {
      // duplicate + clean mesh
      Mesh m = mesh.DuplicateMesh();

      if (!m.IsValid)
        m.RebuildNormals();

      m.Faces.ConvertQuadsToTriangles();
      m.Normals.ComputeNormals();
      m.UnifyNormals();
      m.Compact();

      if (!m.IsValid || m.Faces.Count == 0)
      {
        lastMessage = "Invalid mesh";
        Log = lastMessage;
        return;
      }

      var doc = RhinoDoc.ActiveDoc;

      // versioning
      versionCounter++;
      string fileName = $"mesh_{versionCounter.ToString("000")}.fbx";
      string fullPath = System.IO.Path.Combine(Path, fileName);

      // bake mesh
      Guid id = doc.Objects.AddMesh(m);

      doc.Objects.UnselectAll();
      doc.Objects.Select(id);

      // FBX export command (Rhino pipeline)
      string cmd = "_-Export \"" + fullPath + "\" _Enter";
      RhinoApp.RunScript(cmd, false);

      // cleanup
      doc.Objects.Delete(id, true);

      if (File.Exists(fullPath))
        lastMessage = "Exported: " + fileName;
      else
        lastMessage = "Export failed";
    }
    catch (Exception e)
    {
      lastMessage = "Error: " + e.Message;
    }

    Log = lastMessage;
  }
}