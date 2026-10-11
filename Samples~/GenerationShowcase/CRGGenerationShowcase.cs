using CRG.Core;
using CRG.Generation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CRG.Samples.GenerationShowcase
{
    // The generation showcase: press Space and a level grows from its start room outwards, the next grid type each
    // time (hexagon, square, triangle, octagon + square, then all of them mixed). The previous level dissolves first.
    // R builds the same level again. The scene's look (background, fog, light, bloom) is set up here, so the component
    // works in an empty scene; CRG > Samples > Create Generation Showcase Scene makes one with materials assigned.
    [AddComponentMenu("CRG/Samples/CRG Generation Showcase")]
    public sealed class CRGGenerationShowcase : MonoBehaviour
    {
        private static readonly GridType[] Order = { GridType.Hexagon, GridType.Square, GridType.Triangle, GridType.OctagonSquare, GridType.Mixed };

        [Header("Materials")]
        [Tooltip("A material with the CRG Showcase Reveal shader graph. Created from the shader when empty (Editor only).")]
        public Material revealMaterial;

        [Tooltip("A material with the CRG Showcase Blueprint shader graph. Created from the shader when empty (Editor only).")]
        public Material blueprintMaterial;

        [Header("Levels")]
        [Range(4, 40)]
        public int targetRoomCount = 12;

        [Tooltip("Edge length of every cell, in metres.")]
        public float cellSize = 5f;

        [Header("Look")]
        [Tooltip("Sets the background, fog, ambient light, a key light and bloom when the scene starts.")]
        public bool stylizeScene = true;

        public Color backgroundColor = new Color(0.015f, 0.02f, 0.035f);

        private ShowcaseLevel current;
        private ShowcaseLevel leaving;
        private int typeIndex;
        private int pendingType = -1;
        private int pendingSeed = -1;
        private CRGShowcaseOrbitCamera orbit;
        private GUIStyle panelStyle;

        private void Start()
        {
            if (!EnsureMaterials())
            {
                enabled = false;
                return;
            }
            Camera view = Camera.main;
            if (view == null)
            {
                view = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            }
            orbit = view.GetComponent<CRGShowcaseOrbitCamera>();
            if (orbit == null)
            {
                orbit = view.gameObject.AddComponent<CRGShowcaseOrbitCamera>();
            }
            if (stylizeScene)
            {
                Stylize(view);
            }
            Spawn(Order[typeIndex], -1);
            orbit.Snap();
        }

        private void Update()
        {
            if (ShowcaseInput.Pressed(' '))
            {
                if (leaving != null)
                {
                    // Already switching: each further press skips one more grid type.
                    pendingType = (pendingType + 1) % Order.Length;
                    pendingSeed = -1;
                }
                else
                {
                    Switch((typeIndex + 1) % Order.Length, -1);
                }
            }
            else if (ShowcaseInput.Pressed('r') && current != null && leaving == null)
            {
                Switch(typeIndex, current.Seed);
            }

            float deltaTime = Time.deltaTime;
            current?.Update(deltaTime);
            if (leaving != null)
            {
                leaving.Update(deltaTime);
                if (leaving.Cleared)
                {
                    leaving.Destroy();
                    leaving = null;
                    typeIndex = pendingType;
                    Spawn(Order[typeIndex], pendingSeed);
                }
            }
        }

        // Dissolves the current level, then builds a level of grid type Order[type] (seed -1: a new random one).
        private void Switch(int type, int seed)
        {
            pendingType = type;
            pendingSeed = seed;
            if (current == null)
            {
                typeIndex = type;
                Spawn(Order[type], seed);
                return;
            }
            leaving = current;
            current = null;
            leaving.Clear();
        }

        private void Spawn(GridType type, int seed)
        {
            current = ShowcaseLevel.Create(CreateParameters(type, seed), revealMaterial, blueprintMaterial, transform);
            if (current != null)
            {
                orbit.Frame(current.GetBounds());
                if (stylizeScene)
                {
                    // Fog that keeps the level clear and swallows the far background.
                    float size = Mathf.Max(current.GetBounds().extents.magnitude, 10f);
                    RenderSettings.fogDensity = 0.9f / (size * 4f);
                }
            }
        }

        private GenerationParameters CreateParameters(GridType type, int seed)
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.GridType = type;
            parameters.CellSize = cellSize;
            parameters.WallHeight = cellSize * 0.52f;
            parameters.DoorHeight = cellSize * 0.42f;
            parameters.DoorWidthRatio = 0.3f;
            parameters.WallThickness = cellSize * 0.05f;
            parameters.TargetRoomCount = targetRoomCount;
            parameters.MaxConnectionsPerRoom = 3;
            parameters.AddMeshCollider = false;
            parameters.CreateSpawnPoints = false;
            parameters.BakeNavMesh = false;
            parameters.RandomSeed = seed;

            // Rooms of about the same floor area whatever the cell shape.
            switch (type)
            {
                case GridType.Square:
                    parameters.MinCellsPerRoom = 2;
                    parameters.MaxCellsPerRoom = 6;
                    break;
                case GridType.Triangle:
                    parameters.MinCellsPerRoom = 4;
                    parameters.MaxCellsPerRoom = 12;
                    break;
                case GridType.OctagonSquare:
                    parameters.MinCellsPerRoom = 1;
                    parameters.MaxCellsPerRoom = 4;
                    break;
                default:
                    parameters.MinCellsPerRoom = 2;
                    parameters.MaxCellsPerRoom = 5;
                    break;
            }
            return parameters;
        }

        private bool EnsureMaterials()
        {
            revealMaterial = revealMaterial != null ? revealMaterial : CreateMaterial("Shader Graphs/CRG Showcase Reveal");
            blueprintMaterial = blueprintMaterial != null ? blueprintMaterial : CreateMaterial("Shader Graphs/CRG Showcase Blueprint");
            return revealMaterial != null && blueprintMaterial != null;
        }

        private static Material CreateMaterial(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("CRG showcase: the shader '" + shaderName + "' is missing. The sample needs URP and Shader Graph; use " +
                    "CRG > Samples > Create Generation Showcase Scene so player builds include the materials.");
                return null;
            }
            return new Material(shader) { name = shaderName.Substring(shaderName.LastIndexOf('/') + 1) };
        }

        // A dark, foggy stage with one warm key light and bloom, so the glowing edges stand out.
        private void Stylize(Camera view)
        {
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = backgroundColor;
            view.allowHDR = true;
            view.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = backgroundColor;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.19f, 0.26f);

