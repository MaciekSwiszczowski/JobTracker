using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent.Conditions;

namespace JobTracker.Architecture.Tests;

/// <summary>
/// Named groups of banned namespaces and members, and the ArchUnitNET conditions that forbid a layer from depending
/// on them. Bans match against dependency <em>targets</em> rather than namespace residence; see the remark on
/// <see cref="DependenciesOn"/> for why that distinction decides whether a rule works at all.
/// </summary>
internal static class ExternalPackages
{
    /// <summary>
    /// The MongoDB driver and BSON library. **The most valuable ban here** — it is "MongoDB types never appear in
    /// Domain or Application" (AGENTS.md) made mechanical, and it keeps the data layer the one place that knows
    /// which database it talks to.
    /// </summary>
    /// <remarks>
    /// A prefix rather than a type list: the driver spreads across <c>MongoDB.Driver</c>, <c>MongoDB.Bson</c> and
    /// their sub-namespaces, and a list would have to be kept in step with it — a list that falls behind is a ban with
    /// a hole in it. The prefix also covers <c>[BsonElement]</c>, <c>[BsonId]</c> and the other mapping attributes,
    /// because ArchUnitNET records an attribute on a type or member as a dependency of that type.
    /// </remarks>
    internal static readonly string[] MongoDb = ["MongoDB."];

    /// <summary>
    /// The wall clock, as the property getters a call compiles to. "The clock is an input" (AGENTS.md):
    /// production code reads time through an injected <see cref="TimeProvider"/>.
    /// </summary>
    /// <remarks>
    /// <c>BannedSymbols.txt</c> bans the same five, but only under <c>Debug_Analyzers</c>. This ban runs in every
    /// configuration the suite runs in, so a Debug build and test run catches it too. Each entry is the declaring
    /// type's full name and the getter's ArchUnitNET member name, which carries its (empty) parameter list.
    /// </remarks>
    internal static readonly string[] WallClock =
    [
        "System.DateTime.get_Now()",
        "System.DateTime.get_UtcNow()",
        "System.DateTime.get_Today()",
        "System.DateTimeOffset.get_Now()",
        "System.DateTimeOffset.get_UtcNow()",
    ];

    /// <summary>Every type a dependency target names, filtered to those starting with one of the prefixes.</summary>
    /// <remarks>
    /// Matched against dependency targets rather than through <c>Types().That().ResideInNamespace(...)</c>, because
    /// the loader only puts the solution's own assemblies into <c>Architecture.Types</c>. A package's own types — and
    /// the BCL's — land in <c>ReferencedTypes</c>, which the fluent predicates never select, so a namespace-residence
    /// ban on them silently matches nothing and passes for ever.
    /// </remarks>
    internal static IReadOnlyList<string> DependenciesOn(IType type, string[] namespacePrefixes)
    {
        return Sorted(
            type.Dependencies
                .Select(static dependency => dependency.Target.FullName)
                .Where(name => name is not null && StartsWithAny(name, namespacePrefixes)));
    }

    /// <summary>Every member a call in <paramref name="type"/> targets, filtered to the banned ones.</summary>
    internal static IReadOnlyList<string> CallsTo(IType type, string[] memberNames)
    {
        return Sorted(
            type.Dependencies
                .OfType<MethodCallDependency>()
                .Select(static call => $"{call.TargetMember.DeclaringType.FullName}.{call.TargetMember.Name}")
                .Where(name => Array.IndexOf(memberNames, name) >= 0));
    }

    internal static Func<IType, ConditionResult> NotDependingOn(string[] namespacePrefixes)
    {
        return Condition(type => DependenciesOn(type, namespacePrefixes), "depends on ");
    }

    internal static Func<IType, ConditionResult> NotCalling(string[] memberNames)
    {
        return Condition(type => CallsTo(type, memberNames), "calls ");
    }

    private static Func<IType, ConditionResult> Condition(Func<IType, IReadOnlyList<string>> offendersOf, string verb)
    {
        return type =>
        {
            var offenders = offendersOf(type);
            return offenders.Count == 0
                ? new ConditionResult(type, true, string.Empty)
                : new ConditionResult(type, false, verb + string.Join(", ", offenders));
        };
    }

    private static IReadOnlyList<string> Sorted(IEnumerable<string> names) =>
        [.. names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private static bool StartsWithAny(string name, string[] namespacePrefixes)
    {
        foreach (var prefix in namespacePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
