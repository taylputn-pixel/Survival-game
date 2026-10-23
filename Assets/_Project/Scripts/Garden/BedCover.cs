using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // The frost cloth kept rolled at the end of a bed. Pull it over on a
    // frosty night and the plants live through a light frost (not a hard
    // freeze); roll it back in the morning, since nothing under it can be
    // planted, watered or picked.
    public class BedCover : MonoBehaviour, IInteractable, ISharedUse
    {
        [SerializeField] GardenBed bed;
        [Tooltip("The cloth rolled up at the end of the bed.")]
        [SerializeField] GameObject rolled;
        [Tooltip("The cloth drawn over the bed.")]
        [SerializeField] GameObject drawn;

        void OnEnable()
        {
            bed.CoverChanged += Show;
            Show();
        }

        void OnDisable() => bed.CoverChanged -= Show;

        void Show()
        {
            rolled.SetActive(!bed.Covered);
            drawn.SetActive(bed.Covered);
        }

        public bool CanInteract(PlayerHands hands) => hands.IsEmpty;

        public string PromptVerb(PlayerHands hands) => bed.Covered ? "Roll back the frost cloth" : "Pull the frost cloth over";

        public void Interact(PlayerHands hands)
        {
            bed.Cover(!bed.Covered);
            Audio.Sfx.Play(Audio.SoundId.Paper, transform.position, 0.6f, 0.1f);
        }
    }
}
