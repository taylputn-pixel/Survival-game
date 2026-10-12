using System;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Carrying
{
    // A carryable thing that holds other items: a harvest basket, an egg
    // basket, a jar crate. Contents ride along inside it and stay put when
    // it's set down.
    [RequireComponent(typeof(Pickup))]
    public class Container : MonoBehaviour
    {
        [SerializeField] int capacity = 20;
        [SerializeField] ItemCategory[] accepts = { ItemCategory.Produce };

        [Header("Inside space, local to the container")]
        [Tooltip("The center of the inside floor, where the bottom layer of items rests.")]
        [SerializeField] Vector3 floorCenter = new Vector3(0f, 0.02f, 0f);
        [SerializeField] Vector2 floorSize = new Vector2(0.32f, 0.22f);
        [SerializeField] int columns = 3;
        [SerializeField] int rows = 2;
        [SerializeField] float layerHeight = 0.05f;

        public int Count { get; private set; }
        public bool IsFull => Count >= capacity;

        Pickup self;
        Pickup[] slots;
        bool isHeld;

        void Awake()
        {
            self = GetComponent<Pickup>();
            slots = new Pickup[capacity];
            self.HeldChanged += OnHeldChanged;
        }

        public bool Accepts(Pickup item)
        {
            return !IsFull
                && item != self
                && item.OwnContainer == null
                && item.InContainer != this
                && Array.IndexOf(accepts, item.Category) >= 0;
        }

        public void Add(Pickup item, float glideSeconds)
        {
            if (item.InContainer != null)
            {
                item.InContainer.Remove(item);
            }

            int slot = Array.IndexOf(slots, null);
            slots[slot] = item;
            Count++;

            // A different turn for each slot keeps the pile looking natural.
            Quaternion turn = Quaternion.Euler(0f, (slot * 137.5f) % 360f, 0f);
            Vector3 position = SlotPosition(slot) + Vector3.up * item.RestHalfHeight;
            item.MoveInto(this, position, turn * item.RestTilt, glideSeconds, collidersOn: !isHeld);
        }

        public void Remove(Pickup item)
        {
            int slot = Array.IndexOf(slots, item);
            if (slot < 0)
            {
                return;
            }
            slots[slot] = null;
            Count--;
            item.LeaveContainer();
        }

        Vector3 SlotPosition(int slot)
        {
            int perLayer = columns * rows;
            int layer = slot / perLayer;
            int indexInLayer = slot % perLayer;
            float x = ((indexInLayer % columns) + 0.5f) / columns - 0.5f;
            float z = ((indexInLayer / columns) + 0.5f) / rows - 0.5f;
            return floorCenter + new Vector3(x * floorSize.x, layer * layerHeight, z * floorSize.y);
        }

        void OnHeldChanged(bool held)
        {
            // While carried, the contents can't be targeted or bump into anything.
            isHeld = held;
            foreach (var item in slots)
            {
                if (item != null)
                {
                    item.SetCollidersEnabled(!held);
                }
            }
        }
    }
}
