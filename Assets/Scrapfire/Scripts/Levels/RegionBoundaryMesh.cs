using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scrapfire.Levels
{
    /// <summary>Vertical exterior of a box union. Internal/overlapping faces are omitted.</summary>
    public static class RegionBoundaryMesh
    {
        /// <summary>Union cross-section at a fixed height; overlapping boxes are filled only once.</summary>
        public static Mesh BuildSurface(RegionVolume[] volumes, float height)
        {
            var boxes = new List<Bounds>();
            var xs = new SortedSet<float>();
            var zs = new SortedSet<float>();
            foreach (var volume in volumes ?? Array.Empty<RegionVolume>())
            {
                var box = new Bounds(volume.Center, new Vector3(Mathf.Max(0.01f, volume.Size.x),
                    Mathf.Max(0.01f, volume.Size.y), Mathf.Max(0.01f, volume.Size.z)));
                if (height < box.min.y || height > box.max.y) continue;
                boxes.Add(box);
                xs.Add(box.min.x); xs.Add(box.max.x);
                zs.Add(box.min.z); zs.Add(box.max.z);
            }
            var x = new List<float>(xs);
            var z = new List<float>(zs);
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i + 1 < x.Count; i++)
                for (int k = 0; k + 1 < z.Count; k++)
                {
                    var center = new Vector3((x[i] + x[i + 1]) * 0.5f, height, (z[k] + z[k + 1]) * 0.5f);
                    bool inside = false;
                    foreach (var box in boxes) if (box.Contains(center)) { inside = true; break; }
                    if (!inside) continue;
                    int start = vertices.Count;
                    vertices.Add(new Vector3(x[i], height, z[k]));
                    vertices.Add(new Vector3(x[i + 1], height, z[k]));
                    vertices.Add(new Vector3(x[i + 1], height, z[k + 1]));
                    vertices.Add(new Vector3(x[i], height, z[k + 1]));
                    uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
            var mesh = new Mesh { name = "Locked region area", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Build(RegionVolume[] volumes, float contourWidth = 0f)
        {
            var bounds = new List<Bounds>();
            var xs = new SortedSet<float>();
            var ys = new SortedSet<float>();
            var zs = new SortedSet<float>();
            foreach (var volume in volumes ?? Array.Empty<RegionVolume>())
            {
                var box = new Bounds(volume.Center, new Vector3(Mathf.Max(0.01f, volume.Size.x),
                    Mathf.Max(0.01f, volume.Size.y), Mathf.Max(0.01f, volume.Size.z)));
                bounds.Add(box);
                xs.Add(box.min.x); xs.Add(box.max.x);
                ys.Add(box.min.y); ys.Add(box.max.y);
                zs.Add(box.min.z); zs.Add(box.max.z);
            }
            var x = new List<float>(xs);
            var y = new List<float>(ys);
            var z = new List<float>(zs);
            int nx = Mathf.Max(0, x.Count - 1), ny = Mathf.Max(0, y.Count - 1), nz = Mathf.Max(0, z.Count - 1);
            if ((long)nx * ny * nz > 250000)
                throw new InvalidOperationException("Region boundary is too complex. Split it into smaller regions.");
            var occupied = new bool[nx, ny, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    for (int k = 0; k < nz; k++)
                    {
                        var middle = new Vector3((x[i] + x[i + 1]) * 0.5f,
                            (y[j] + y[j + 1]) * 0.5f, (z[k] + z[k + 1]) * 0.5f);
                        foreach (var box in bounds)
                            if (box.Contains(middle)) { occupied[i, j, k] = true; break; }
                    }
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var grid = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    for (int k = 0; k < nz; k++)
                    {
                        if (!occupied[i, j, k]) continue;
                        if (i == 0 || !occupied[i - 1, j, k])
                            Side(new Vector3(x[i], y[j], z[k]), new Vector3(x[i], y[j], z[k + 1]),
                                new Vector3(x[i], y[j + 1], z[k + 1]), new Vector3(x[i], y[j + 1], z[k]));
                        if (i == nx - 1 || !occupied[i + 1, j, k])
                            Side(new Vector3(x[i + 1], y[j], z[k]), new Vector3(x[i + 1], y[j], z[k + 1]),
                                new Vector3(x[i + 1], y[j + 1], z[k + 1]), new Vector3(x[i + 1], y[j + 1], z[k]));
                        if (k == 0 || !occupied[i, j, k - 1])
                            Side(new Vector3(x[i], y[j], z[k]), new Vector3(x[i + 1], y[j], z[k]),
                                new Vector3(x[i + 1], y[j + 1], z[k]), new Vector3(x[i], y[j + 1], z[k]));
                        if (k == nz - 1 || !occupied[i, j, k + 1])
                            Side(new Vector3(x[i], y[j], z[k + 1]), new Vector3(x[i + 1], y[j], z[k + 1]),
                                new Vector3(x[i + 1], y[j + 1], z[k + 1]), new Vector3(x[i], y[j + 1], z[k + 1]));
                    }
            var mesh = new Mesh { name = "Region exterior sides", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, grid);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            void Side(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Quad(a, b, c, d);
                if (contourWidth <= 0f) return;
                // Horizontal contour bands remain visible when a side plane is edge-on to an
                // orthographic camera. They are presentation only, never collision geometry.
                Vector3 offset = Vector3.Cross(b - a, d - a).normalized * contourWidth * 0.5f;
                for (float level = Mathf.Ceil(a.y); level <= d.y; level += 1f)
                {
                    var from = new Vector3(a.x, level + 0.02f, a.z);
                    var to = new Vector3(b.x, level + 0.02f, b.z);
                    Quad(from - offset, to - offset, to + offset, from + offset);
                }
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                float width = Vector3.Distance(a, b), height = Vector3.Distance(a, d);
                grid.Add(Vector2.zero); grid.Add(new Vector2(width, 0f));
                grid.Add(new Vector2(width, height)); grid.Add(new Vector2(0f, height));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
        }
    }
}
