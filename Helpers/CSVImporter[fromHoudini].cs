using System;
using System.IO;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // Persistent cache
  private string lastPath = null;
  private DateTime lastWriteTime = DateTime.MinValue;

  private List<Curve> cache_curves = new List<Curve>();
  private List<int> cache_layers = new List<int>();
  private GH_Structure<GH_Number> cache_mix = new GH_Structure<GH_Number>();
  private GH_Structure<GH_Point> cache_pts = new GH_Structure<GH_Point>();

  private void RunScript(
		string Path,
		bool Import,
		ref object Curves,
		ref object Layer_num,
		ref object Mix,
		ref object PointsTree)
  {
    // Always output cached data
    Curves = cache_curves;
    Layer_num = cache_layers;
    Mix = cache_mix;
    PointsTree = cache_pts;

    // No trigger → do nothing
    if (!Import) return;

    if (string.IsNullOrEmpty(Path) || !File.Exists(Path)) return;

    DateTime writeTime = File.GetLastWriteTime(Path);

    // Only reload if file changed
    if (Path == lastPath && writeTime == lastWriteTime)
      return;

    lastPath = Path;
    lastWriteTime = writeTime;

    // Clear cache
    cache_curves = new List<Curve>();
    cache_layers = new List<int>();
    cache_mix = new GH_Structure<GH_Number>();
    cache_pts = new GH_Structure<GH_Point>();

    using (var reader = new StreamReader(Path))
    {
      string headerLine = reader.ReadLine();
      if (headerLine == null) return;

      char sep = headerLine.Contains(";") ? ';' : ',';
      var headers = headerLine.Split(sep);

      int idx_curve = Array.IndexOf(headers, "curve_id");
      int idx_layer = Array.IndexOf(headers, "layer");
      int idx_x = Array.IndexOf(headers, "x");
      int idx_y = Array.IndexOf(headers, "y");
      int idx_z = Array.IndexOf(headers, "z");
      int idx_mix = Array.IndexOf(headers, "mix");

      if (idx_curve < 0 || idx_layer < 0 || idx_x < 0 ||
          idx_y < 0 || idx_z < 0 || idx_mix < 0)
        return;

      int current_id = -1;
      int current_layer = 0;

      var pts = new List<Point3d>();
      var mx = new List<double>();

      int curve_index = 0;

      while (!reader.EndOfStream)
      {
        var line = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(line)) continue;

        var cols = line.Split(sep);

        int cid = Convert.ToInt32(cols[idx_curve]);

        if (cid != current_id)
        {
          if (pts.Count > 0)
          {
            var path = new GH_Path(curve_index);

            cache_curves.Add(new PolylineCurve(pts));
            cache_layers.Add(current_layer);

            for (int i = 0; i < mx.Count; i++)
              cache_mix.Append(new GH_Number(mx[i]), path);

            for (int i = 0; i < pts.Count; i++)
              cache_pts.Append(new GH_Point(pts[i]), path);

            curve_index++;
          }

          pts = new List<Point3d>();
          mx = new List<double>();

          current_id = cid;
          current_layer = Convert.ToInt32(cols[idx_layer]);
        }

        double x = Convert.ToDouble(cols[idx_x]);
        double y = Convert.ToDouble(cols[idx_y]);
        double z = Convert.ToDouble(cols[idx_z]);

        // same transform as Python
        pts.Add(new Point3d(x, -z, y));
        mx.Add(Convert.ToDouble(cols[idx_mix]));
      }

      // last curve
      if (pts.Count > 0)
      {
        var path = new GH_Path(curve_index);

        cache_curves.Add(new PolylineCurve(pts));
        cache_layers.Add(current_layer);

        for (int i = 0; i < mx.Count; i++)
          cache_mix.Append(new GH_Number(mx[i]), path);

        for (int i = 0; i < pts.Count; i++)
          cache_pts.Append(new GH_Point(pts[i]), path);
      }
    }
  }
}