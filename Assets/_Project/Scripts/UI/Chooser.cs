using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GrandmasHouse.UI
{
    // A menu value picked from a short list ("Off / On", "Auto / Xbox / ...").
    // Left and right change it on a controller or keyboard; clicking steps
    // to the next one with the mouse.
    public class Chooser : Selectable, IPointerClickHandler, ISubmitHandler
    {
        Text label;
        string[] options;
        Action<int> onChange;

        public int Index { get; private set; }

        public void Setup(Text text, string[] choices, int index, Action<int> changed)
        {
            label = text;
            options = choices;
            onChange = changed;
            Index = index;
            Show();
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left)
            {
                Step(-1);
            }
            else if (eventData.moveDir == MoveDirection.Right)
            {
                Step(1);
            }
            else
            {
                base.OnMove(eventData);
            }
        }

        public void OnPointerClick(PointerEventData eventData) => Step(1);

        public void OnSubmit(BaseEventData eventData) => Step(1);

        void Step(int direction)
        {
            Index = (Index + direction + options.Length) % options.Length;
            Show();
            onChange(Index);
        }

        void Show() => label.text = "‹   " + options[Index] + "   ›";
    }
}
