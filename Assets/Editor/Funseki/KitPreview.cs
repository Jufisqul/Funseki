using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Funseki.School.EditorTools
{
    // Renders every PF_Kit_* prefab in a row into a PNG, inside an isolated preview scene
    // (open scenes are not touched). Handy to eyeball the kit after regenerating it.
    public static class KitPreview
    {
        public const string OutputPath = KitGenerator.Root + "Docs/Kit_Preview.png";

        [MenuItem("Tools/Funseki/Render Kit Preview")]
        public static void Render()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var guids = AssetDatabase.FindAssets("PF_Kit_ t:Prefab", new[] { KitGenerator.PrefabDir.TrimEnd('/') });
                const int perRow = 9;
                for (int i = 0; i < guids.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    go.transform.position = new Vector3((i % perRow) * 4.5f, 0f, (i / perRow) * 5f);
                    go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                }
                int rows = (guids.Length + perRow - 1) / perRow;

                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                light.transform.rotation = Quaternion.Euler(50f, 150f, 0f);
                SceneManager_Move(light.gameObject, scene);

                var cam = new GameObject("PreviewCamera").AddComponent<Camera>();
                SceneManager_Move(cam.gameObject, scene);
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.65f, 0.75f);
                cam.fieldOfView = 40f;
                var center = new Vector3((perRow - 1) * 4.5f * 0.5f - 1f, 0.5f, (rows - 1) * 5f * 0.5f);
                cam.transform.position = center + new Vector3(0f, 22f, -32f);
                cam.transform.LookAt(center);

                SaveCameraPng(cam, OutputPath, 1800, 1100);
                Debug.Log("[Funseki] Kit preview saved: " + OutputPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        public static void SaveCameraPng(Camera cam, string assetPath, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            AssetDatabase.ImportAsset(assetPath);
        }
    }
}
