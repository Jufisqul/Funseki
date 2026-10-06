using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.Interaction
{
    // World-space "E — <Prompt>" label above the current target, always facing the camera.
    // Built in code from InteractionSettings so it needs no prefab.
    public class InteractionPrompt
    {
        readonly InteractionSettings settings;
        GameObject root;
        TextMeshProUGUI label;
        Image background;

        public InteractionPrompt(InteractionSettings settings) => this.settings = settings;

        public void Show(GameObject target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text))
            {
                if (root != null) root.SetActive(false);
                return;
            }
            if (root == null) Build();

            label.text = text;
            label.fontSize = settings.fontSize;
            label.color = settings.textColor;
            background.color = settings.backgroundColor;

            Bounds b = BoundsOf(target);
            Vector3 pos = new(b.center.x, b.max.y + settings.promptOffset, b.center.z);
            var cam = Camera.main;
            if (cam != null)
            {
                // Pull the label toward the camera so a door or a board set into a wall doesn.t swallow it.
                Vector3 toCam = cam.transform.position - pos;
                pos += toCam.normalized * Mathf.Min(settings.promptPullToCamera, toCam.magnitude * 0.5f);
                root.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
            }
            root.transform.position = pos;
            root.SetActive(true);
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }

        static Bounds BoundsOf(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                return b;
            }
            var col = target.GetComponentInChildren<Collider>();
            return col != null ? col.bounds : new Bounds(target.transform.position, Vector3.zero);
        }

        void Build()
        {
            root = new GameObject("InteractionPrompt", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            root.transform.localScale = Vector3.one * 0.01f;

            background = root.AddComponent<Image>();
            background.raycastTarget = false;
            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = root.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = new Vector4(6f, 3f, 6f, 3f);
            label.raycastTarget = false;
            if (settings.font != null) label.font = settings.font;

            if (settings.promptOnTop)
            {
                // Draw over walls and props: UI shaders read their depth test from unity_GUIZTestMode.
                background.material = OnTop(new Material(Image.defaultGraphicMaterial));
                label.fontMaterial = OnTop(label.fontMaterial);
            }
        }

        static Material OnTop(Material m)
        {
            m.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
            return m;
        }
    }
}
