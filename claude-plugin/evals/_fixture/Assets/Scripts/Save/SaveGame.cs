using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tidewater
{
    [System.Serializable]
    public class SaveData
    {
        public int Coins;
        public string CurrentPortId;
        public Dictionary<string, int> HoldByCargoId = new Dictionary<string, int>();
        public List<List<string>> VisitedRoutes = new List<List<string>>();
    }

    public static class SaveGame
    {
        static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, $"slot{slot}.json");

        public static void Write(int slot, SaveData data)
        {
            File.WriteAllText(PathFor(slot), JsonUtility.ToJson(data));
        }

        public static SaveData Read(int slot)
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot)));
        }
    }
}
