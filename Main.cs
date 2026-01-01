using Verse;
using HarmonyLib;

namespace CompanionshipBarracks
{
    [StaticConstructorOnStartup]
    public static class Main
    {
        static Main()
        {
            var harmony = new Harmony("shikiyuzu.companionship_barracks");
            harmony.PatchAll();
            Log.Message("[陪伴营房] 补丁已应用。");
        }
    }
}