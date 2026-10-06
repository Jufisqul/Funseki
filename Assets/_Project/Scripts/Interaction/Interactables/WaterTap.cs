using System.Collections;
using UnityEngine;

namespace Funseki.Interaction
{
    // Кранчик с водой (GDD 5.9): E opens / closes the water. The very first opening in the game runs brown for a while,
    // then clear, and the hero says the first-time line («Дерьмо»). Later openings are silent, except every
    // milestoneEvery-th one («Да, всё ещё вода»). Saved: open or closed, the brown water already seen, number of openings.
    public class WaterTap : InteractableObject
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("The stream; switched on while the tap is open")]
        [SerializeField] Renderer water;

        WaterTapData D => (WaterTapData)data;
        MaterialPropertyBlock block;
        Coroutine colorRoutine;

        bool IsOpen => Flags.GetFlag(Key("open"));
        public override string Prompt => IsOpen ? D.closePrompt : D.openPrompt;

        protected override void RestoreState()
        {
            SetWater(IsOpen);
            SetColor(D.cleanColor);
        }

        protected override void OnUsed(GameObject hero, bool first, GameObject witness)
        {
            if (IsOpen)
            {
                Flags.SetFlag(Key("open"), false);
                SetWater(false);
                Report(hero, "tap_closed");
                return;
            }

            Flags.SetFlag(Key("open"));
            int opens = Flags.AddCounter(Key("opens"));
            SetWater(true);
            Report(hero, "tap_opened");

            if (!Flags.GetFlag(Key("dirty_seen")))
            {
                Flags.SetFlag(Key("dirty_seen"));
                if (colorRoutine != null) StopCoroutine(colorRoutine);
                colorRoutine = StartCoroutine(DirtyThenClean());
                SayUsualLine(hero, true, witness);
            }
            else if (D.milestoneEvery > 0 && opens % D.milestoneEvery == 0)
                Say(hero, InteractableData.Pick(D.milestoneLines));
            else
                SayUsualLine(hero, false, witness);
        }

        IEnumerator DirtyThenClean()
        {
            SetColor(D.dirtyColor);
            yield return new WaitForSeconds(D.dirtyDuration);
            for (float t = 0f; t < D.clearingTime; t += Time.deltaTime)
            {
                SetColor(Color.Lerp(D.dirtyColor, D.cleanColor, t / D.clearingTime));
                yield return null;
            }
            SetColor(D.cleanColor);
            colorRoutine = null;
        }

        void SetWater(bool on)
        {
            if (water != null) water.gameObject.SetActive(on);
        }

        void SetColor(Color c)
        {
            if (water == null) return;
            block ??= new MaterialPropertyBlock();
            water.GetPropertyBlock(block);
            block.SetColor(BaseColor, c);
            block.SetColor(ColorId, c);
            water.SetPropertyBlock(block);
        }
    }
}
