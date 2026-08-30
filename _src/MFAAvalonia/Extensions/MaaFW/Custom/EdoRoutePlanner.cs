using System;
using System.Collections.Generic;
using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public enum EdoStrategy
{
    DirectBoss,
    Conservative,
    Balanced,
    Aggressive
}

public enum EdoPointType
{
    Unknown,
    Black,
    Purple,
    Yellow,
    Boss
}

public static class EdoActionCountParser
{
    public static int Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return -1;

        var value = 0;
        var readingDigits = false;
        var sawChineseOne = false;
        foreach (var character in text)
        {
            var digit = character switch
            {
                >= '0' and <= '9' => character - '0',
                >= '０' and <= '９' => character - '０',
                _ => -1
            };
            if (digit >= 0)
            {
                value = value * 10 + digit;
                readingDigits = true;
                continue;
            }

            if (readingDigits)
                return value;

            sawChineseOne |= character == '一';
        }

        return readingDigits ? value : sawChineseOne ? 1 : -1;
    }

    public static int Resolve(int recognizedCount, string currentPoint, int savedCount)
    {
        if (recognizedCount >= 0)
            return recognizedCount;

        if (savedCount > 0)
            return savedCount;

        return -1;
    }

}

public static class EdoPointColorClassifier
{
    public const int RequiredPixels = 20;

    public static EdoPointType Classify(int blackPixels, int purplePixels, int yellowPixels)
    {
        if (blackPixels >= RequiredPixels)
            return EdoPointType.Black;

        if (purplePixels >= RequiredPixels)
            return EdoPointType.Purple;

        if (yellowPixels >= RequiredPixels)
            return EdoPointType.Yellow;

        return EdoPointType.Unknown;
    }

    public static EdoPointType Confirm(IReadOnlyList<EdoPointType> results)
    {
        if (results.Count != 3 || results[0] == EdoPointType.Unknown)
            return EdoPointType.Unknown;

        return results.All(result => result == results[0])
            ? results[0]
            : EdoPointType.Unknown;
    }
}

public sealed class EdoPlanningState
{
    private EdoPlanningState(
        string currentPoint,
        int remainingActions,
        IReadOnlyDictionary<string, EdoPointType> pointTypes,
        IReadOnlySet<string> visitedPoints)
    {
        CurrentPoint = currentPoint;
        RemainingActions = remainingActions;
        PointTypes = pointTypes;
        VisitedPoints = visitedPoints;
    }

    public string CurrentPoint { get; }

    public int RemainingActions { get; }

    public IReadOnlyDictionary<string, EdoPointType> PointTypes { get; }

    public IReadOnlySet<string> VisitedPoints { get; }

    public static EdoPlanningState Create(string currentPoint, int remainingActions)
    {
        return Create(currentPoint, remainingActions, null);
    }

    public static EdoPlanningState Create(
        string currentPoint,
        int remainingActions,
        IReadOnlyDictionary<string, EdoPointType>? pointTypes,
        IReadOnlySet<string>? visitedPoints = null)
    {
        if (string.IsNullOrWhiteSpace(currentPoint))
            throw new ArgumentException("当前位置不能为空", nameof(currentPoint));

        var pointTypeCopy = new Dictionary<string, EdoPointType>(
            pointTypes ?? new Dictionary<string, EdoPointType>(),
            StringComparer.Ordinal);
        var visitedCopy = new HashSet<string>(StringComparer.Ordinal)
        {
            "Start",
            currentPoint
        };
        if (visitedPoints != null)
            visitedCopy.UnionWith(visitedPoints);

        return new EdoPlanningState(currentPoint, remainingActions, pointTypeCopy, visitedCopy);
    }
}

public sealed record EdoPlanResult(
    string? NextPoint,
    IReadOnlyList<string> PlannedRoute,
    bool IsExplorationRoute,
    double BossSuccessProbability = 0);

