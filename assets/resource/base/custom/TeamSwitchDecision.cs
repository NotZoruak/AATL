namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>计算联队战每一轮的目标部队，并记录当前已选部队。</summary>
public static class TeamSwitchDecision
{
    public static int GetTargetTeam(string teamConfig, int remaining, int initialTeam)
    {
        if (teamConfig.Length != 9 || remaining is < 1 or > 9)
        {
            return initialTeam;
        }

        var team = teamConfig[9 - remaining] - '0';
        return team is >= 1 and <= 5 ? team : initialTeam;
    }

    public static bool ShouldSwitch(string teamConfig, int remaining, int initialTeam, int currentTeam)
    {
        return GetTargetTeam(teamConfig, remaining, initialTeam) != currentTeam;
    }
}

/// <summary>保存一次联队战任务中的当前部队，并在新一圈任务开始时重置。</summary>
public static class TeamSwitchState
{
    private static string? _teamConfig;
    private static int _initialTeam;
    private static int _currentTeam;
    private static int _lastRound;

    public static bool ShouldSwitch(string teamConfig, int remaining, int initialTeam)
    {
        var round = 11 - remaining;
        if (_teamConfig != teamConfig || _initialTeam != initialTeam || round == 2 || round < _lastRound)
        {
            _teamConfig = teamConfig;
            _initialTeam = initialTeam;
            _currentTeam = initialTeam;
        }

        _lastRound = round;
        return TeamSwitchDecision.ShouldSwitch(teamConfig, remaining, initialTeam, _currentTeam);
    }

    public static void SetCurrentTeam(int team)
    {
        _currentTeam = team;
    }
}
