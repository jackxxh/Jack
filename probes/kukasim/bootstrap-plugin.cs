using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using VisualComponents.Create3D;
using VisualComponents.UX.Shared;

[Export(typeof(IPlugin))]
public sealed class KukaLabKukaSim410Bootstrap : IPlugin
{
    private const string ProbeAssemblyVariable = "KUKA_LAB_PROBE_ASSEMBLY";
    private const string ProbeTypeVariable = "KUKA_LAB_PROBE_TYPE";
    private const string TracePathVariable = "KUKA_LAB_BRIDGE_TRACE_PATH";

    public void Initialize()
    {
        var probeAssembly = Environment.GetEnvironmentVariable(ProbeAssemblyVariable);
        var probeType = Environment.GetEnvironmentVariable(ProbeTypeVariable);
        if (string.IsNullOrWhiteSpace(probeAssembly) || string.IsNullOrWhiteSpace(probeType))
        {
            return;
        }

        var worker = new Thread(() => RunWhenReady(probeAssembly, probeType));
        worker.IsBackground = true;
        worker.Name = "KukaLab.KukaSim410.Bootstrap";
        worker.Start();
    }

    public void Exit()
    {
    }

    private static void RunWhenReady(string probeAssembly, string probeType)
    {
        try
        {
            var deadline = DateTime.UtcNow.AddMinutes(3);
            while (DateTime.UtcNow < deadline)
            {
                var current = Application.Current;
                var application = TryGetApplication();
                if (current != null && application != null && application.Initialized && application.IsReady)
                {
                    current.Dispatcher.Invoke(new System.Action(() => InvokeProbe(probeAssembly, probeType)));
                    return;
                }

                Thread.Sleep(200);
            }

            throw new TimeoutException("Timed out waiting for the KUKA.Sim application to become ready.");
        }
        catch (Exception exception)
        {
            WriteTrace(exception is TargetInvocationException && exception.InnerException != null
                ? exception.InnerException.ToString()
                : exception.ToString());
            var current = Application.Current;
            if (current != null)
            {
                current.Dispatcher.Invoke(new System.Action(() => AppLoaderHelper.ExitApp(current, 2)));
            }
        }
    }

    private static IApplication TryGetApplication()
    {
        try
        {
            return IoC.Get<IApplication>();
        }
        catch
        {
            return null;
        }
    }

    private static void InvokeProbe(string probeAssembly, string probeType)
    {
        var assemblyPath = Path.GetFullPath(probeAssembly);
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("The declared KUKA Lab probe assembly is missing.", assemblyPath);
        }

        var assembly = Assembly.LoadFrom(assemblyPath);
        var type = assembly.GetType(probeType, true);
        var main = type.GetMethod("Main", BindingFlags.Public | BindingFlags.Static);
        if (main == null || main.GetParameters().Length != 0)
        {
            throw new MissingMethodException(probeType, "Main");
        }

        main.Invoke(null, null);
    }

    private static void WriteTrace(string message)
    {
        try
        {
            var tracePath = Environment.GetEnvironmentVariable(TracePathVariable);
            if (!string.IsNullOrWhiteSpace(tracePath))
            {
                File.AppendAllText(tracePath, DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
            }
        }
        catch
        {
        }
    }
}