public static class EdoRoutePlanner
{
    private static readonly IReadOnlyList<string> IdealRoute =
    [
        "Start", "P01", "P02", "P03", "P08", "P12", "P11", "P07",
        "P06", "P05", "P04", "P13", "P14", "P09", "P10", "P15",
        "P16", "Boss"
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Adjacency =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Start"] = ["P01", "P02", "P03"],
            ["P01"] = ["Start", "P02", "P05"],
            ["P02"] = ["Start", "P01", "P03", "P06"],
            ["P03"] = ["Start", "P02", "P07", "P08"],
            ["P04"] = ["P05", "P09", "P13"],
            ["P05"] = ["P01", "P04", "P06", "P10"],
            ["P06"] = ["P02", "P05", "P07", "P16"],
            ["P07"] = ["P03", "P06", "P08", "P11"],
            ["P08"] = ["P03", "P07", "P12"],
            ["P09"] = ["P04", "P10", "P14"],
            ["P10"] = ["P05", "P09", "P15", "P16"],
            ["P11"] = ["P07", "P12", "P16"],
            ["P12"] = ["P08", "P11", "P17"],
            ["P13"] = ["P04", "P14"],
            ["P14"] = ["P09", "P13", "P15"],
            ["P15"] = ["P10", "P14", "P16"],
            ["P16"] = ["P06", "P10", "P11", "P15", "P17", "Boss"],
            ["P17"] = ["P12", "P16"],
            ["Boss"] = ["P16"]
        };

    public static EdoPlanResult Plan(EdoPlanningState state, EdoStrategy strategy)
    {
        if (state.RemainingActions <= 0 || state.CurrentPoint == "Boss")
            return new EdoPlanResult(null, [], false);

        if (strategy == EdoStrategy.DirectBoss)
            return CreateBossRoute(state);

        if (strategy == EdoStrategy.Conservative)
            return CreateConservativePlan(state);

        var idealSuffix = FindIdealSuffix(state.CurrentPoint, state.PointTypes);
        if (idealSuffix.Count > 0 && IsVisited(state, idealSuffix[0]))
            return CreateBossRoute(state);
        var bestSafePrefix = Array.Empty<string>();
        var bestProbability = 0d;
        for (var length = 1; length <= idealSuffix.Count; length++)
        {
            var prefix = idealSuffix.Take(length).ToArray();
            if (!IsValidRoute(state.CurrentPoint, prefix))
                continue;

            var probability = EstimateBossSuccessProbability(state, prefix);
            if (probability < GetSafetyThreshold(strategy))
                continue;

            bestSafePrefix = prefix;
            bestProbability = probability;
        }

        if (bestSafePrefix.Length > 0)
        {
            return new EdoPlanResult(
                bestSafePrefix[0],
                [state.CurrentPoint, .. bestSafePrefix],
                true,
                bestProbability);
        }

        return CreateBossRoute(state);
    }

    private static EdoPlanResult CreateConservativePlan(EdoPlanningState state)
    {
        if (!Adjacency.TryGetValue(state.CurrentPoint, out var neighbors))
            return new EdoPlanResult(null, [], false);

        var candidates = neighbors
            .Where(point => CanReachBossInWorstCase(state, point))
            .OrderBy(point => IsVisited(state, point))
            .ThenBy(point => GetForwardIdealPriority(state.CurrentPoint, point))
            .ThenBy(point => FindShortestRoute(point, "Boss").Count)
            .ThenBy(point => point, StringComparer.Ordinal);
        var nextPoint = candidates.FirstOrDefault();
        if (nextPoint == null)
            return CreateBossRoute(state);

        return new EdoPlanResult(
            nextPoint,
            [state.CurrentPoint, nextPoint],
            nextPoint != "Boss",
            1);
    }

    private static bool CanReachBossInWorstCase(EdoPlanningState state, string candidate)
    {
        var actionsAfterMove = state.RemainingActions - 1;
        if (actionsAfterMove < 0)
            return false;

        var distanceToBoss = FindShortestRoute(candidate, "Boss").Count - 1;
        if (distanceToBoss < 0)
            return false;

        // 最坏情况按普通点不提供任何额外行动计算，确保每次重新规划仍然有必经余量。
        return actionsAfterMove >= distanceToBoss;
    }

    private static int GetIdealRouteIndex(string point)
    {
        for (var index = 0; index < IdealRoute.Count; index++)
        {
            if (IdealRoute[index] == point)
                return index;
        }

        return int.MaxValue;
    }

    private static bool IsVisited(EdoPlanningState state, string point)
    {
        return state.VisitedPoints.Contains(point);
    }

