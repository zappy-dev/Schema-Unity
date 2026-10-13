using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tidewater
{
    /// <summary>Pushes every ship that is under sail along the wind.</summary>
    public class WindModel : MonoBehaviour
    {
        [SerializeField] Vector2 wind = new Vector2(1f, 0.2f);
        readonly List<Ship> ships = new List<Ship>();

        public void Register(Ship ship) => ships.Add(ship);

        void Update()
        {
            var sailing = ships.Where(s => s.Fuel > 0f).Select(s => s.transform).ToList();
            foreach (var t in sailing)
            {
                t.Translate(wind * Time.deltaTime);
            }
        }
    }
}
