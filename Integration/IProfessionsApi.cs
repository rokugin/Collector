using StardewValley;

namespace Collector.Integration;

public interface IProfessionsApi {

    int GetEcologistForageQuality(Farmer? farmer = null);

}