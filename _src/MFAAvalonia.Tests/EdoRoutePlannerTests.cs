using MFAAvalonia.Extensions.MaaFW.Custom;

public static class EdoRoutePlannerTests
{
    public static void Run()
    {
        var emptyState = EdoPlanningState.Create("Start", 6);
        var firstPlan = EdoRoutePlanner.Plan(emptyState, EdoStrategy.Balanced);
        AssertTrue(firstPlan.NextPoint == "P01", "Start 行动充裕时应沿理想路线进入 P01");

        var conservativeState = EdoPlanningState.Create("Start", 5);
        var conservativePlan = EdoRoutePlanner.Plan(conservativeState, EdoStrategy.Conservative);
        AssertTrue(
            conservativePlan.NextPoint == "P01",
            "保守策略在理论成功率达到 95% 时应继续探索 P01");

        var detectedBlackStartState = EdoPlanningState.Create(
            "Start",
            6,
            new Dictionary<string, EdoPointType>
            {
                ["P01"] = EdoPointType.Black,
                ["P02"] = EdoPointType.Black
            });
        var detectedBlackStartPlan = EdoRoutePlanner.Plan(
            detectedBlackStartState,
            EdoStrategy.Conservative);
        AssertTrue(
            detectedBlackStartPlan.NextPoint == "P01",
            "仅识别到颜色的未走点不能被视为已访问，起步仍应进入 P01");

        AssertTrue(
            EdoPointColorClassifier.Confirm([EdoPointType.Purple, EdoPointType.Purple, EdoPointType.Purple])
                == EdoPointType.Purple,
            "三次颜色识别一致时才应确认该颜色");
        AssertTrue(
            EdoPointColorClassifier.Confirm([EdoPointType.Purple, EdoPointType.Black, EdoPointType.Purple])
                == EdoPointType.Unknown,
            "三次颜色识别不一致时不应记录颜色");

        var awayFromBossState = EdoPlanningState.Create("P03", 10);
        var awayFromBossPlan = EdoRoutePlanner.Plan(awayFromBossState, EdoStrategy.Balanced);
        AssertTrue(awayFromBossPlan.NextPoint == "P08", "行动充裕时应允许远离 Boss 继续探索理想路线");

        var prefixState = EdoPlanningState.Create("P03", 5);
        var prefixPlan = EdoRoutePlanner.Plan(prefixState, EdoStrategy.Balanced);
        AssertTrue(
            prefixPlan.PlannedRoute.Count >= 4
                && prefixPlan.PlannedRoute.Take(4).SequenceEqual(["P03", "P08", "P12", "P11"]),
            "行动有限时应沿理想路线评估安全前缀");

        var junctionState = EdoPlanningState.Create("P17", 10);
        var junctionPlan = EdoRoutePlanner.Plan(junctionState, EdoStrategy.Balanced);
        AssertTrue(junctionPlan.NextPoint == "P16", "P17 完成探索后应返回 P16 进入下一段理想路线");

        var p10State = EdoPlanningState.Create(
            "P10",
            10,
            new Dictionary<string, EdoPointType>
            {
                ["P09"] = EdoPointType.Black
            });
        var p10Plan = EdoRoutePlanner.Plan(p10State, EdoStrategy.Balanced);
        AssertTrue(p10Plan.NextPoint == "P15", "P10 后应沿理想路线进入 P15");

        var p16State = EdoPlanningState.Create(
            "P16",
            10,
            new Dictionary<string, EdoPointType>
            {
                ["P15"] = EdoPointType.Black
            });
        var p16Plan = EdoRoutePlanner.Plan(p16State, EdoStrategy.Balanced);
        AssertTrue(p16Plan.NextPoint == "Boss", "P16 后应沿理想路线进入 Boss");

        var fallbackState = EdoPlanningState.Create(
            "P07",
            2,
            new Dictionary<string, EdoPointType>
            {
                ["P03"] = EdoPointType.Purple,
                ["P08"] = EdoPointType.Purple,
                ["P12"] = EdoPointType.Yellow,
                ["P11"] = EdoPointType.Yellow,
                ["P07"] = EdoPointType.Purple
            });
        var fallbackPlan = EdoRoutePlanner.Plan(fallbackState, EdoStrategy.Balanced);
        AssertTrue(
            fallbackPlan.BossSuccessProbability < 1,
            "回退至最短王路线时应显示实际进王概率，而非固定 100%");

        var noSafeConservativeState = EdoPlanningState.Create(
            "Start",
            3,
            new Dictionary<string, EdoPointType>
            {
                ["P01"] = EdoPointType.Black,
                ["P02"] = EdoPointType.Black,
                ["P03"] = EdoPointType.Black
            });
        var noSafeConservativePlan = EdoRoutePlanner.Plan(
            noSafeConservativeState,
            EdoStrategy.Conservative);
        AssertTrue(
            noSafeConservativePlan.NextPoint == "P02",
            "保守策略没有安全探索路线时应继续沿最短王点路线前进");

        var revisitStartState = EdoPlanningState.Create(
            "P01",
            7,
            new Dictionary<string, EdoPointType>
            {
                ["P01"] = EdoPointType.Purple,
                ["P02"] = EdoPointType.Black,
                ["P05"] = EdoPointType.Black
            },
            new HashSet<string> { "P01" });
        var revisitStartPlan = EdoRoutePlanner.Plan(revisitStartState, EdoStrategy.Conservative);
        AssertTrue(
            revisitStartPlan.NextPoint == "P02",
            "保守策略在前进点已访问时也不能回到起点");

        var revisitIdealPointState = EdoPlanningState.Create(
            "P12",
            3,
            new Dictionary<string, EdoPointType>
            {
                ["P11"] = EdoPointType.Purple
            },
            new HashSet<string> { "P11", "P12" });
        var balancedRevisitPlan = EdoRoutePlanner.Plan(revisitIdealPointState, EdoStrategy.Balanced);
        AssertTrue(
            balancedRevisitPlan.NextPoint == "P17",
            "均衡策略在同样可进王时应优先走未访问的 P17");
        var aggressiveRevisitPlan = EdoRoutePlanner.Plan(revisitIdealPointState, EdoStrategy.Aggressive);
        AssertTrue(
            aggressiveRevisitPlan.NextPoint == "P17",
            "激进策略在同样可进王时应优先走未访问的 P17");

        var balancedLowActionPlan = EdoRoutePlanner.Plan(
            EdoPlanningState.Create("P10", 1),
            EdoStrategy.Balanced);
        AssertTrue(
            balancedLowActionPlan.NextPoint == "P16",
            "均衡策略步数不足时应先走最短进王路线");
        var balancedRecoveredActionPlan = EdoRoutePlanner.Plan(
            EdoPlanningState.Create("P10", 2),
            EdoStrategy.Balanced);
        AssertTrue(
            balancedRecoveredActionPlan.NextPoint == "P15",
            "均衡策略回步后步数充裕时应切回更长的理想路线");

        var knownBlackP16 = new Dictionary<string, EdoPointType>
        {
            ["P16"] = EdoPointType.Black
        };
        var aggressiveLowActionPlan = EdoRoutePlanner.Plan(
            EdoPlanningState.Create("P10", 1, knownBlackP16),
            EdoStrategy.Aggressive);
        AssertTrue(
            aggressiveLowActionPlan.NextPoint == "P16",
            "激进策略无法达到 50% 门槛时应先走最短进王路线");
        var aggressiveRecoveredActionPlan = EdoRoutePlanner.Plan(
            EdoPlanningState.Create("P10", 3, knownBlackP16),
            EdoStrategy.Aggressive);
        AssertTrue(
            aggressiveRecoveredActionPlan.NextPoint == "P15",
            "激进策略回步后达到 50% 门槛时应切回更长的理想路线");

        AssertTrue(
            EdoPointColorClassifier.Classify(20, 0, 0) == EdoPointType.Black,
            "黑色像素达到 20 个时应记录为黑色点");
        AssertTrue(
            EdoPointColorClassifier.Classify(0, 20, 0) == EdoPointType.Purple,
            "紫色像素达到 20 个时应记录为紫色点");
        AssertTrue(
            EdoPointColorClassifier.Classify(0, 0, 20) == EdoPointType.Yellow,
            "橙色像素达到 20 个时应记录为黄色点");
        AssertTrue(
            EdoPointColorClassifier.Classify(19, 0, 19) == EdoPointType.Unknown,
            "所有颜色像素都不足 20 个时不应确认点位类型");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
