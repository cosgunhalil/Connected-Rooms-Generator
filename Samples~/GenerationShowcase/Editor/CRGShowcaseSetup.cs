using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CRG.Samples.GenerationShowcase.Editor
{
    // CRG > Samples > Create Generation Showcase Scene: makes the two materials from the sample's shader graphs (saved
    // next to the sample, so player builds include them) and a scene with a camera, a light and the showcase, saved
    // next to the sample and opened.
    internal static class CRGShowcaseSetup
    {
        private const string RevealShader = "Shader Graphs/CRG Showcase Reveal";
        private const string BlueprintShader = "Shader Graphs/CRG Showcase Blueprint";

        [MenuItem("CRG/Samples/Create Generation Showcase Scene")]
        private static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            string folder = SampleFolder();
            Material reveal = MaterialAsset(folder, "CRG-Showcase-Reveal.mat", RevealShader);
            Material blueprint = MaterialAsset(folder, "CRG-Showcase-Blueprint.mat", BlueprintShader);
            if (reveal == null || blueprint == null)
            {
                return;
            }

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            Camera view = cameraObject.GetComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.allowHDR = true;
            view.farClipPlane = 500f;
            view.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            cameraObject.AddComponent<CRGShowcaseOrbitCamera>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 30f, -30f), Quaternion.Euler(45f, 0f, 0f));

            Light key = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.92f, 0.82f);
            key.intensity = 1.1f;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            CRGGenerationShowcase showcase = new GameObject("Generation Showcase").AddComponent<CRGGenerationShowcase>();
            showcase.revealMaterial = reveal;
            showcase.blueprintMaterial = blueprint;

            string scenePath = AssetDatabase.GenerateUniqueAssetPath(folder + "/CRG-GenerationShowcase.unity");
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = showcase.gameObject;
            Debug.Log("CRG showcase: created " + scenePath + ". Press Play, then Space.", showcase);
        }

        // The folder this sample was imported into (two levels up from this script: Editor/ inside the sample).
        private static string SampleFolder()
        {
            string[] guids = AssetDatabase.FindAssets("CRGShowcaseSetup t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/Editor/CRGShowcaseSetup.cs"))
                {
                    return Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace('\\', '/');
                }
            }
            return "Assets";
        }

        private static Material MaterialAsset(string folder, string fileName, string shaderName)
        {
            string path = folder + "/" + fileName;
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("CRG Generation Showcase",
                    "The shader '" + shaderName + "' wasn't found. The sample needs the Universal Render Pipeline and Shader Graph; " +
                    "check the Console for shader graph import errors.", "OK");
                return null;
            }
            Material material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
