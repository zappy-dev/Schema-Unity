using UnityEngine;

namespace Tidewater
{
    public class Ship : MonoBehaviour
    {
        // Every ship burns fuel at the same rate for now.
        const float FuelBurnPerSecond = 1f;

        public string DisplayName { get; private set; }
        public float Speed { get; private set; }
        public int Capacity { get; private set; }
        public float Fuel { get; private set; } = 100f;

        public void Configure(string displayName, float speed, int capacity)
        {
            DisplayName = displayName;
            Speed = speed;
            Capacity = capacity;
        }

        void Update()
        {
            Fuel = Mathf.Max(0f, Fuel - FuelBurnPerSecond * Time.deltaTime);
            transform.Translate(Vector3.right * (Speed * Time.deltaTime));
        }
    }
}
