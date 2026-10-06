using System;
using UnityEngine;

namespace Funseki.Interaction
{
    // Стеллаж с буклетами: the booklet's pages. The first-time / repeat lines are said when the booklet closes.
    [CreateAssetMenu(fileName = "Interactable_Booklet", menuName = "Funseki/Interaction/Booklet")]
    public class BookletData : InspectData
    {
        [Serializable]
        public class Page
        {
            public string title;
            [Tooltip("Picture of the page; may be empty")]
            public Texture image;
            [TextArea(3, 8)] public string text;
        }

        public Page[] pages;

        [Header("Booklet screen")]
        [Tooltip("{0} current page, {1} page count")]
        public string pageFormat = "{0} / {1}";
        public string previousLabel = "◄ A";
        public string nextLabel = "D ►";
        public string closeLabel = "Закрыть";
        public Vector2 panelSize = new(1100f, 820f);
        public Color paperColor = new(0.96f, 0.93f, 0.85f);
        public Color inkColor = new(0.15f, 0.12f, 0.1f);
        public float titleFontSize = 44f;
        public float bodyFontSize = 26f;
    }
}
