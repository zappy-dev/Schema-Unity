using Schema.Runtime;
using UnityEngine;

namespace Tidewater
{
    /// <summary>Loads the game's data before anything else reads it.</summary>
    public class Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            var init = SchemaRuntime.Initialize();
            if (init.Failed)
            {
                Debug.LogError(init.Message);
            }
        }
    }
}
