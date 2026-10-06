using System.Collections.Generic;
using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.Inventory
{
    // Row of item slots at the bottom of the screen with the selected item's name above it.
    // Appears on scrolling or picking up and fades out after InventorySettings.panelHideDelay without input.
    // Built in code from InventorySettings; sits next to InventoryService.
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] InventorySettings settings;

        CanvasGroup group;
        RectTransform row;
        TextMeshProUGUI nameLabel;
        readonly List<(Image back, Image icon, TextMeshProUGUI label)> slots = new();
        float hideAt = -1f;

        void Awake() => Build();

        void OnEnable()
        {
            GameEvents.OnItemCycled += OnInventoryTouched;
            GameEvents.OnItemAdded += OnInventoryTouched;
            GameEvents.OnItemRemoved += OnInventoryTouched;
            GameEvents.OnSelectedItemChanged += OnInventoryTouched;
        }

        void OnDisable()
        {
            GameEvents.OnItemCycled -= OnInventoryTouched;
            GameEvents.OnItemAdded -= OnInventoryTouched;
            GameEvents.OnItemRemoved -= OnInventoryTouched;
            GameEvents.OnSelectedItemChanged -= OnInventoryTouched;
        }

        void OnInventoryTouched(ItemData _)
        {
            Refresh();
            hideAt = Time.unscaledTime + settings.panelHideDelay;
        }

        void Update()
        {
            float target = Time.unscaledTime < hideAt ? 1f : 0f;
            float speed = settings.panelFadeTime > 0f ? Time.unscaledDeltaTime / settings.panelFadeTime : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, speed);
        }

        void Refresh()
        {
            if (!ServiceLocator.TryGet<Inventory>(out var inv)) return;
            for (int i = 0; i < slots.Count; i++)
            {
                var (back, icon, label) = slots[i];
                bool filled = i < inv.Count;
                back.gameObject.SetActive(filled);
                if (!filled) continue;
                var item = inv.Items[i];
                back.color = i == inv.SelectedIndex ? settings.selectedSlotColor : settings.slotColor;
                icon.sprite = item.icon;
                icon.enabled = item.icon != null;
                // No icon yet (greybox): the slot shows the item name instead.
                label.text = item.icon != null ? "" : item.displayName;
            }
            nameLabel.text = inv.Selected != null ? inv.Selected.displayName : "";
        }

        void Build()
        {
            var canvasGo = new GameObject("InventoryPanel", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            row = new GameObject("Slots", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(canvasGo.transform, false);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, settings.panelBottomOffset);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = settings.slotSpacing;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < settings.capacity; i++)
            {
                var back = NewImage("Slot" + i, row, settings.slotSize);
                var icon = NewImage("Icon", back.rectTransform, settings.slotSize * 0.8f);
                icon.preserveAspect = true;
                var label = NewText("Name", back.rectTransform, settings.slotFontSize);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.textWrappingMode = TextWrappingModes.Normal;
                back.gameObject.SetActive(false);
                slots.Add((back, icon, label));
            }

            nameLabel = NewText("SelectedName", canvasGo.transform, settings.nameFontSize);
            var rt = nameLabel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(800f, settings.nameFontSize * 1.5f);
            rt.anchoredPosition = new Vector2(0f, settings.panelBottomOffset + settings.slotSize + settings.slotSpacing);
        }

        static Image NewImage(string name, Transform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            return img;
        }

        TextMeshProUGUI NewText(string name, Transform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (settings.font != null) t.font = settings.font;
            t.fontSize = size;
            t.color = settings.textColor;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }
    }
}
