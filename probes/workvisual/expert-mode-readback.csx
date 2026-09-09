using KukaRoboter.Contracts.DataAccess;
using KukaRoboter.OnlineServicesFacade;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");

string ReadValue(DataAccessFacade data, string name)
{
    var value = data.Read(new[] { new DataIdentifier(name) }).FirstOrDefault();
    if (value == null || !value.Exists || !value.IsAvailable
        || value.CurrentValue == null || value.CurrentValue.Value == null)
    {
        return "<unavailable>";
    }

    return value.CurrentValue.Value.ToString();
}

string DescribeUserLevel(string raw)
{
    switch (raw)
    {
        case "5":
            return "Operator";
        case "10":
            return "Programmer";
        case "20":
            return "ExpertProgrammer";
        case "27":
            return "SafetyRecovery";
        case "29":
            return "SafetyMaintenance";
        case "30":
            return "KrcAdministrator";
        default:
            return "Unknown";
    }
}

Logger.Info("KUKA Lab read-only expert-mode transition readback");
Logger.Info("ControllerAddress=" + address);

try
{
    using (var data = new DataAccessFacade(address))
    {
        var userLevel = ReadValue(data, "$USER_LEVEL");
        Logger.Info("MODE_OP=" + ReadValue(data, "$MODE_OP"));
        Logger.Info("USER_LEVEL=" + userLevel);
        Logger.Info("USER_LEVEL_ROLE=" + DescribeUserLevel(userLevel));
        Logger.Info("USER_LEVEL_AT_LEAST_EXPERT="
            + (userLevel == "20" || userLevel == "27" || userLevel == "29" || userLevel == "30"));
        Logger.Info("DRIVES_ON=" + ReadValue(data, "$DRIVES_ON"));
        Logger.Info("PRO_STATE1=" + ReadValue(data, "$PRO_STATE1"));
        Logger.Info("PRO_MODE1=" + ReadValue(data, "$PRO_MODE1"));
    }

    return 0;
}
catch (Exception ex)
{
    Logger.Error("EXPERT_MODE_READBACK_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    return 42;
}
