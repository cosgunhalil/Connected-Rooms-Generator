using System.Collections.Generic;
using System.Diagnostics;
using CRG.Core;
using CRG.Generation;
using CRG.Geometry;
using CRG.Runtime;
using UnityEngine;

namespace CRG.Samples.GenerationShowcase
{
    // One generated level and its animation. The level is built one object per room, every room with the reveal
    // material; a MaterialPropertyBlock per room drives the shader. On a timeline from the moment it was built:
    // - the blueprint: the grid around the level glows in, then each room's outline, spreading through the doors;
    // - each room solidifies from the door it was entered through (the start room from its middle), in order of its
    //   distance from the start room, and its outline fades once it is solid;
    // - Clear dissolves every room again, farthest first, and Cleared turns true when the level can be destroyed.
    internal sealed class ShowcaseLevel
    {
        // The timeline, in seconds (about 5.5 for a whole level).
        private const float GridFadeIn = 0.5f;
        private const float GridAlpha = 0.35f;
        private const float OutlineStart = 0.15f;
        private const float OutlineSpread = 0.7f;
        private const float OutlineFadeIn = 0.3f;
        private const float RevealStart = 1.0f;
        private const float RevealSpread = 3.0f;
        private const float RevealJitter = 0.2f;
        private const float RevealDuration = 1.3f;
        private const float OutlineFadeOut = 0.5f;
        private const float ClearSpread = 0.35f;
        private const float ClearDuration = 0.5f;

        private static readonly int RevealID = Shader.PropertyToID("_Reveal");
        private static readonly int RevealOriginID = Shader.PropertyToID("_RevealOrigin");
        private static readonly int RevealRadiusID = Shader.PropertyToID("_RevealRadius");
        private static readonly int HeightDelayID = Shader.PropertyToID("_HeightDelay");
        private static readonly int LineAlphaID = Shader.PropertyToID("_LineAlpha");

        private sealed class RoomVisual
        {
            public Renderer Renderer;
            public Renderer Outline;
            public float Distance;
            public float Start;
            public Vector4 Origin;
            public float Radius;
            public float Reveal;
            public float RevealAtClear;
            public float OutlineAlpha;
        }

        private readonly List<RoomVisual> rooms = new List<RoomVisual>();
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private Renderer grid;
        private float age;
        private float clearAge = -1f;
        private float lastRevealEnd;

        public GameObject Root { get; private set; }
        public CRGLevelData Data { get; private set; }
        public GridType GridType { get; private set; }
        public int Seed { get; private set; }
        public int CellCount { get; private set; }
        public double GenerationMilliseconds { get; private set; }

        // Whether every room is solid and every outline gone.
        public bool Finished => clearAge < 0f && age >= lastRevealEnd + OutlineFadeOut;

        public bool Clearing => clearAge >= 0f;

        public bool Cleared => clearAge >= ClearSpread + ClearDuration;

        // Generates and builds a level under parent, everything hidden at first. Null when the settings are invalid.
        public static ShowcaseLevel Create(GenerationParameters parameters, Material reveal, Material blueprint, Transform parent)
        {
            if (!parameters.Validate(out string error))
            {
                UnityEngine.Debug.LogError("CRG showcase: invalid settings for " + parameters.GridType + ": " + error);
                return null;
            }

            Stopwatch clock = Stopwatch.StartNew();
            LevelGeometryGenerator generator = LevelGeometryGenerator.CreateFor(parameters);
            generator.SetMaterials(reveal, reveal, reveal);
            GameObject root = generator.GenerateLevelSeparateByRoom();
            clock.Stop();

            root.transform.SetParent(parent, false);
            ShowcaseLevel level = new ShowcaseLevel
            {
                Root = root,
                Data = root.GetComponent<CRGLevelData>(),
                GridType = parameters.GridType,
                GenerationMilliseconds = clock.Elapsed.TotalMilliseconds,
            };
            level.Seed = level.Data.Seed;
            level.Build(reveal, blueprint);
            return level;
        }

        private void Build(Material reveal, Material blueprint)
        {
            int maxDistance = 1;
            foreach (CRGLevelData.RoomData room in Data.Rooms)
            {
                maxDistance = Mathf.Max(maxDistance, room.DistanceFromStart);
            }

            float heightDelay = reveal.HasProperty(HeightDelayID) ? reveal.GetFloat(HeightDelayID) : 1f;
            Dictionary<int, Renderer> renderers = new Dictionary<int, Renderer>();
            foreach (Transform child in Root.transform)
            {
                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer != null && child.name.StartsWith("Room_") && int.TryParse(child.name.Substring(5), out int id))
                {
                    renderers[id] = renderer;
                }
            }

            System.Random jitter = new System.Random(Seed);
            foreach (CRGLevelData.RoomData room in Data.Rooms)
            {
                if (!renderers.TryGetValue(room.RoomID, out Renderer renderer))
                {
                    continue;
                }
                CellCount += room.Cells.Count;
                Vector3 origin = Root.transform.TransformPoint(GetEntrance(room));
                RoomVisual visual = new RoomVisual
                {
                    Renderer = renderer,
                    Outline = CreateRenderer("Blueprint " + room.RoomID, ShowcaseBlueprint.BuildRoomOutline(Data, room), blueprint),
                    Distance = room.DistanceFromStart / (float)maxDistance,
                    Origin = origin,
                    Radius = GetRadius(room, origin) + Data.WallHeight * heightDelay,
                };
                visual.Start = RevealStart + RevealSpread * visual.Distance + (float)jitter.NextDouble() * RevealJitter;
                lastRevealEnd = Mathf.Max(lastRevealEnd, visual.Start + RevealDuration);
                rooms.Add(visual);
            }

