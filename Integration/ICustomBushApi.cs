using StardewValley;
using StardewValley.TerrainFeatures;

namespace Collector.Integration;

public interface ICustomBushApi {

    public bool IsCustomBush(Bush bush);

    /// <summary>Tries to get the custom bush model associated with the given bush.</summary>
    /// <param name="bush">The bush.</param>
    /// <param name="customBush">
    /// When this method returns, contains the custom bush associated with the given bush, if found;
    /// otherwise, it contains null.
    /// </param>
    /// <returns><c>true</c> if the custom bush associated with the given bush is found; otherwise, <c>false</c>.</returns>
    public bool TryGetCustomBush(Bush bush, out ICustomBush? customBush);

    public bool TryGetShakeOffItem(Bush bush, out Item? item);

}