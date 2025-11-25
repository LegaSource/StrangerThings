using BepInEx.Configuration;

namespace StrangerThings.Managers;

public class ConfigManager
{
    // Demogorgon
    public static ConfigEntry<int> demogorgonRarity;
    public static ConfigEntry<int> demogorgonDamage;
    // Vecna
    public static ConfigEntry<int> vecnaRarity;
    public static ConfigEntry<int> vecnaDamage;
    // Upside Down
    public static ConfigEntry<string> visibilityStateInclusions;

    public static void Load()
    {
        // Demogorgon
        demogorgonRarity = StrangerThings.configFile.Bind(Constants.DEMOGORGON, "Rarity", 20, $"{Constants.DEMOGORGON} base rarity.");
        demogorgonDamage = StrangerThings.configFile.Bind(Constants.DEMOGORGON, "Damage", 40, $"{Constants.DEMOGORGON} damage.");
        // Vecna
        vecnaRarity = StrangerThings.configFile.Bind(Constants.VECNA, "Rarity", 20, $"{Constants.VECNA} base rarity.");
        vecnaDamage = StrangerThings.configFile.Bind(Constants.VECNA, "Damage", 40, $"{Constants.VECNA} damage.");
        // Upside Down
        visibilityStateInclusions = StrangerThings.configFile.Bind(Constants.UPSIDE_DOWN, "Visibility state whitelist", "SP_Snowman,SP_SnowPile,LK_Lantern,SawBoxExplosive,ChainEscape", "Additional list of Network Objects whose visibility (visible/invisible) will be updated when switching between dimensions.");
    }
}