            Mesh gridMesh = ShowcaseBlueprint.BuildSurroundingGrid(Data);
            if (gridMesh != null)
            {
                grid = CreateRenderer("Blueprint Grid", gridMesh, blueprint);
            }
            Apply();
        }

        // Where a room is entered: the door to its neighbour nearest the start room (lowest distance, then lowest
        // ID), or the room's middle for the start room, in the level's local space.
        private Vector3 GetEntrance(CRGLevelData.RoomData room)
        {
            CRGLevelData.DoorData best = null;
            int bestDistance = int.MaxValue;
            int bestRoom = int.MaxValue;
            foreach (CRGLevelData.DoorData door in Data.Doors)
            {
                int other = door.RoomA == room.RoomID ? door.RoomB : door.RoomB == room.RoomID ? door.RoomA : -1;
                CRGLevelData.RoomData neighbor = other >= 0 ? Data.GetRoom(other) : null;
                if (neighbor == null || neighbor.DistanceFromStart >= room.DistanceFromStart)
                {
                    continue;
                }
                if (neighbor.DistanceFromStart < bestDistance || (neighbor.DistanceFromStart == bestDistance && other < bestRoom))
                {
                    best = door;
                    bestDistance = neighbor.DistanceFromStart;
                    bestRoom = other;
                }
            }
            return best != null ? Data.GetDoorCenterLocal(best) : Data.GetRoomAnchorLocalPosition(room);
        }

        // The farthest corner of the room from origin, seen from above.
        private float GetRadius(CRGLevelData.RoomData room, Vector3 origin)
        {
            float radius = 0f;
            foreach (CellCoord cell in room.Cells)
            {
                int corners = Data.GetCellEdgeCount(room, cell);
                for (int corner = 0; corner < corners; corner++)
                {
                    Vector3 point = Root.transform.TransformPoint(Data.GetCellCornerLocal(room, cell, corner));
                    radius = Mathf.Max(radius, Vector2.Distance(new Vector2(point.x, point.z), new Vector2(origin.x, origin.z)));
                }
            }
            return radius;
        }

        private Renderer CreateRenderer(string name, Mesh mesh, Material material)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(Root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        public void Update(float deltaTime)
        {
            age += deltaTime;
            if (clearAge >= 0f)
            {
                clearAge += deltaTime;
                foreach (RoomVisual room in rooms)
                {
                    // Farthest rooms go first, the start room last.
                    float t = (clearAge - ClearSpread * (1f - room.Distance)) / ClearDuration;
                    room.Reveal = room.RevealAtClear * (1f - Ease(Mathf.Clamp01(t)));
                    room.OutlineAlpha = 0f;
                }
                Apply();
                return;
            }

            foreach (RoomVisual room in rooms)
            {
                float outlineIn = Mathf.Clamp01((age - OutlineStart - OutlineSpread * room.Distance) / OutlineFadeIn);
                float outlineOut = Mathf.Clamp01((age - room.Start - RevealDuration) / OutlineFadeOut);
                room.OutlineAlpha = outlineIn * (1f - outlineOut);
                room.Reveal = Ease(Mathf.Clamp01((age - room.Start) / RevealDuration));
            }
            Apply();
        }

        // Starts dissolving the level from wherever its animation is.
        public void Clear()
        {
            if (clearAge >= 0f)
            {
                return;
            }
            clearAge = 0f;
            foreach (RoomVisual room in rooms)
            {
                room.RevealAtClear = room.Reveal;
            }
        }

        public Bounds GetBounds()
        {
            Bounds bounds = new Bounds(Root.transform.position, Vector3.zero);
            bool any = false;
            foreach (RoomVisual room in rooms)
            {
                if (any)
                {
                    bounds.Encapsulate(room.Renderer.bounds);
                }
                else
                {
                    bounds = room.Renderer.bounds;
                    any = true;
                }
            }
            return bounds;
        }

        public void Destroy()
        {
            if (Root != null)
            {
                foreach (MeshFilter filter in Root.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh != null && filter.sharedMesh.name == "CRG-Showcase-Blueprint")
                    {
                        Object.Destroy(filter.sharedMesh);
                    }
                }
                Object.Destroy(Root);
            }
        }

        private void Apply()
        {
            float gridAlpha = clearAge >= 0f ? 0f : GridAlpha * Mathf.Clamp01(age / GridFadeIn)
                * (1f - Mathf.Clamp01((age - (lastRevealEnd - 0.8f)) / 1f));
            foreach (RoomVisual room in rooms)
            {
                room.Renderer.GetPropertyBlock(block);
                block.SetFloat(RevealID, room.Reveal);
                block.SetVector(RevealOriginID, room.Origin);
                block.SetFloat(RevealRadiusID, room.Radius);
                room.Renderer.SetPropertyBlock(block);
                SetLineAlpha(room.Outline, room.OutlineAlpha);
            }
            if (grid != null)
            {
                SetLineAlpha(grid, gridAlpha);
            }
        }

        private void SetLineAlpha(Renderer renderer, float alpha)
        {
            renderer.enabled = alpha > 0.001f;
            renderer.GetPropertyBlock(block);
            block.SetFloat(LineAlphaID, alpha);
            renderer.SetPropertyBlock(block);
        }

        // Smoothstep: starts and ends gently.
        private static float Ease(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
