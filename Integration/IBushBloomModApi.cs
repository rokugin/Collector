using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Collector.Integration;

public interface IBushBloomModApi {

    /// <summary>
    /// Returns an array of (item_id, first_day, last_day) for all possible active blooming
    /// schedules on the given season and day, optionally within the given year and/or location.
    /// </summary>
    public (string, WorldDate, WorldDate)[] GetActiveSchedules(string season, int dayofMonth, int? year = null, GameLocation? location = null, Vector2? tile = null);

    /// <summary>
    /// Returns an array of (item_id, first_day, last_day) for all blooming schedules.
    /// </summary>
    public (string, WorldDate, WorldDate)[] GetAllSchedules();

    /// <summary>
    /// Clear and reparse all schedules.
    /// </summary>
    public void ReloadSchedules();

    /// <summary>
    /// Specifies whether BBM successfully parsed all schedules.
    /// </summary>
    public bool IsReady();

    /// <summary>
    /// Performs the general operations of the Bush.shake() function without all the player, debris,
    /// and UI logic. Namely, this will return an item ID if the bush is in bloom and mark the bush
    /// as no longer blooming. You must create the item and handle any operations needed for it.
    /// </summary>
    public string FakeShake(Bush bush);

}