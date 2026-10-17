using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using CompanionshipBarracks.Barracks;
using CompanionshipBarracks.SiblingLovin;

namespace CompanionshipBarracks.GameTests;

[StaticConstructorOnStartup]
public static class GameTests
{
    private static readonly List<string> Results = new();

    static GameTests()
    {
        if (GenCommandLine.CommandLineArgPassed("cb-tests"))
            LongEventHandler.ExecuteWhenFinished(() => LongEventHandler.QueueLongEvent(Run, "GeneratingMap", true, null));
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Results.Add("PASS " + name);
        File.WriteAllLines(Path.Combine(GenFilePaths.SaveDataFolderPath, "cb-test-results.txt"), Results);
    }

    private static void Run()
    {
        int exitCode = 0;
        try
        {
            Check(DefDatabase<ThoughtDef>.GetNamedSilentFail("SleptInBarracks_OnlySiblings")?.stages.Count == 10,
                "XML loaded: ten barracks stages");
            Check(DefDatabase<ThoughtDef>.GetNamedSilentFail("MakeLoveWithSister")?.durationDays == 3,
                "XML loaded: event duration");
            var method = AccessTools.Method(typeof(Toils_LayDown), "ApplyBedThoughts",
                new[] { typeof(Pawn), typeof(Building_Bed) });
            Check(Harmony.GetPatchInfo(method)?.Owners.Contains("shikiyuzu.companionship_barracks") == true,
                "Barracks patch registered in the actual game runtime");
            var gain = AccessTools.Method(typeof(MemoryThoughtHandler), "TryGainMemory",
                new[] { typeof(Thought_Memory), typeof(Pawn) });
            Check(Harmony.GetPatchInfo(gain)?.Owners.Contains("shikiyuzu.companionship_barracks") == true,
                "Event patch registered in the actual game runtime");
            var cleanup = AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.RemovePositiveBedroomThoughts));
            Check(Harmony.GetPatchInfo(cleanup)?.Owners.Contains("shikiyuzu.companionship_barracks") == true,
                "Positive bedroom cleanup patch registered");
            Check(AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Companionship Barracks") == 1,
                "Exactly one version of the mod assembly loaded");
            var content = LoadedModManager.RunningMods.Single(m => m.PackageIdPlayerFacing.ToLowerInvariant() == "shikiyuzu.zompanionship_barracks");
            Check(Path.GetFullPath(content.RootDir) == Path.GetFullPath(Path.Combine(GenFilePaths.ModsFolderPath, "Main")),
                "Mod loaded only from isolated directory: " + content.RootDir);
            var loadedAssembly = typeof(BedThoughtPatch).Assembly;
            Check(Path.GetFullPath(loadedAssembly.Location) == Path.GetFullPath(Path.Combine(content.RootDir, "1.7", "Assemblies", "Companionship Barracks.dll")),
                "Active DLL path: " + loadedAssembly.Location);
            Rules();
            Root_Play.SetupForQuickTestPlay();
            Find.GameInitData.mapSize = 100;
            Find.GameInitData.PrepForMapGen();
            Find.Scenario.PreMapGenerate();
            new Harmony("companionship.tests.startup").Patch(AccessTools.Method(typeof(Game), nameof(Game.InitNewGame)),
                postfix: new HarmonyMethod(typeof(GameTests), nameof(GameReady)));
            LongEventHandler.QueueLongEvent(null, "Play", "GeneratingMap", false, null);
            return;
        }
        catch (Exception ex)
        {
            Results.Add("FAIL " + ex);
            exitCode = 1;
        }
        File.WriteAllLines(Path.Combine(GenFilePaths.SaveDataFolderPath, "cb-test-results.txt"), Results);
        Environment.ExitCode = exitCode;
        Root.Shutdown();
    }

    private static void GameReady()
    {
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            int exitCode = 0;
            try
            {
                Check(Current.Game.CurrentMap != null, "Actual game world and map generated");
                Integration();
            }
            catch (Exception ex) { Results.Add("FAIL " + ex); exitCode = 1; }
            File.WriteAllLines(Path.Combine(GenFilePaths.SaveDataFolderPath, "cb-test-results.txt"), Results);
            Environment.ExitCode = exitCode;
            Root.Shutdown();
        });
    }

    private static void Rules()
    {
        var boundaries = new[] { (-21, Relationship.Enemy), (-20, Relationship.Enemy), (-19, Relationship.Neutral),
            (19, Relationship.Neutral), (20, Relationship.Friend), (59, Relationship.Friend), (60, Relationship.GoodFriend) };
        foreach (var (opinion, expected) in boundaries)
            Check(RelationshipRules.Classify(opinion, false, false, false, false) == expected, "Opinion boundary " + opinion);
        Check(RelationshipRules.Classify(19, true, true, true, true) == Relationship.Neutral, "Family requires opinion 20");
        Check(RelationshipRules.Classify(20, true, true, true, true) == Relationship.Sibling, "Family classification exclusive and sibling first");
        Check(RelationshipRules.Classify(20, true, false, true, true) == Relationship.Parent, "Parent precedes child");
        Check(RelationshipRules.Classify(20, true, false, false, true) == Relationship.Child, "Child classification");
        Check(RelationshipRules.Classify(20, true, false, false, false) == Relationship.OtherFamily, "Other blood family counted");
        var cases = new (BarracksThought expected, Relationship[] input)[]
        {
            (BarracksThought.KeepVanilla, []), (BarracksThought.KeepVanilla, [Relationship.Neutral]),
            (BarracksThought.OnlySiblings, [Relationship.Sibling]), (BarracksThought.OnlyParents, [Relationship.Parent]),
            (BarracksThought.OnlyChildren, [Relationship.Child]), (BarracksThought.OnlyRelatives, [Relationship.OtherFamily]),
            (BarracksThought.OnlyRelatives, [Relationship.Sibling, Relationship.Parent]),
            (BarracksThought.GoodFriends_NoEnemies, [Relationship.GoodFriend]),
            (BarracksThought.Friends_NoEnemies, [Relationship.Friend]),
            (BarracksThought.Friends_NoEnemies, [Relationship.Friend, Relationship.Friend, Relationship.Friend, Relationship.Neutral, Relationship.Neutral]),
            (BarracksThought.Friends_NoEnemies, [Relationship.Friend, Relationship.Friend, Relationship.GoodFriend, Relationship.Neutral, Relationship.Neutral]),
            (BarracksThought.Friends_NoEnemies, [Relationship.Sibling, Relationship.Friend, Relationship.Neutral]),
            (BarracksThought.GoodFriends_WithEnemies, [Relationship.GoodFriend, Relationship.Enemy]),
            (BarracksThought.Friends_WithEnemies, [Relationship.Friend, Relationship.Enemy]),
            (BarracksThought.SomeEnemies, [Relationship.Enemy, Relationship.Neutral, Relationship.Neutral]),
            (BarracksThought.ManyEnemies, [Relationship.Enemy, Relationship.Neutral]),
            (BarracksThought.KeepVanilla, [Relationship.Friend, Relationship.Neutral, Relationship.Neutral])
        };
        foreach (var (expected, input) in cases)
            Check(RelationshipRules.Select(input) == expected, "Rule " + string.Join(',', input) + " -> " + expected);
    }

    private static Pawn NewPawn(Map map, IntVec3 cell, Gender gender, int years)
    {
        var p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        p.gender = gender;
        p.ageTracker.AgeBiologicalTicks = years * 3600000L;
        foreach (var trait in p.story.traits.allTraits.ToArray()) p.story.traits.RemoveTrait(trait);
        p.needs.mood.thoughts.memories.Memories.Clear();
        GenSpawn.Spawn(p, cell, map);
        return p;
    }

    private static Building_Bed Bed(Map map, IntVec3 cell)
    {
        var bed = (Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
        bed.SetFaction(Faction.OfPlayer);
        return (Building_Bed)GenSpawn.Spawn(bed, cell, map);
    }

    private static ThoughtDef Reject;
    private static ThoughtDef Filtered;
    private static bool SkipBed;
    private static bool RejectMemory(Thought_Memory newThought) => newThought.def != Reject;
    private static bool SkipBedOriginal() => !SkipBed;
    private static void FilterThought(ThoughtDef def, ref bool __result) { if (def == Filtered) __result = false; }
    private static readonly System.Reflection.MethodInfo ApplyBed = AccessTools.Method(typeof(Toils_LayDown), "ApplyBedThoughts");
    private static void Settle(Pawn p, Building_Bed bed) => ApplyBed.Invoke(null, [p, bed]);
    private static bool IsBarracks(Thought_Memory m) => m.def.defName.StartsWith("SleptInBarracks_");

    private static void Integration()
    {
        Find.TickManager.DebugSetTicksGame(1000);
        var map = Current.Game.CurrentMap;
        var area = new CellRect(35, 35, 12, 12);
        foreach (var cell in area.Cells)
        {
            foreach (var thing in cell.GetThingList(map).ToArray())
                if (!(thing is Pawn)) thing.Destroy(DestroyMode.Vanish);
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
        }
        foreach (var cell in area.EdgeCells)
            GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), cell, map);
        var a = NewPawn(map, new IntVec3(38, 0, 38), Gender.Male, 30);
        var b = NewPawn(map, new IntVec3(42, 0, 38), Gender.Female, 20);
        var parent = NewPawn(map, new IntVec3(38, 0, 40), Gender.Female, 55);
        var father = NewPawn(map, new IntVec3(39, 0, 40), Gender.Male, 55);
        a.relations.ClearAllRelations(); b.relations.ClearAllRelations();
        a.SetMother(parent); b.SetMother(parent);
        a.SetFather(father); b.SetFather(father);
        var bed = Bed(map, new IntVec3(38, 0, 38));
        var otherBed = Bed(map, new IntVec3(42, 0, 38));
        a.ownership.ClaimBedIfNonMedical(bed);
        b.ownership.ClaimBedIfNonMedical(otherBed);
        map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
        Check(bed.GetRoom().Role == RoomRoleDefOf.Barracks, "Fixture room is an actual barracks");
        Check(BarracksContext.Residents(a, bed.GetRoom()).SequenceEqual([b]), "Bed owner collection excludes actor");
        otherBed.OwnersForReading.Add(b);
        try { Check(BarracksContext.Residents(a, bed.GetRoom()).Length == 1, "Duplicate bed owner is counted once"); }
        finally { otherBed.OwnersForReading.RemoveAt(otherBed.OwnersForReading.Count - 1); }
        // 固定关系好感，避免随机生成的外貌/背景影响边界测试。
        var opinion = (Thought_MemorySocial)ThoughtMaker.MakeThought(ThoughtDefOf.RescuedMe);
        opinion.opinionOffset = 100;
        a.needs.mood.thoughts.memories.TryGainMemory(opinion, b);
        Check(a.relations.OpinionOf(b) >= 60, "Fixture sibling opinion reaches special threshold");
        var memories = a.needs.mood.thoughts.memories;
        Check(BarracksContext.Select(a, bed.GetRoom()).thought == BarracksThought.BrotherWithSister, "Barracks male older mapping");
        a.ageTracker.AgeBiologicalTicks = 18 * 3600000L;
        Check(BarracksContext.Select(a, bed.GetRoom()).thought == BarracksThought.BrotherWithOlderSister, "Barracks male younger mapping");
        a.gender = Gender.Female; b.gender = Gender.Male;
        Check(BarracksContext.Select(a, bed.GetRoom()).thought == BarracksThought.SisterWithBrother, "Barracks female younger mapping");
        a.ageTracker.AgeBiologicalTicks = 30 * 3600000L;
        Check(BarracksContext.Select(a, bed.GetRoom()).thought == BarracksThought.SisterWithYoungerBrother, "Barracks female older mapping");
        a.ageTracker.AgeBiologicalTicks = b.ageTracker.AgeBiologicalTicks;
        Check(BarracksContext.Select(a, bed.GetRoom()).thought == BarracksThought.OnlySiblings, "Equal ticks use ordinary sibling barracks");
        a.ageTracker.AgeBiologicalTicks = 30 * 3600000L; a.gender = Gender.None;
        Check(BarracksContext.Select(a, bed.GetRoom()).thought == BarracksThought.OnlySiblings, "Gender.None does not select special barracks");
        a.gender = Gender.Male;
        Check(BarracksContext.Select(a, bed.GetRoom()).thought == BarracksThought.OnlySiblings, "Same sex uses ordinary sibling barracks");
        b.gender = Gender.Female;

        var effects = AccessTools.Method(typeof(Toils_LayDown), "ApplyBedRelatedEffects");
        var finish = AccessTools.Method(typeof(Toils_LayDown), "FinalizeLayingJob");
        a.mindState.applyBedThoughtsTick = Find.TickManager.TicksGame + 100;
        a.mindState.applyBedThoughtsOnLeave = false;
        effects.Invoke(null, [a, bed, false, false, 1]);
        finish.Invoke(null, [a, bed, false]);
        Check(!memories.Memories.Any(IsBarracks), "Before deadline and early leave do not manufacture sleep memory");
        a.mindState.applyBedThoughtsTick = Find.TickManager.TicksGame;
        effects.Invoke(null, [a, bed, false, false, 1]);
        var custom = memories.Memories.Single(IsBarracks);
        Check(custom.def == CompanionshipBarracksDefOf.SleptInBarracks_BrotherWithSister && custom.otherPawn == b,
            "Deadline submits and replaces vanilla memory with correct target");
        Check(custom.DurationTicks == 60000 && a.mindState.applyBedThoughtsOnLeave, "Vanilla duration and leave flag preserved");
        int expectedStage = RoomStatDefOf.Impressiveness.GetScoreStageIndex(bed.GetRoom().GetStat(RoomStatDefOf.Impressiveness));
        Check(custom.CurStageIndex == expectedStage && memories.GetFirstMemoryOfDef(ThoughtDefOf.SleptInBarracks) == null,
            "Original stage retained and vanilla removed only after acceptance");
        custom.age = 500;
        finish.Invoke(null, [a, bed, false]);
        Check(memories.Memories.Single(IsBarracks).age == 0, "Vanilla leave settlement renews only one custom sleep memory");

        var harness = new Harmony("companionship.tests");
        var gain = AccessTools.Method(typeof(MemoryThoughtHandler), "TryGainMemory", [typeof(Thought_Memory), typeof(Pawn)]);
        harness.Patch(gain, prefix: new HarmonyMethod(typeof(GameTests), nameof(RejectMemory)) { priority = Priority.First });
        harness.Patch(ApplyBed, prefix: new HarmonyMethod(typeof(GameTests), nameof(SkipBedOriginal)) { priority = Priority.First });
        Reject = custom.def;
        Settle(a, bed);
        Check(!memories.Memories.Any(IsBarracks) && memories.GetFirstMemoryOfDef(ThoughtDefOf.SleptInBarracks) != null,
            "Rejected barracks candidate preserves this settlement's vanilla memory");
        Reject = null;
        Settle(a, bed);
        custom = memories.Memories.Single(IsBarracks);
        SkipBed = true;
        Settle(a, bed);
        Check(memories.Memories.Contains(custom), "Skipped original settlement leaves custom memories untouched");
        SkipBed = false;

        for (int stage = 0; stage < 10; stage++)
        {
            foreach (var m in memories.Memories.Where(IsBarracks).ToArray()) memories.RemoveMemory(m);
            var vanilla = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.SleptInBarracks, stage);
            memories.TryGainMemory(vanilla);
            BedThoughtPatch.Postfix(a, bed, true);
            var result = memories.Memories.Single(IsBarracks);
            Check(result.CurStageIndex == stage && result.CurStage == result.def.stages[stage], "All existing stages retained: " + stage);
        }
        var definition = custom.def;
        var savedStages = definition.stages;
        definition.stages = [];
        try { Settle(a, bed); Check(memories.GetFirstMemoryOfDef(ThoughtDefOf.SleptInBarracks) != null, "Invalid stage falls back to vanilla"); }
        finally { definition.stages = savedStages; }
        Settle(a, otherBed);
        Check(!memories.Memories.Any(IsBarracks), "Non-owned bed clears stale custom memory without replacement");
        Settle(a, bed);
        a.story.traits.GainTrait(new Trait(TraitDefOf.Ascetic));
        Settle(a, bed);
        Check(!memories.Memories.Any(IsBarracks), "Ascetic follows vanilla eligibility");
        a.story.traits.RemoveTrait(a.story.traits.GetTrait(TraitDefOf.Ascetic));
        bed.ForOwnerType = BedOwnerType.Prisoner;
        Settle(a, bed);
        Check(!memories.Memories.Any(IsBarracks), "Prisoner bed follows vanilla exclusion");
        bed.ForOwnerType = BedOwnerType.Colonist;
        bed.Medical = true;
        Settle(a, bed);
        Check(!memories.Memories.Any(IsBarracks), "Unowned medical bed does not manufacture barracks effect");
        bed.Medical = false;
        a.ownership.ClaimBedIfNonMedical(bed);
        var animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Muffalo);
        Settle(animal, null);
        Check(animal.needs.mood == null, "Pawn without mood exits vanilla and patch safely");

        foreach (var m in memories.Memories.Where(IsBarracks).ToArray()) memories.RemoveMemory(m);
        var positive = (Thought_Memory)ThoughtMaker.MakeThought(CompanionshipBarracksDefOf.SleptInBarracks_GoodFriends_NoEnemies, 9);
        var negative = (Thought_Memory)ThoughtMaker.MakeThought(CompanionshipBarracksDefOf.SleptInBarracks_ManyEnemies, 0);
        var zero = (Thought_Memory)ThoughtMaker.MakeThought(CompanionshipBarracksDefOf.SleptInBarracks_OnlyParents, 0);
        zero.pawn = a;
        zero.moodOffset = -(int)zero.MoodOffset();
        memories.TryGainMemory(positive); memories.TryGainMemory(negative); memories.TryGainMemory(zero);
        ThoughtUtility.RemovePositiveBedroomThoughts(a);
        Check(!memories.Memories.Contains(positive) && memories.Memories.Contains(negative) && memories.Memories.Contains(zero),
            "Positive cleanup retains zero and negative custom effects");

        EventTests(a, b, harness);
        var eventMemory = memories.GetFirstMemoryOfDef(MakeLoveDefOf.MakeLoveWithSister);
        Settle(a, bed);
        Check(memories.Memories.Contains(eventMemory), "Sleep settlement does not clear event history");
        var sleep = memories.Memories.Single(IsBarracks);
        memories.TryGainMemory(ThoughtDefOf.GotSomeLovin, b);
        Check(memories.Memories.Contains(sleep), "Event submission does not clear sleep memory");
        b.ownership.UnclaimBed();
        Check(BarracksContext.Residents(a, bed.GetRoom()).Length == 0, "Unclaimed beds are not residents");
        Settle(a, bed);
        Check(!memories.Memories.Any(IsBarracks), "Zero-resident settlement returns to vanilla");
        otherBed.Destroy(); map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
        Check(bed.GetRoom().Role == RoomRoleDefOf.Bedroom, "After removing second bed, role=" + bed.GetRoom().Role);
        Settle(a, bed);
        int bedroomStage = RoomStatDefOf.Impressiveness.GetScoreStageIndex(bed.GetRoom().GetStat(RoomStatDefOf.Impressiveness));
        Check(!memories.Memories.Any(IsBarracks) && (memories.GetFirstMemoryOfDef(ThoughtDefOf.SleptInBedroom) != null)
            == (ThoughtDefOf.SleptInBedroom.stages[bedroomStage] != null), "Bedroom transition follows vanilla, including its null stage");
        Settle(a, null);
        Check(memories.GetFirstMemoryOfDef(ThoughtDefOf.SleptOnGround) != null, "Ground environmental memory preserved");
        harness.UnpatchAll("companionship.tests");

        // 同一游戏的实际保存/读取，涵盖两种记忆与原版睡眠期限。
        var saveSleep = (Thought_Memory)ThoughtMaker.MakeThought(CompanionshipBarracksDefOf.SleptInBarracks_BrotherWithSister, 7);
        memories.TryGainMemory(saveSleep, b); saveSleep.age = 1234;
        var saveEvent = memories.GetFirstMemoryOfDef(MakeLoveDefOf.MakeLoveWithSister);
        saveEvent.age = 2345; saveEvent.moodPowerFactor = 1.5f;
        a.mindState.applyBedThoughtsTick = Find.TickManager.TicksGame + 5432;
        a.mindState.applyBedThoughtsOnLeave = true;
        string id = a.ThingID, otherId = b.ThingID;
        int deadline = a.mindState.applyBedThoughtsTick, eventCount = memories.NumMemoriesOfDef(saveEvent.def);
        GameDataSaveLoader.SaveGame("cb-integration");
        SavedGameLoaderNow.LoadGameFromSaveFileNow("cb-integration");
        var loaded = Find.CurrentMap.mapPawns.AllPawns.Single(p => p.ThingID == id);
        var restored = loaded.needs.mood.thoughts.memories;
        var restoredSleep = restored.Memories.Single(IsBarracks);
        var restoredEvent = restored.GetFirstMemoryOfDef(MakeLoveDefOf.MakeLoveWithSister);
        Check(restoredSleep.age == 1234 && restoredSleep.CurStageIndex == 7 && restoredSleep.otherPawn.ThingID == otherId,
            "Save/load preserves sleep Def, stage, age and pawn reference");
        Check(restoredEvent.age == 2345 && restoredEvent.moodPowerFactor == 1.5f && restoredEvent.otherPawn.ThingID == otherId
            && restored.NumMemoriesOfDef(restoredEvent.def) == eventCount, "Save/load preserves event age, multiplier, partner and stack without duplication");
        Check(loaded.mindState.applyBedThoughtsTick == deadline && loaded.mindState.applyBedThoughtsOnLeave, "Save/load retains vanilla sleep deadline and leave flag");
        restoredSleep.age = 60000;
        restored.MemoryThoughtInterval();
        Check(!restored.Memories.Contains(restoredSleep) && restored.Memories.Contains(restoredEvent), "Sleep expires after one day independently of event");
        restoredEvent.age = restoredEvent.DurationTicks;
        restored.MemoryThoughtInterval();
        Check(!restored.Memories.Contains(restoredEvent), "Event expires after its configured duration");
    }

    private static void EventTests(Pawn a, Pawn b, Harmony harness)
    {
        var memories = a.needs.mood.thoughts.memories;
        var otherMemories = b.needs.mood.thoughts.memories;
        Check(LovinMemoryPatch.Select(a, b) == MakeLoveDefOf.MakeLoveWithSister && LovinMemoryPatch.Select(b, a) == MakeLoveDefOf.MakeLoveWithBrother,
            "Both supported event perspectives");
        a.ageTracker.AgeBiologicalTicks = b.ageTracker.AgeBiologicalTicks;
        Check(LovinMemoryPatch.Select(a, b) == null, "Equal-age event retains vanilla");
        var ordinary = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
        memories.TryGainMemory(ordinary, b);
        Check(memories.Memories.Contains(ordinary), "Unmapped event keeps the exact original instance");
        a.ageTracker.AgeBiologicalTicks -= 1;
        Check(LovinMemoryPatch.Select(a, b) == null && LovinMemoryPatch.Select(b, a) == null, "Unconfigured age direction retains vanilla");
        a.ageTracker.AgeBiologicalTicks = 30 * 3600000L;
        a.gender = Gender.None;
        Check(LovinMemoryPatch.Select(a, b) == null, "Gender.None event retains vanilla");
        a.gender = Gender.Male;
        if (ModsConfig.IdeologyActive)
            Check(a.Ideo?.PreceptsListForReading.Count > 0, "Ideology fixture has non-null precept context");
        foreach (float factor in new[] { 1f, 1.5f })
        {
            var input = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
            input.moodPowerFactor = factor; input.otherPawn = b; input.moodOffset = 2; input.durationTicksOverride = 180123;
            input.sourcePrecept = a.Ideo?.PreceptsListForReading.FirstOrDefault();
            memories.TryGainMemory(input);
            var result = memories.Memories.Last(m => m.def == MakeLoveDefOf.MakeLoveWithSister);
            Check(!memories.Memories.Contains(input) && result.otherPawn == b && result.moodPowerFactor == factor
                && result.moodOffset == 2 && result.durationTicksOverride == 180123 && result.sourcePrecept == input.sourcePrecept
                && ((Thought_MemorySocial)result).opinionOffset == result.CurStage.baseOpinionOffset,
                "Event retains runtime context and its own social defaults, multiplier=" + factor);
            result.moodOffset = 0;
            Check(result.MoodOffset() == 10 * factor, "Existing base mood 10 yields " + 10 * factor + " after runtime multiplier");
        }
        Check(otherMemories.NumMemoriesOfDef(MakeLoveDefOf.MakeLoveWithBrother) == 0, "One side never manufactures the other side's submission");
        otherMemories.TryGainMemory(ThoughtDefOf.GotSomeLovin, a);
        Check(otherMemories.NumMemoriesOfDef(MakeLoveDefOf.MakeLoveWithBrother) == 1, "Other side's own submission creates one correct result");
        var original = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
        Reject = MakeLoveDefOf.MakeLoveWithSister;
        int previous = memories.NumMemoriesOfDef(Reject);
        memories.TryGainMemory(original, b);
        Check(memories.Memories.Contains(original) && memories.NumMemoriesOfDef(Reject) == previous, "Rejected new event cannot mistake old same-Def memory for acceptance");
        Reject = null;
        var savedDef = MakeLoveDefOf.MakeLoveWithSister;
        MakeLoveDefOf.MakeLoveWithSister = null;
        var missing = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
        try { memories.TryGainMemory(missing, b); Check(memories.Memories.Contains(missing), "Missing event Def preserves original instance"); }
        finally { MakeLoveDefOf.MakeLoveWithSister = savedDef; }
        harness.Patch(AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.CanGetThought)),
            postfix: new HarmonyMethod(typeof(GameTests), nameof(FilterThought)));
        Filtered = ThoughtDefOf.GotSomeLovin;
        int count = memories.Memories.Count;
        memories.TryGainMemory(ThoughtDefOf.GotSomeLovin, b);
        Check(memories.Memories.Count == count, "Original eligibility rejection is not bypassed");
        Filtered = savedDef;
        var filtered = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);
        memories.TryGainMemory(filtered, b);
        Check(memories.Memories.Contains(filtered), "Candidate filter rejection returns original instance");
        Filtered = null;
        for (int i = 0; i < 12; i++)
        {
            foreach (var memory in memories.Memories) memory.age += 150;
            memories.TryGainMemory(ThoughtDefOf.GotSomeLovin, b);
        }
        Check(memories.NumMemoriesOfDef(savedDef) == 10 && memories.Memories.Any(m => m.def == savedDef && m.age == 0)
            && savedDef.stackedEffectMultiplier == 0.6f && savedDef.durationDays == 3, "Repeated events retain ten-stack cap, newest event and original decay/duration");
    }
}
