using System.Collections.Generic;
using Tidewater.Schemes;
using UnityEngine;

namespace Tidewater
{
    public static class VoyagePlanner
    {
        /// <summary>The ports a ship visits from <paramref name="startPortId"/> to the end of the route.</summary>
        public static List<string> PlanRoute(string startPortId)
        {
            var route = new List<string>();
            var id = startPortId;
            while (!string.IsNullOrEmpty(id) && route.Count < 64)
            {
                if (!PortsScheme.GetEntry(id).Try(out var port))
                {
                    Debug.LogError($"Voyage stalls: no port '{id}'");
                    break;
                }
                route.Add(port.ID);
                id = port.NextPort;
            }
            return route;
        }
    }
}
