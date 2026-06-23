using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Threading.Tasks;
using Rhino;
using Rhino.Geometry;

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
		PointCloud Pointcloud,
		Color Color,
		double ColorTolerance,
		double Distance,
		ref object Centers)
  {
    if (Pointcloud == null || Pointcloud.Count == 0)
    {
      Centers = null;
      return;
    }

    if (!Pointcloud.ContainsColors)
    {
      Centers = null;
      return;
    }

    ColorTolerance = Math.Max(0.0, Math.Min(1.0, ColorTolerance));

    double targetH, targetS, targetV;
    ColorToHSV(Color, out targetH, out targetS, out targetV);

    double hueTol = 180.0 * ColorTolerance;
    double satTol = ColorTolerance;
    double valTol = ColorTolerance;

    ConcurrentBag<Point3d> matchedPtsBag = new ConcurrentBag<Point3d>();

    Parallel.For(0, Pointcloud.Count, i =>
    {
      PointCloudItem item = Pointcloud[i];
      Color c = item.Color;

      if (ColorTolerance <= 0.0)
      {
        if (c.ToArgb() == Color.ToArgb())
          matchedPtsBag.Add(item.Location);
      }
      else
      {
        double h, s, v;
        ColorToHSV(c, out h, out s, out v);

        double dh = HueDistance(targetH, h);
        double ds = Math.Abs(targetS - s);
        double dv = Math.Abs(targetV - v);

        if (dh <= hueTol && ds <= satTol && dv <= valTol)
          matchedPtsBag.Add(item.Location);
      }
    });

    List<Point3d> matchedPts = new List<Point3d>(matchedPtsBag);

    if (matchedPts.Count == 0)
    {
      Centers = null;
      return;
    }

    if (matchedPts.Count == 1)
    {
      Centers = new List<Point3d>() { matchedPts[0] };
      return;
    }

    double tol = RhinoDoc.ActiveDoc != null
      ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
      : 0.01;

    double clusterRadius;

    if (Distance > 0.0)
    {
      clusterRadius = Distance;
    }
    else
    {
      BoundingBox bb = new BoundingBox(matchedPts);
      double diag = bb.Diagonal.Length;
      double estimatedSpacing = diag / Math.Max(1.0, Math.Sqrt(matchedPts.Count));
      clusterRadius = Math.Max(tol * 2.0, estimatedSpacing * 2.5);
    }

    RTree tree = new RTree();
    for (int i = 0; i < matchedPts.Count; i++)
      tree.Insert(matchedPts[i], i);

    bool[] visited = new bool[matchedPts.Count];
    List<Point3d> centers = new List<Point3d>();

    for (int i = 0; i < matchedPts.Count; i++)
    {
      if (visited[i]) continue;

      List<int> cluster = FloodCluster(i, matchedPts, tree, visited, clusterRadius);

      if (cluster.Count == 0) continue;

      double x = 0.0;
      double y = 0.0;
      double z = 0.0;

      for (int j = 0; j < cluster.Count; j++)
      {
        Point3d p = matchedPts[cluster[j]];
        x += p.X;
        y += p.Y;
        z += p.Z;
      }

      double count = cluster.Count;
      centers.Add(new Point3d(x / count, y / count, z / count));
    }

    Centers = centers;
  }

  private List<int> FloodCluster(int startIndex, List<Point3d> pts, RTree tree, bool[] visited, double radius)
  {
    List<int> cluster = new List<int>();
    Queue<int> queue = new Queue<int>();

    visited[startIndex] = true;
    queue.Enqueue(startIndex);

    double r2 = radius * radius;

    while (queue.Count > 0)
    {
      int current = queue.Dequeue();
      cluster.Add(current);

      List<int> neighbors = new List<int>();
      Sphere s = new Sphere(pts[current], radius);

      tree.Search(s, (sender, e) =>
      {
        int id = e.Id;
        if (id == current) return;

        if (pts[current].DistanceToSquared(pts[id]) <= r2)
          neighbors.Add(id);
      });

      for (int i = 0; i < neighbors.Count; i++)
      {
        int n = neighbors[i];
        if (!visited[n])
        {
          visited[n] = true;
          queue.Enqueue(n);
        }
      }
    }

    return cluster;
  }

  private double HueDistance(double h1, double h2)
  {
    double d = Math.Abs(h1 - h2);
    return Math.Min(d, 360.0 - d);
  }

  private void ColorToHSV(Color color, out double hue, out double saturation, out double value)
  {
    double r = color.R / 255.0;
    double g = color.G / 255.0;
    double b = color.B / 255.0;

    double max = Math.Max(r, Math.Max(g, b));
    double min = Math.Min(r, Math.Min(g, b));
    double delta = max - min;

    hue = 0.0;

    if (delta > 0.0)
    {
      if (max == r)
        hue = 60.0 * (((g - b) / delta) % 6.0);
      else if (max == g)
        hue = 60.0 * (((b - r) / delta) + 2.0);
      else
        hue = 60.0 * (((r - g) / delta) + 4.0);
    }

    if (hue < 0.0)
      hue += 360.0;

    saturation = (max <= 0.0) ? 0.0 : delta / max;
    value = max;
  }
}