using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.Interaction
{
    // Стеллаж с буклетами (GDD 5.9): E opens the school booklet (pages with a picture and text from BookletData,
    // A / D or the buttons flip them). Closing it (Esc / E / «Закрыть») makes the hero say the line from GDD.
    public class BookletRack : InteractableObject
    {
        [SerializeField] InputActionAsset actions;

        BookletData D => (BookletData)data;
        InspectSession session;
        BookletView view;
        GameObject reader;
        bool readerFirst;
        GameObject readerWitness;

        void Awake() => session = new InspectSession(actions);

        protected override bool CanUse(GameObject hero) => session.CanOpen;

        protected override void OnUsed(GameObject hero, bool first, GameObject witness)
        {
            reader = hero;
            readerFirst = first;
            readerWitness = witness;
            session.Begin();
            if (view == null)
            {
                view = new BookletView(D);
                view.CloseClicked += Close;
            }
            view.Open();
            Report(hero, "booklet_opened");
        }

        void Update()
        {
            if (!session.Active) return;
            if (session.ClosePressed) { Close(); return; }
            int step = session.Step;
            if (step != 0) view.Flip(step);
        }

        void Close()
        {
            if (!session.Active) return;
            session.End();
            view.Hide();
            Report(reader, "booklet_closed");
            SayUsualLine(reader, readerFirst, readerWitness);
        }

        void OnDisable()
        {
            if (session == null || !session.Active) return;
            session.End();
            view?.Hide();
        }

        void OnDestroy() => view?.Destroy();
    }
}
