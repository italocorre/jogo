using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MarAberto
{
    [Serializable] public class SlotSave { public int type; public int count; }

    [Serializable]
    public class PieceSave
    {
        public int id, kind, raft;   // raft = -1 em terra
        public Vector3 pos;          // local na jangada, ou no mundo
        public Quaternion rot;
        public string owner;
        public float fuel;
        public List<SlotSave> chest = new List<SlotSave>();
    }

    [Serializable]
    public class RaftSave
    {
        public int id;
        public string name;
        public Vector3 pos;
        public Quaternion rot;
        public bool anchored;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public string name;
        public bool hasPos;
        public Vector3 pos;
        public Vector3 look;
        public bool hasBed;
        public Vector3 bedPos, bedLook;
        public int startIsland = -1;
        public List<SlotSave> inv = new List<SlotSave>();
        public int selected;
        public List<string> unlocked = new List<string>();
        public int backpack, oxygen;
        public float health = 100f;
    }

    [Serializable]
    public class WorldSave
    {
        public int version = 1;
        public float timeOfDay = 0.3f;
        public int nextPiece = 1, nextRaft = 1;
        public List<RaftSave> rafts = new List<RaftSave>();
        public List<PieceSave> pieces = new List<PieceSave>();
        public List<PlayerSaveData> players = new List<PlayerSaveData>();

        public static string FilePath => Path.Combine(Application.persistentDataPath, "mundo.json");

        public static WorldSave Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JsonUtility.FromJson<WorldSave>(File.ReadAllText(FilePath));
            }
            catch (Exception e) { Debug.LogWarning("Falha ao carregar o mundo: " + e.Message); return null; }
        }

        public void Write()
        {
            try
            {
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(this));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e) { Debug.LogWarning("Falha ao salvar o mundo: " + e.Message); }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }

        public PlayerSaveData FindPlayer(string name)
        {
            foreach (var p in players) if (p.name == name) return p;
            return null;
        }

        public void StorePlayer(PlayerSaveData d)
        {
            players.RemoveAll(p => p.name == d.name);
            players.Add(d);
        }

        public static List<SlotSave> Slots(Inventory inv)
        {
            var l = new List<SlotSave>();
            foreach (var s in inv.Slots) l.Add(new SlotSave { type = (int)s.Type, count = s.Count });
            return l;
        }

        public static List<ItemStack> Stacks(List<SlotSave> l)
        {
            var r = new List<ItemStack>();
            if (l != null) foreach (var s in l) r.Add(new ItemStack((ItemType)s.type, s.count));
            return r;
        }
    }
}
