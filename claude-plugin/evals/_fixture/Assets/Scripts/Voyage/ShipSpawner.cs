using System.Collections.Generic;
using Tidewater.Schemes;
using UnityEngine;

namespace Tidewater
{
    public class ShipSpawner : MonoBehaviour
    {
        [SerializeField] GameObject shipPrefab;

        public Ship Spawn(string shipId, Vector3 at)
        {
            var row = ShipsScheme.GetEntry(shipId).Result;
            var ship = Instantiate(shipPrefab, at, Quaternion.identity).GetComponent<Ship>();
            ship.Configure(row.Name, row.Speed, row.Capacity);
            return ship;
        }

        public void SpawnFleet(IEnumerable<string> shipIds, Vector3 origin)
        {
            var offset = 0f;
            foreach (var id in shipIds)
            {
                Spawn(id, origin + Vector3.up * offset);
                offset += 1.5f;
            }
        }
    }
}