    private static int GetForwardIdealPriority(string currentPoint, string candidate)
    {
        var currentIndex = GetIdealRouteIndex(currentPoint);
        var candidateIndex = GetIdealRouteIndex(candidate);
        if (candidateIndex > currentIndex)
            return candidateIndex - currentIndex;

        return IdealRoute.Count + candidateIndex;
    }

    private static IReadOnlyList<string> FindIdealSuffix(
        string currentPoint,
        IReadOnlyDictionary<string, EdoPointType> pointTypes)
    {
        var currentIndex = FindIdealRouteIndex(currentPoint, pointTypes);
        if (currentIndex < 0)
            return [];

        return IdealRoute.Skip(currentIndex + 1).ToArray();
    }

    private static int FindIdealRouteIndex(
        string currentPoint,
        IReadOnlyDictionary<string, EdoPointType> pointTypes)
    {
        var occurrences = new List<int>();
        for (var index = 0; index < IdealRoute.Count; index++)
        {
            if (IdealRoute[index] != currentPoint)
                continue;

            occurrences.Add(index);
        }

        if (occurrences.Count == 0)
            return -1;

        if (occurrences.Count == 1)
            return occurrences[0];

        for (var occurrenceIndex = occurrences.Count - 1; occurrenceIndex >= 0; occurrenceIndex--)
        {
            var index = occurrences[occurrenceIndex];
            if (index == 0)
                return index;

            var previous = IdealRoute[index - 1];
            if (pointTypes.ContainsKey(previous))
                return index;
        }

        return occurrences[0];
    }

    private static double GetSafetyThreshold(EdoStrategy strategy)
    {
        return strategy switch
        {
            EdoStrategy.Conservative => 0.95,
            EdoStrategy.Aggressive => 0.5,
            _ => 0.8
        };
    }

    private static EdoPlanResult CreateBossRoute(EdoPlanningState state)
    {
        var route = FindShortestRoute(state.CurrentPoint, "Boss", state.VisitedPoints);
        var probability = route.Count > 1
            ? EstimateBossSuccessProbability(state, route.Skip(1).ToArray())
            : 0;
        return new EdoPlanResult(
            route.Count > 1 ? route[1] : null,
            route,
            false,
            probability);
    }

    private static bool IsValidRoute(string currentPoint, IReadOnlyList<string> route)
    {
        var previous = currentPoint;
        foreach (var point in route)
        {
            if (!IsAdjacent(previous, point))
                return false;

            previous = point;
        }

        return true;
    }

    private static double EstimateBossSuccessProbability(
        EdoPlanningState state,
        IReadOnlyList<string> route)
    {
        var knownPurple = state.PointTypes.Values.Count(type => type == EdoPointType.Purple);
        var knownYellow = state.PointTypes.Values.Count(type => type == EdoPointType.Yellow);
        var knownBlack = state.PointTypes.Values.Count(type => type == EdoPointType.Black);
        var simulations = new List<SimulationState>
        {
            new(
                state.RemainingActions,
                11 - knownPurple,
                4 - knownYellow,
                3 - knownBlack,
                new HashSet<string>(state.PointTypes.Keys, StringComparer.Ordinal))
        };

        foreach (var point in route)
        {
            var next = new List<SimulationState>();
            foreach (var simulation in simulations)
                ExpandSimulation(state, simulation, point, next);

            simulations = MergeSimulations(next);
            if (simulations.Count == 0)
                return 0;
        }

        var successfulProbability = 0d;
        foreach (var simulation in simulations)
        {
            var distance = FindShortestRoute(route[^1], "Boss").Count - 1;
            if (simulation.Actions >= distance)
                successfulProbability += simulation.Probability;
        }

        return successfulProbability;
    }

