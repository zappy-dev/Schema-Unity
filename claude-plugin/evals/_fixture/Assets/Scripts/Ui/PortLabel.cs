using Tidewater.Schemes;
using UnityEngine;
using UnityEngine.UI;

namespace Tidewater
{
    public class PortLabel : MonoBehaviour
    {
        [SerializeField] Text label;

        public void Show(PortsEntry port)
        {
            label.text = $"{port.Name} ({port.Fee}c)";
            label.color = ColorUtility.TryParseHtmlString(port.Tint, out var tint) ? tint : Color.white;
        }
    }
}
