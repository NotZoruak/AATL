using System;
using System.Collections.Generic;
using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW;

internal static class MaaInterfaceActivationHelper
{
    public static string? ResolveControllerName(MaaInterface? maaInterface, MaaControllerTypes controllerType)
    {
        var controllerTypeKey = controllerType.ToJsonKey();
        return maaInterface?.Controller?.FirstOrDefault(c =>
            c.Type != null && c.Type.Equals(controllerTypeKey, StringComparison.OrdinalIgnoreCase))?.Name;
    }

    public static bool IsOptionApplicable(
        MaaInterface? maaInterface,
        MaaInterface.MaaInterfaceOption interfaceOption,
        string? currentControllerName,
        string? currentResourceName)
    {
        return MatchesFilter(interfaceOption.Controller, currentControllerName)
               && MatchesFilter(interfaceOption.Resource, currentResourceName);
    }

    public static bool IsTaskSupportedByResource(
        MaaInterface? maaInterface,
        MaaInterface.MaaInterfaceTask? task,
        string? currentResourceName)
    {
        if (task == null)
            return false;

        if (!MatchesFilter(task.Resource, currentResourceName))
            return false;

        if (string.IsNullOrWhiteSpace(currentResourceName))
            return true;

        var currentResource = maaInterface?.Resources.Values.FirstOrDefault(r =>
            r.Name != null && r.Name.Equals(currentResourceName, StringComparison.OrdinalIgnoreCase));

        return IsTaskAllowedByOwner(currentResource?.Task, task.Name);
    }

    public static bool IsTaskSupportedByController(
        MaaInterface? maaInterface,
        MaaInterface.MaaInterfaceTask? task,
        string? currentControllerName)
    {
        if (task == null)
            return false;

        if (!MatchesFilter(task.Controller, currentControllerName))
            return false;

        if (string.IsNullOrWhiteSpace(currentControllerName))
            return true;

        var currentController = maaInterface?.Controller?.FirstOrDefault(c =>
            c.Name != null && c.Name.Equals(currentControllerName, StringComparison.OrdinalIgnoreCase));

        return IsTaskAllowedByOwner(currentController?.Task, task.Name);
    }

    private static bool IsTaskAllowedByOwner(IReadOnlyCollection<string>? allowedTasks, string? taskName)
    {
        if (allowedTasks == null || allowedTasks.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(taskName))
            return false;

        return allowedTasks.Any(name => name.Equals(taskName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesFilter(IReadOnlyCollection<string>? allowedNames, string? currentName)
    {
        if (allowedNames == null || allowedNames.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(currentName))
            return false;

        return allowedNames.Any(name => name.Equals(currentName, StringComparison.OrdinalIgnoreCase));
    }
}
