using Tidewater.Schemes;
using UnityEngine;

namespace Tidewater
{
    public class TradeBoard : MonoBehaviour
    {
        /// <summary>What a port buys first when a ship docks.</summary>
        public static readonly string[] OpeningDemand =
        {
            CargoScheme.Ids.CARGO_GRAIN,
            CargoScheme.Ids.CARGO_TIMBER,
            CargoScheme.Ids.CARGO_SPICE,
        };

        public int PriceOf(string cargoId, int units)
        {
            return CargoScheme.GetEntry(cargoId).Try(out var cargo) ? cargo.BasePrice * units : 0;
        }
    }
}
