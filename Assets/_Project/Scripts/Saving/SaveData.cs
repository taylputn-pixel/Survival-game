using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrandmasHouse.Saving
{
    // Everything in one saved game, written as JSON.
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public List<SystemEntry> systems = new List<SystemEntry>();
        public List<ItemRecord> items = new List<ItemRecord>();
        public List<PlayerRecord> players = new List<PlayerRecord>();
    }

    // One player: where they stood and how they were doing. Kept by the
    // player's key (their Steam ID, or "host"), so friends pick up where
    // they left off when they join again.
    [Serializable]
    public class PlayerRecord
    {
        public string key;
        public Vector3 position;
        public float yaw;
        public string needs;
    }

    // The saved state of one system (the clock, a garden bed, a station).
    [Serializable]
    public class SystemEntry
    {
        public string key;
        public string json;
    }

    // One item and where it was: loose in the world, inside another item
    // (a basket, a jar), or on a spot in the scene (a shelf, a towel, a vine).
    [Serializable]
    public class ItemRecord
    {
        public string key;
        public int containerIndex = -1;
        public int parentItem = -1;
        public string parentPath;
        public Vector3 position;
        public Quaternion rotation;
        public bool kinematic;
        public bool colliders;
        public List<string> states = new List<string>();
    }

    // Something in the scene that saves its own state.
    public interface ISaveable
    {
        string SaveKey { get; }
        string Capture();
        void Restore(string json);
    }

    // Extra state on an item (how full a jar is, water in the can).
    public interface IItemState
    {
        string Capture();
        void Restore(string json);
    }

    // Called once every item is back in place, so stations can pick up
    // what's sitting on them again.
    public interface IAfterLoad
    {
        void AfterLoad();
    }
}
