using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using CompanionshipBarracks.Barracks;

namespace CompanionshipBarracks;

internal static class MemoryReplacement
{
    internal static readonly Dictionary<BarracksThought, ThoughtDef> BarracksDefs = new();
    private static readonly HashSet<string> Warned = new();

    internal static void Initialize()
    {
        foreach (BarracksThought choice in Enum.GetValues(typeof(BarracksThought)))
        {
            if (choice == BarracksThought.KeepVanilla) continue;
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail("SleptInBarracks_" + choice);
            BarracksDefs[choice] = def;
            if (def == null || !def.IsMemory || def.stages?.Count != ThoughtDefOf.SleptInBarracks.stages.Count)
                Warn("barracks-" + choice, "营房定义或阶段不完整：" + choice + "；无效候选将保留原版结果。");
        }
        foreach (string name in new[] { "MakeLoveWithSister", "MakeLoveWithBrother" })
            if (!Valid(DefDatabase<ThoughtDef>.GetNamedSilentFail(name), 0))
                Warn(name, "事件定义不可用：" + name + "；将保留原版结果。");
    }

    internal static bool Valid(ThoughtDef def, int stage) => def != null && def.IsMemory && def.stages != null
        && stage >= 0 && stage < def.stages.Count && def.stages[stage] != null;

    internal static void CopyRuntime(Thought_Memory source, Thought_Memory target)
    {
        target.moodPowerFactor = source.moodPowerFactor;
        target.moodOffset = source.moodOffset;
        target.sourcePrecept = source.sourcePrecept;
        target.durationTicksOverride = source.durationTicksOverride;
    }

    internal static void Warn(string key, string text)
    {
        if (Warned.Add(key)) Log.Warning("[陪伴营房] " + text);
    }

    internal static void Trace(Pawn pawn, string text)
    {
        if (Prefs.DevMode) Log.Message("[陪伴营房] " + pawn + ": " + text);
    }
}
