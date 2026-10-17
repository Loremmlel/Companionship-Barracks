using System.Collections.Generic;
using System.Linq;

namespace CompanionshipBarracks.Barracks;

public enum Relationship { Neutral, Friend, GoodFriend, Enemy, Sibling, Parent, Child, OtherFamily }
public enum BarracksThought
{
    KeepVanilla,
    GoodFriends_NoEnemies, Friends_NoEnemies, GoodFriends_WithEnemies, Friends_WithEnemies,
    SomeEnemies, ManyEnemies, OnlySiblings, OnlyParents, OnlyChildren, OnlyRelatives,
    BrotherWithSister, SisterWithBrother, SisterWithYoungerBrother, BrotherWithOlderSister
}

public static class RelationshipRules
{
    public static Relationship Classify(int opinion, bool bloodFamily, bool sibling, bool parent, bool child)
    {
        if (bloodFamily && opinion >= 20)
            return sibling ? Relationship.Sibling : parent ? Relationship.Parent : child ? Relationship.Child : Relationship.OtherFamily;
        return opinion >= 60 ? Relationship.GoodFriend : opinion >= 20 ? Relationship.Friend : opinion <= -20 ? Relationship.Enemy : Relationship.Neutral;
    }

    public static BarracksThought Select(IReadOnlyList<Relationship> residents)
    {
        int n = residents.Count;
        if (n == 0) return BarracksThought.KeepVanilla;
        int family = residents.Count(r => r >= Relationship.Sibling);
        if (family == n)
        {
            if (residents.All(r => r == Relationship.Sibling)) return BarracksThought.OnlySiblings;
            if (residents.All(r => r == Relationship.Parent)) return BarracksThought.OnlyParents;
            if (residents.All(r => r == Relationship.Child)) return BarracksThought.OnlyChildren;
            return BarracksThought.OnlyRelatives;
        }
        int good = family + residents.Count(r => r == Relationship.GoodFriend);
        int positive = good + residents.Count(r => r == Relationship.Friend);
        int enemies = residents.Count(r => r == Relationship.Enemy);
        if (2 * good >= n) return enemies == 0 ? BarracksThought.GoodFriends_NoEnemies : BarracksThought.GoodFriends_WithEnemies;
        if (2 * positive >= n) return enemies == 0 ? BarracksThought.Friends_NoEnemies : BarracksThought.Friends_WithEnemies;
        if (enemies > 0) return 3 * enemies <= n ? BarracksThought.SomeEnemies : BarracksThought.ManyEnemies;
        return BarracksThought.KeepVanilla;
    }
}
