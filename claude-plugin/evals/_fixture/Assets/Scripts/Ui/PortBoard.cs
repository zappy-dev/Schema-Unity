using Tidewater.Schemes;
using UnityEngine;

namespace Tidewater
{
    /// <summary>The harbor screen: one label per port.</summary>
    public class PortBoard : MonoBehaviour
    {
        [SerializeField] PortLabel labelPrefab;

        void Start()
        {
            if (!PortsScheme.Get().Try(out var ports))
            {
                Debug.LogError("PortBoard: ports table not loaded");
                return;
            }
            foreach (var port in ports.GetEntries())
            {
                Instantiate(labelPrefab, transform).Show(port);
            }
        }
    }
}
