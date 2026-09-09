using KukaRoboter.OnlineServicesFacade;
using KukaRoboter.Contracts.DataAccess;
using System.Text;
using System.Threading;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
var target = ScriptRunnerEnvironment.GetArgument<string>("target");

string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
string ReadValue(DataAccessFacade data, DataIdentifier identifier)
{
    var value = data.Read(identifier);
    if (value == null || !value.Exists || !value.IsAvailable || value.CurrentValue == null || value.CurrentValue.Value == null)
        return "<unavailable>";
    return value.CurrentValue.Value.ToString();
}

Logger.Info("OPERATION_MODE_SET_BEGIN=True");
try
{
    using (var data = new DataAccessFacade(address))
    {
        var identifier = new DataIdentifier("$MODE_OP");
        var before = ReadValue(data, identifier);
        Logger.Info("MODE_BEFORE=" + B64(before));

        if (!string.Equals(before, target, StringComparison.OrdinalIgnoreCase))
        {
            var write = new DataValue
            {
                Identifier = identifier,
                Values = new[] { new TimestampedValue(target) }
            };
            var result = data.Write(write);
            Logger.Info("WRITE_RESULT=" + B64(result == null ? string.Empty : result.ToString()));
            Thread.Sleep(1000);
        }

        var after = ReadValue(data, identifier);
        Logger.Info("MODE_AFTER=" + B64(after));
        var verified = string.Equals(after, target, StringComparison.OrdinalIgnoreCase);
        Logger.Info("MODE_VERIFIED=" + verified);
        return verified ? 0 : 71;
    }
}
catch (Exception exception)
{
    Logger.Error("OPERATION_MODE_SET_FAILED=" + B64(exception.ToString()));
    return 72;
}