    private static List<SimulationState> MergeSimulations(IEnumerable<SimulationState> simulations)
    {
        return simulations
            .GroupBy(
                simulation => $"{simulation.Actions}:{simulation.Purple}:{simulation.Yellow}:{simulation.Black}",
                StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                return first with { Probability = group.Sum(simulation => simulation.Probability) };
            })
            .ToList();
    }

    private static void ExpandSimulation(
        EdoPlanningState state,
        SimulationState simulation,
        string point,
        ICollection<SimulationState> output)
    {
        if (simulation.Actions <= 0)
            return;

        if (point == "Boss")
        {
            output.Add(simulation with { Actions = simulation.Actions - 1 });
            return;
        }

        if (simulation.Visited.Contains(point))
        {
            output.Add(simulation with { Actions = simulation.Actions - 1 });
            return;
        }

        if (state.PointTypes.TryGetValue(point, out _))
        {
            var visited = new HashSet<string>(simulation.Visited, StringComparer.Ordinal)
            {
                point
            };
            output.Add(simulation with
            {
                Actions = simulation.Actions - 1,
                Visited = visited
            });
            return;
        }

        var totalUnknown = simulation.Purple + simulation.Yellow + simulation.Black;
        if (totalUnknown <= 0)
            return;

        AddSimulationBranch(simulation, point, EdoPointType.Black, 0, simulation.Black, totalUnknown, output);
        AddSimulationBranch(simulation, point, EdoPointType.Yellow, 1, simulation.Yellow, totalUnknown, output);
        for (var recovery = 0; recovery <= 3; recovery++)
            AddSimulationBranch(simulation, point, EdoPointType.Purple, recovery, simulation.Purple, totalUnknown * 4, output);
    }

    private static void AddSimulationBranch(
        SimulationState simulation,
        string point,
        EdoPointType type,
        int recovery,
        int count,
        int denominator,
        ICollection<SimulationState> output)
    {
        if (count <= 0)
            return;

        var visited = new HashSet<string>(simulation.Visited, StringComparer.Ordinal)
        {
            point
        };
        var branchProbability = (double)count / denominator;
        output.Add(simulation with
        {
            Actions = simulation.Actions - 1 + recovery,
            Purple = type == EdoPointType.Purple ? simulation.Purple - 1 : simulation.Purple,
            Yellow = type == EdoPointType.Yellow ? simulation.Yellow - 1 : simulation.Yellow,
            Black = type == EdoPointType.Black ? simulation.Black - 1 : simulation.Black,
            Visited = visited,
            Probability = simulation.Probability * branchProbability
        });
    }

    private static bool IsAdjacent(string from, string to)
    {
        return Adjacency.TryGetValue(from, out var neighbors) && neighbors.Contains(to);
    }

    private static IReadOnlyList<string> FindShortestRoute(
        string start,
        string destination,
        IReadOnlySet<string>? visitedPoints = null)
    {
        var distances = GetDistancesFrom(destination);
        if (!distances.TryGetValue(start, out var distance))
            return [];

        var routes = new List<IReadOnlyList<string>>();
        CollectShortestRoutes(start, destination, distance, distances, [start], routes);
        return routes
            .OrderBy(route => route.Skip(1).Count(point => visitedPoints?.Contains(point) == true))
            .ThenBy(route => route.Count > 1 ? GetForwardIdealPriority(route[0], route[1]) : 0)
            .ThenBy(route => string.Join(",", route), StringComparer.Ordinal)
            .FirstOrDefault() ?? [];
    }

    private static Dictionary<string, int> GetDistancesFrom(string destination)
    {
        var distances = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [destination] = 0
        };
        var queue = new Queue<string>();
        queue.Enqueue(destination);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in Adjacency[current])
            {
                if (distances.ContainsKey(neighbor))
                    continue;

                distances[neighbor] = distances[current] + 1;
                queue.Enqueue(neighbor);
            }
        }

        return distances;
    }

    private static void CollectShortestRoutes(
        string current,
        string destination,
        int distance,
        IReadOnlyDictionary<string, int> distances,
        List<string> route,
        ICollection<IReadOnlyList<string>> routes)
    {
        if (current == destination)
        {
            routes.Add([.. route]);
            return;
        }

        foreach (var neighbor in Adjacency[current])
        {
            if (!distances.TryGetValue(neighbor, out var neighborDistance)
                || neighborDistance != distance - 1)
                continue;

            route.Add(neighbor);
            CollectShortestRoutes(neighbor, destination, neighborDistance, distances, route, routes);
            route.RemoveAt(route.Count - 1);
        }
    }

    private sealed record SimulationState(
        int Actions,
        int Purple,
        int Yellow,
        int Black,
        HashSet<string> Visited,
        double Probability = 1);
}
