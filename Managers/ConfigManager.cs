using BepInEx.Configuration;

namespace StrangerThings.Managers;

public class ConfigManager
{
    // Demogorgon
    public static ConfigEntry<int> demogorgonRarity;
    // Vecna
    public static ConfigEntry<int> vecnaRarity;
    // Crustapikan
    public static ConfigEntry<int> crustapikanRarity;
    // Crustapikan Larvae
    public static ConfigEntry<int> crustapikanLarvaeRarity;
    // Upside Down
    public static ConfigEntry<string> visibilityStateInclusions;

    public static void Load()
    {
        // Demogorgon
        demogorgonRarity = StrangerThings.configFile.Bind(Constants.DEMOGORGON, "Rarity", 20, $"{Constants.DEMOGORGON} base rarity.");
        // Vecna
        vecnaRarity = StrangerThings.configFile.Bind(Constants.VECNA, "Rarity", 20, $"{Constants.VECNA} base rarity.");
        // Crustapikan
        crustapikanRarity = StrangerThings.configFile.Bind(Constants.CRUSTAPIKAN, "Rarity", 20, $"{Constants.CRUSTAPIKAN} base rarity.");
        // Crustapikan Larvae
        crustapikanLarvaeRarity = StrangerThings.configFile.Bind(Constants.CRUSTAPIKAN_LARVAE, "Rarity", 20, $"{Constants.CRUSTAPIKAN_LARVAE} base rarity.");
        // Upside Down
        visibilityStateInclusions = StrangerThings.configFile.Bind(Constants.UPSIDE_DOWN, "Visibility state whitelist", "SP_Snowman,SP_SnowPile,LK_Lantern,SawBoxExplosive,ChainEscape", "Additional list of Network Objects whose visibility (visible/invisible) will be updated when switching between dimensions.");
    }
}
