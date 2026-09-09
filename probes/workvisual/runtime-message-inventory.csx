using KukaRoboter.OnlineServicesFacade;
using System.Text;

var address = ScriptRunnerEnvironment.GetArgument<string>("address");
string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

Logger.Info("KUKA Lab runtime message inventory");
Logger.Info("ControllerAddress=" + address);

try
{
    using (var messages = new RuntimeMessageWindowFacade(address))
    {
        var snapshot = messages.GetMessages().OrderBy(item => item.Id).ToArray();
        Logger.Info("MessageCount=" + snapshot.Length);
        foreach (var message in snapshot)
        {
            Logger.Info("MESSAGE=" + message.Id + "|" + B64(message.MessageCode) + "|" + message.MessageNumber
                + "|" + B64(message.Type.ToString()) + "|" + B64(message.Text) + "|" + B64(message.ResourceMessage));
        }
    }

    return 0;
}
catch (Exception ex)
{
    Logger.Error("RUNTIME_MESSAGE_INVENTORY_FAILED: " + ex.GetType().FullName + ": " + ex.Message);
    return 42;
}