#if UNITY_2022_2_OR_NEWER
            bool hasLight = FindAnyObjectByType<Light>() != null;
            bool hasVolume = FindAnyObjectByType<Volume>() != null;
#else
            bool hasLight = FindObjectOfType<Light>() != null;
            bool hasVolume = FindObjectOfType<Volume>() != null;
#endif
            if (!hasLight)
            {
                Light key = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
                key.type = LightType.Directional;
                key.color = new Color(1f, 0.92f, 0.82f);
                key.intensity = 1.1f;
                key.shadows = LightShadows.Soft;
                key.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }
            if (!hasVolume)
            {
                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(0.9f);
                bloom.intensity.Override(1.6f);
                bloom.scatter.Override(0.7f);
                Tonemapping tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.ACES);
                Vignette vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.3f);
                Volume volume = new GameObject("Showcase Post-processing", typeof(Volume)).GetComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }
        }

        private void OnGUI()
        {
            if (panelStyle == null)
            {
                Texture2D background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                background.Apply();
                panelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 14,
                    richText = true,
                    padding = new RectOffset(12, 12, 10, 10),
                };
                panelStyle.normal.background = background;
                panelStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);
            }

            ShowcaseLevel shown = current ?? leaving;
            string level = shown == null
                ? "Switching..."
                : $"<b>{Name(shown.GridType)}</b>  ({typeIndex + 1}/{Order.Length})\n" +
                  $"Seed {shown.Seed}   Rooms {shown.Data.Rooms.Count}   Cells {shown.CellCount}   Doors {shown.Data.Doors.Count}\n" +
                  $"Generated in {shown.GenerationMilliseconds:F1} ms";
            string text = "<b>Connected Rooms Generator</b>\n" + level + "\n\n" +
                          "<color=#9fd8ff>Space</color>  next grid type     <color=#9fd8ff>R</color>  same level again\n" +
                          "<color=#9fd8ff>Drag</color>  orbit     <color=#9fd8ff>Scroll</color>  zoom";
            GUI.Box(new Rect(16, 16, 430, 128), text, panelStyle);
        }

        private static string Name(GridType type)
        {
            switch (type)
            {
                case GridType.OctagonSquare:
                    return "Octagon + Square";
                case GridType.Mixed:
                    return "Mixed (every grid type)";
                default:
                    return type.ToString();
            }
        }

        private void OnDestroy()
        {
            current?.Destroy();
            leaving?.Destroy();
        }
    }
}
