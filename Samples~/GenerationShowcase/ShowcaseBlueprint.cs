using System.Collections.Generic;
using CRG.Core;
using CRG.Runtime;
using UnityEngine;

namespace CRG.Samples.GenerationShowcase
{
    // Builds the blueprint: thin glowing strips lying on the floor along cell edges, in the level's local space. One mesh
    // per room (its cells' outlines, each shared edge once), and for single-grid levels one more for the empty cells
    // around the rooms, the grid the rooms are carved from.
    internal static class ShowcaseBlueprint
    {
        // Strip width and lift above the floor, as fractions of the cell size.
        private const float WidthPerCellSize = 0.012f;
        private const float LiftPerCellSize = 0.004f;

        public static Mesh BuildRoomOutline(CRGLevelData data, CRGLevelData.RoomData room)
        {
            List<(Vector3, Vector3)> edges = new List<(Vector3, Vector3)>();
            HashSet<(Vector3Int, Vector3Int)> seen = new HashSet<(Vector3Int, Vector3Int)>();
            foreach (CellCoord cell in room.Cells)
            {
                int corners = data.GetCellEdgeCount(room, cell);
                for (int corner = 0; corner < corners; corner++)
                {
                    Vector3 a = data.GetCellCornerLocal(room, cell, corner);
                    Vector3 b = data.GetCellCornerLocal(room, cell, (corner + 1) % corners);
                    AddOnce(edges, seen, a, b, data.CellSize);
                }
            }
            return BuildStrips(edges, data.CellSize);
        }

        // The empty cells touching any room, for single-grid levels; null for mixed and hand-built levels, whose rooms
        // don't share one grid.
        public static Mesh BuildSurroundingGrid(CRGLevelData data)
        {
            if (data.IsMixed || data.IsHandBuilt)
            {
                return null;
            }
            IGridTopology topology = data.Topology;
            HashSet<CellCoord> roomCells = new HashSet<CellCoord>();
            foreach (CRGLevelData.RoomData room in data.Rooms)
            {
                roomCells.UnionWith(room.Cells);
            }

            List<CellCoord> empty = new List<CellCoord>();
            HashSet<CellCoord> added = new HashSet<CellCoord>();
            foreach (CellCoord cell in roomCells)
            {
                foreach (CellCoord neighbor in topology.GetNeighbors(cell))
                {
                    if (!roomCells.Contains(neighbor) && added.Add(neighbor))
                    {
                        empty.Add(neighbor);
                    }
                }
            }

            List<(Vector3, Vector3)> edges = new List<(Vector3, Vector3)>();
            HashSet<(Vector3Int, Vector3Int)> seen = new HashSet<(Vector3Int, Vector3Int)>();
            foreach (CellCoord cell in empty)
            {
                Vector3 center = topology.GetCellCenter(cell, data.CellSize);
                int corners = topology.GetEdgeCount(cell);
                for (int corner = 0; corner < corners; corner++)
                {
                    Vector3 a = center + topology.GetCornerOffset(cell, corner, data.CellSize);
                    Vector3 b = center + topology.GetCornerOffset(cell, (corner + 1) % corners, data.CellSize);
                    AddOnce(edges, seen, a, b, data.CellSize);
                }
            }
            return BuildStrips(edges, data.CellSize);
        }

        // Keeps each edge once whichever way round it comes, matching end points within a millimetre per metre of cell
        // size (presentation only: an edge drawn twice would just glow twice as bright).
        private static void AddOnce(List<(Vector3, Vector3)> edges, HashSet<(Vector3Int, Vector3Int)> seen, Vector3 a, Vector3 b, float cellSize)
        {
            float grid = cellSize * 0.001f;
            Vector3Int ka = Vector3Int.RoundToInt(a / grid);
            Vector3Int kb = Vector3Int.RoundToInt(b / grid);
            (Vector3Int, Vector3Int) key = Less(ka, kb) ? (ka, kb) : (kb, ka);
            if (seen.Add(key))
            {
                edges.Add((a, b));
            }
        }

        private static bool Less(Vector3Int a, Vector3Int b)
        {
            return a.x != b.x ? a.x < b.x : a.y != b.y ? a.y < b.y : a.z < b.z;
        }

        // One flat quad per edge, facing up, widened a little past each end so corners close.
        private static Mesh BuildStrips(List<(Vector3, Vector3)> edges, float cellSize)
        {
            float halfWidth = cellSize * WidthPerCellSize * 0.5f;
            Vector3 lift = Vector3.up * cellSize * LiftPerCellSize;
            List<Vector3> vertices = new List<Vector3>(edges.Count * 4);
            List<Vector3> normals = new List<Vector3>(edges.Count * 4);
            List<int> triangles = new List<int>(edges.Count * 6);
            foreach ((Vector3 a, Vector3 b) in edges)
            {
                Vector3 along = b - a;
                along.y = 0f;
                if (along.sqrMagnitude < 1e-8f)
                {
                    continue;
                }
                along.Normalize();
                Vector3 side = new Vector3(-along.z, 0f, along.x) * halfWidth;
                Vector3 start = a - along * halfWidth + lift;
                Vector3 end = b + along * halfWidth + lift;
                int first = vertices.Count;
                vertices.Add(start - side);
                vertices.Add(start + side);
                vertices.Add(end + side);
                vertices.Add(end - side);
                for (int i = 0; i < 4; i++)
                {
                    normals.Add(Vector3.up);
                }
                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 3);
            }

            Mesh mesh = new Mesh { name = "CRG-Showcase-Blueprint" };
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
