using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;
using Kuka.Sim.Progress;
using Kuka.Sim.ProgrammingCore;
using Kuka.Sim.ProgrammingCore.Execution;
using Kuka.Sim.ProgrammingCore.Simulation;
using KukaRoboter.KrlController.Core.Messaging;
using VisualComponents.Create3D;

public static class KukaLabIntegratedLiveProbe
{
    private static readonly object Gate = new object();
    private static readonly List<string> Events = new List<string>();
    private static readonly List<string> Samples = new List<string>();
    private static readonly List<string> ControllerMessages = new List<string>();
    private static DateTime StartedUtc;
    private static object KrlExecutor;
    private static object Interpreter;
    private static IVisualObject TcpNode;
    private static object SimulationViewModel;
    private static ISimulationController SimulationController;
    private static bool ProgramFinished;
    private static bool SawNonIdleInterpreterState;
    private static bool SawControllerError;
    private static string LastSample = string.Empty;

    public static void Main()
    {
        var resultPath = Environment.GetEnvironmentVariable("KUKA_LAB_RESULT_PATH");
        var tracePath = Environment.GetEnvironmentVariable("KUKA_LAB_TRACE_PATH");
        var componentPath = Environment.GetEnvironmentVariable("KUKA_LAB_COMPONENT_PATH");
        var sourcePath = Environment.GetEnvironmentVariable("KUKA_LAB_SOURCE_PATH");
        var dataPath = Environment.GetEnvironmentVariable("KUKA_LAB_DATA_PATH");
        var programName = Environment.GetEnvironmentVariable("KUKA_LAB_PROGRAM_NAME");
        if (string.IsNullOrWhiteSpace(programName)) programName = "LAB_MINIMAL";
        var exitCode = 2;
        IExecutor executor = null;

        try
        {
            RequireNewAbsolute(resultPath, false);
            RequireNewAbsolute(tracePath, false);
            RequireNewAbsolute(componentPath, true);
            RequireNewAbsolute(sourcePath, true);
            RequireNewAbsolute(dataPath, true);
            StartedUtc = DateTime.UtcNow;
            Trace(tracePath, "ENTERED");

            var application = IoC.Get<IApplication>();
            if (!application.Initialized || !application.IsReady || !application.ValidLicenseExists)
            {
                throw new InvalidOperationException("KUKA.Sim application is not initialized, ready, and licensed.");
            }

            // The vendor provider registers managers from component-behavior-added events.
            // Resolve it before loading the component so it cannot miss an executor that is
            // materialized as part of the component load.
            var managerProviderType = ResolveType("Kuka.Sim.ProgrammingCore.Programs.IProgramManagerProvider");
            var managerProvider = IoC.GetInstance(managerProviderType, null);

            bool loadedAsComponent;
            var loaded = application.LoadLayout(new Uri(Path.GetFullPath(componentPath)), new Vector3(0, 0, 0), out loadedAsComponent);
            if (loaded == null || loaded.Length == 0)
            {
                throw new InvalidOperationException("The exact C01 component or synchronized layout did not load.");
            }

            var exactComponents = loaded
                .OfType<ISimComponent>()
                .Where(item => string.Equals(item.Name, "KR 210 R2700-2 C01", StringComparison.Ordinal))
                .ToList();
            if (exactComponents.Count != 1)
            {
                throw new InvalidOperationException("The loaded project does not expose exactly one KR 210 R2700-2 C01 component.");
            }
            var component = exactComponents[0];
            var exactKrlExecutor = ExecutionExtensions.GetKrlExecutor(component);
            if (exactKrlExecutor == null)
            {
                Trace(tracePath, "KRL_EXECUTOR_CREATE_BEGIN mode=vendor-public-api");
                Kuka.Sim.ProgrammingCore.Execution.KrlExecutor.Create(component);
                PumpUntil(
                    delegate { return ExecutionExtensions.GetKrlExecutor(component) != null; },
                    TimeSpan.FromSeconds(15));
                exactKrlExecutor = ExecutionExtensions.GetKrlExecutor(component);
                Trace(tracePath, "KRL_EXECUTOR_CREATE_END exists=" + (exactKrlExecutor != null));
            }
            if (exactKrlExecutor == null)
                throw new InvalidOperationException("The vendor public KrlExecutor.Create API did not create an executor for the exact C01 component.");
            if (!exactKrlExecutor.IsEnabled)
                throw new InvalidOperationException("The exact C01 KRL executor exists but is disabled.");
            // A controller-synchronized layout persists OnlineExecution=true.  Integrated
            // validation must explicitly return the public executor to its local program
            // manager; otherwise the vendor controller looks for the newly imported module
            // in the remote manager and reports that it cannot find it.
            Set(exactKrlExecutor, "OnlineExecution", false);

            object manager = Invoke(managerProvider, "GetProgramManager", component);
            if (manager == null)
            {
                Trace(tracePath, "PROGRAM_MANAGER_PROVIDER_INITIALIZE_BEGIN managers="
                    + AsObjects(TryGet(managerProvider, "Managers") as IEnumerable).Count());
                // ProgramManagerProvider is a public vendor IPlugin.  A healthy shell calls
                // Initialize during plug-in startup; invoke that same public lifecycle hook
                // when the KUKA programming ribbon failed to register and left it uninitialized.
                Invoke(managerProvider, "Initialize");
                Trace(tracePath, "PROGRAM_MANAGER_PROVIDER_INITIALIZE_END managers="
                    + AsObjects(TryGet(managerProvider, "Managers") as IEnumerable).Count());
            }
            PumpUntil(delegate
            {
                manager = Invoke(managerProvider, "GetProgramManager", component);
                return manager != null;
            }, TimeSpan.FromSeconds(30));
            if (manager == null)
                throw new InvalidOperationException("The public program-manager provider did not bind the exact C01 component.");
            PumpUntil(delegate { return Convert.ToBoolean(Get(manager, "IsReady"), CultureInfo.InvariantCulture); }, TimeSpan.FromSeconds(15));
            if (!Convert.ToBoolean(Get(manager, "IsReady"), CultureInfo.InvariantCulture))
                throw new InvalidOperationException("The exact C01 program manager did not become ready within 15 seconds.");
            PumpUntil(delegate
            {
                return object.ReferenceEquals(TryGet(exactKrlExecutor, "ActiveProgramManager"), manager);
            }, TimeSpan.FromSeconds(10));
            if (!object.ReferenceEquals(TryGet(exactKrlExecutor, "ActiveProgramManager"), manager))
                throw new InvalidOperationException("The public KRL executor did not switch from the online manager to the local program manager.");
            var isComponent = true;
            Trace(tracePath, "PROGRAM_MANAGER_BOUND mode=provider"
                + " loadedAsComponent=" + loadedAsComponent
                + " managerType=" + manager.GetType().FullName
                + " managerComponent=" + NameOf(TryGet(manager, "Component"))
                + " ready=" + Text(TryGet(manager, "IsReady"))
                + " root=" + (TryGet(manager, "Root") != null)
                + " executor=" + (TryGet(manager, "KrlExecutor") != null)
                + " onlineExecution=" + Text(TryGet(exactKrlExecutor, "OnlineExecution"))
                + " localManagerActive=" + object.ReferenceEquals(TryGet(exactKrlExecutor, "ActiveProgramManager"), manager));
            TcpNode = component.FindNode("mountplate") as IVisualObject;
            if (TcpNode == null)
            {
                throw new InvalidOperationException("The exact C01 mountplate node is unavailable.");
            }
            Trace(tracePath, "COMPONENT_AND_TCP_READY loaded=" + loaded.Length);

            var config = Get(manager, "SimulationConfiguration");
            var originalMotionExecution = Text(Get(config, "MotionExecution"));
            var originalModule = Text(Get(config, "ProgramModuleName"));
            var originalStepMode = Text(Get(config, "ProgramStepMode"));
            var originalTrace = Text(Get(config, "TraceDataRecording"));
            SetEnum(config, "MotionExecution", "Integrated");
            SetEnum(config, "ProgramStepMode", "Go");
            Set(config, "TraceDataRecording", true);
            Trace(tracePath, "CONFIGURED_INTEGRATED_GO_PENDING_PROGRAM");

            var programDirectory = FindDirectory(Get(manager, "Root"), @"KRC:\R1\Program");
            if (programDirectory == null) throw new DirectoryNotFoundException(@"KRC:\R1\Program");
            var importType = ResolveType("Kuka.Sim.ProgrammingCore.Programs.ImportExportExtension");
            var importMethod = importType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "ImportFiles" && method.GetParameters().Length == 3);
            importMethod.Invoke(null, new object[]
            {
                Get(programDirectory, "KrlDirectory"),
                false,
                new List<string> { Path.GetFullPath(sourcePath), Path.GetFullPath(dataPath) }
            });

            var program = AsObjects(Get(programDirectory, "Programs") as IEnumerable)
                .Single(item => string.Equals(Text(Get(item, "Name")), programName, StringComparison.OrdinalIgnoreCase));
            var synchronizationStateBefore = Text(Get(program, "SynchronizationState"));
            RunWithDispatcherPump(delegate { Invoke(program, "Synchronize"); });
            var synchronizationStateAfter = Text(Get(program, "SynchronizationState"));
            var synchronizationModeAfter = Text(Get(program, "SynchronizationMode"));
            Invoke(program, "Activate");
            var programSelector = Text(Get(program, "FullPath"));
            if (string.IsNullOrWhiteSpace(programSelector))
                throw new InvalidOperationException("The imported program has no unique KRC path.");
            Set(config, "ProgramModuleName", programSelector);
            PumpUntil(delegate
            {
                PumpOnce();
                var visible = AsObjects(Get(programDirectory, "Programs") as IEnumerable)
                    .Any(item => string.Equals(Text(Get(item, "Name")), programName, StringComparison.OrdinalIgnoreCase));
                return visible
                    && string.Equals(Text(Get(config, "ProgramModuleName")), programSelector, StringComparison.OrdinalIgnoreCase);
            }, TimeSpan.FromSeconds(10));
            if (!string.Equals(Text(Get(config, "ProgramModuleName")), programSelector, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The imported program was not committed to the public simulation configuration.");
            Trace(tracePath, "IMPORTED_SYNCHRONIZED_ACTIVATED"
                + " program=" + Text(Get(program, "Name"))
                + " type=" + Text(TryGet(program, "ProgramType"))
                + " sync=" + Text(TryGet(program, "SynchronizationState"))
                + " configuredModule=" + Text(Get(config, "ProgramModuleName")));

            KrlExecutor = Get(manager, "KrlExecutor");
            executor = KrlExecutor as IExecutor;
            if (executor == null) throw new InvalidCastException("KRL executor does not implement IExecutor.");
            Interpreter = TryGet(KrlExecutor, "Interpreter");
            executor.StatementExecuting += OnStatementExecuting;
            executor.StatementExecuted += OnStatementExecuted;
            executor.ProgramFinished += OnProgramFinished;

            var simulationControl = IoC.Get<ISimulationControl>();
            var controllerCandidates = IoC.GetAll<ISimulationControllerFactory>()
                .Select(factory => new { Factory = factory, Controller = factory.GetController(exactKrlExecutor) })
                .Where(candidate => candidate.Controller != null)
                .ToList();
            if (controllerCandidates.Count != 1)
            {
                throw new InvalidOperationException(
                    "Expected one vendor simulation controller for Integrated execution, observed "
                    + controllerCandidates.Count + ".");
            }
            SimulationController = controllerCandidates[0].Controller;
            SimulationController.MessageAdded += OnControllerMessageAdded;
            Trace(tracePath, "PUBLIC_CONTROLLER_CREATED factory=" + controllerCandidates[0].Factory.GetType().FullName
                + " controller=" + SimulationController.GetType().FullName
                + " id=" + SimulationController.Id
                + " mode=" + SimulationController.SimulationMode);
            SimulationController.Initialize(
                new SimulationContext(simulationControl, new NullProgressReporter(), CancellationToken.None),
                CancellationToken.None);
            Interpreter = TryGet(KrlExecutor, "Interpreter");
            if (Interpreter == null)
                throw new InvalidOperationException("The vendor Integrated simulation controller initialized without assigning the KRL interpreter.");
            Trace(tracePath, "PUBLIC_CONTROLLER_INITIALIZED interpreter=" + Interpreter.GetType().FullName
                + " mode=" + Text(TryGet(Interpreter, "StepMode"))
                + " state=" + Text(TryGet(Interpreter, "State")));

            var statementTree = AsObjects(Get(Get(Get(program, "OriginalProgram"), "MainRoutine"), "Statements") as IEnumerable).ToList();
            var viewModelType = ResolveType("VisualComponents.UX.Shared.ISimulationControlViewModel");
            SimulationViewModel = IoC.GetInstance(viewModelType, null);
            if (SimulationViewModel == null) throw new InvalidOperationException("Simulation control view model is unavailable.");
            if (!Convert.ToBoolean(Get(SimulationViewModel, "IsSimulationAtStart"), CultureInfo.InvariantCulture))
            {
                Invoke(SimulationViewModel, "ResetSimulation");
                PumpUntil(delegate { return Convert.ToBoolean(Get(SimulationViewModel, "IsSimulationAtStart"), CultureInfo.InvariantCulture); }, TimeSpan.FromSeconds(10));
            }

            Capture("BEFORE_START");
            var modeBefore = Interpreter == null ? string.Empty : Text(TryGet(Interpreter, "StepMode"));
            var stateBefore = Interpreter == null ? string.Empty : Text(TryGet(Interpreter, "State"));
            Trace(tracePath, "SWITCH_RUN_STATE_BEGIN mode=" + modeBefore + " state=" + stateBefore);

            Invoke(SimulationViewModel, "SwitchRunState");
            Trace(tracePath, "SWITCH_RUN_STATE_RETURNED");
            SimulationController.Start();
            Trace(tracePath, "PUBLIC_CONTROLLER_START_RETURNED");
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                if (Interpreter == null) Interpreter = TryGet(KrlExecutor, "Interpreter");
                Capture("POLL");
                var state = Interpreter == null ? string.Empty : Text(TryGet(Interpreter, "State"));
                if (!string.IsNullOrEmpty(state) && !string.Equals(state, "Stop", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(state, "Reset", StringComparison.OrdinalIgnoreCase))
                {
                    SawNonIdleInterpreterState = true;
                }

                var running = Convert.ToBoolean(Get(SimulationViewModel, "IsSimulationRunning"), CultureInfo.InvariantCulture);
                if (ProgramFinished || SawControllerError || (SawNonIdleInterpreterState
                    && (string.Equals(state, "End", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(state, "Stop", StringComparison.OrdinalIgnoreCase))))
                {
                    break;
                }
                PumpOnce();
                Thread.Sleep(10);
            }

            if (Convert.ToBoolean(Get(SimulationViewModel, "IsSimulationRunning"), CultureInfo.InvariantCulture))
            {
                Trace(tracePath, "PROGRAM_FINISHED_PAUSE_BEGIN");
                Invoke(SimulationViewModel, "PauseSimulation");
                PumpUntil(
                    delegate { return !Convert.ToBoolean(Get(SimulationViewModel, "IsSimulationRunning"), CultureInfo.InvariantCulture); },
                    TimeSpan.FromSeconds(5));
                Trace(tracePath, "PROGRAM_FINISHED_PAUSE_END");
            }

            Capture("AFTER_RUN");
            var finalRunning = Convert.ToBoolean(Get(SimulationViewModel, "IsSimulationRunning"), CultureInfo.InvariantCulture);
            var finalPaused = Convert.ToBoolean(Get(SimulationViewModel, "IsSimulationPaused"), CultureInfo.InvariantCulture);
            var finalAtStart = Convert.ToBoolean(Get(SimulationViewModel, "IsSimulationAtStart"), CultureInfo.InvariantCulture);
            var finalState = Interpreter == null ? string.Empty : Text(TryGet(Interpreter, "State"));
            var finalMode = Interpreter == null ? string.Empty : Text(TryGet(Interpreter, "StepMode"));

            var lines = new List<string>
            {
                "APPLICATION\t" + application.Initialized + "\t" + application.IsReady + "\t" + application.ValidLicenseExists,
                "COMPONENT\t" + component.Name + "\t" + isComponent + "\t" + loaded.Length + "\tmountplate",
                "CONFIG_BEFORE\t" + originalMotionExecution + "\t" + originalModule + "\t" + originalStepMode + "\t" + originalTrace,
                "CONFIG_AFTER\t" + Text(Get(config, "MotionExecution")) + "\t" + Text(Get(config, "ProgramModuleName")) + "\t" + Text(Get(config, "ProgramStepMode")) + "\t" + Text(Get(config, "TraceDataRecording")),
                "SYNCHRONIZATION\t" + synchronizationStateBefore + "\t" + synchronizationStateAfter + "\t" + synchronizationModeAfter,
                "STATEMENT_TREE_COUNT\t" + statementTree.Count,
                "INTERPRETER_BEFORE\t" + modeBefore + "\t" + stateBefore,
                "INTERPRETER_AFTER\t" + finalMode + "\t" + finalState + "\tSawNonIdle=" + SawNonIdleInterpreterState,
                "SIMULATION_AFTER\tRunning=" + finalRunning + "\tPaused=" + finalPaused + "\tAtStart=" + finalAtStart,
                "PROGRAM_FINISHED\t" + ProgramFinished
            };
            lock (Gate)
            {
                lines.Add("EVENT_COUNT\t" + Events.Count);
                lines.AddRange(Events);
                lines.Add("SAMPLE_COUNT\t" + Samples.Count);
                lines.AddRange(Samples);
            }

            var controller = TryGet(KrlExecutor, "KrlController");
            var messages = controller == null ? null : TryGet(controller, "Messages") as IEnumerable;
            foreach (var message in AsObjects(messages))
            {
                lines.Add("MESSAGE\t" + Text(TryGet(message, "Id")) + "\t" + Text(TryGet(message, "Severity")) + "\t" + Clean(Text(TryGet(message, "Text"))));
            }
            lock (Gate)
            {
                lines.AddRange(ControllerMessages.Where(message => !lines.Contains(message)));
            }

            var succeeded = ProgramFinished
                && string.Equals(finalMode, "Go", StringComparison.OrdinalIgnoreCase)
                && !finalRunning
                && Samples.Count >= 2
                && Events.Any(item => item.StartsWith("STATEMENT_EXECUTING\t", StringComparison.Ordinal));
            lines.Add("SUCCESS\t" + succeeded);
            WriteNew(resultPath, lines);
            Trace(tracePath, "RESULT_WRITTEN success=" + succeeded);
            exitCode = succeeded ? 0 : 4;
        }
        catch (Exception exception)
        {
            var current = Unwrap(exception);
            try
            {
                if (!string.IsNullOrWhiteSpace(resultPath) && Path.IsPathRooted(resultPath) && !File.Exists(resultPath))
                {
                    WriteNew(resultPath, new[] { "ERROR\t" + current.GetType().FullName + "\t" + Clean(current.Message), current.ToString() });
                }
                if (!string.IsNullOrWhiteSpace(tracePath) && Path.IsPathRooted(tracePath)) Trace(tracePath, "ERROR " + current);
            }
            catch { }
        }
        finally
        {
            try
            {
                if (executor != null)
                {
                    executor.StatementExecuting -= OnStatementExecuting;
                    executor.StatementExecuted -= OnStatementExecuted;
                    executor.ProgramFinished -= OnProgramFinished;
                }
                if (SimulationViewModel != null)
                {
                    if (Convert.ToBoolean(Get(SimulationViewModel, "IsSimulationRunning"), CultureInfo.InvariantCulture)) Invoke(SimulationViewModel, "PauseSimulation");
                    Invoke(SimulationViewModel, "ResetSimulation");
                    PumpUntil(delegate { return Convert.ToBoolean(Get(SimulationViewModel, "IsSimulationAtStart"), CultureInfo.InvariantCulture); }, TimeSpan.FromSeconds(10));
                }
                if (SimulationController != null)
                {
                    SimulationController.MessageAdded -= OnControllerMessageAdded;
                    try { SimulationController.Stop(); } catch { }
                    SimulationController.Dispose();
                    SimulationController = null;
                }
            }
            catch (Exception cleanupException)
            {
                try { if (!string.IsNullOrWhiteSpace(tracePath)) Trace(tracePath, "CLEANUP_ERROR " + Unwrap(cleanupException)); } catch { }
                exitCode = 5;
            }
            try { Trace(tracePath, "EXIT_REQUESTED code=" + exitCode); } catch { }
            var current = Application.Current;
            if (current != null)
            {
                try { AppLoaderHelper.ExitApp(current, exitCode); } catch { }
            }

            // KUKA.Sim 4.10 may keep the native engine alive after WPF shutdown and
            // then enter AppBootstrapper.OnStartup again, which produces the recurring
            // NullReferenceException popup. The result/trace are already fsync'd and
            // controller objects are disposed above, so terminate this isolated,
            // Lab-owned process deterministically with the requested probe exit code.
            Environment.Exit(exitCode);
        }
    }

    private static void OnStatementExecuting(object sender, StatementExecutingEventArgs args)
    {
        AddEvent("STATEMENT_EXECUTING", args.Statement, args.IsImmediate);
        Capture("STATEMENT_EXECUTING");
    }

    private static void OnStatementExecuted(object sender, StatementExecutedEventArgs args)
    {
        AddEvent("STATEMENT_EXECUTED", args.Statement, args.IsImmediate);
        Capture("STATEMENT_EXECUTED");
    }

    private static void OnProgramFinished(object sender, EventArgs args)
    {
        lock (Gate) Events.Add("PROGRAM_FINISHED\t" + Elapsed());
        ProgramFinished = true;
        Capture("PROGRAM_FINISHED");
    }

    private static void OnControllerMessageAdded(object sender, MessagesChangedEventArgs args)
    {
        var message = args == null ? null : args.Message;
        if (message == null) return;
        var severity = Text(message.Severity);
        lock (Gate)
        {
            ControllerMessages.Add("MESSAGE\t" + message.Id + "\t" + severity + "\t" + Clean(message.Text));
            Events.Add("CONTROLLER_MESSAGE\t" + Elapsed() + "\t" + message.Id + "\t" + severity + "\t" + Clean(message.Text));
        }
        SawControllerError = severity.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void AddEvent(string kind, IStatement statement, bool immediate)
    {
        lock (Gate)
        {
            Events.Add(kind + "\t" + Elapsed() + "\t" + TypeOf(statement) + "\t" + TryText(statement, "Name") + "\tImmediate=" + immediate + "\t" + DescribeStatement(statement));
        }
    }

    private static string DescribeStatement(IStatement statement)
    {
        if (statement == null) return string.Empty;
        var parts = new List<string>();
        foreach (var property in statement.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length != 0 || !property.CanRead) continue;
            var name = property.Name;
            if (!(property.PropertyType.IsPrimitive || property.PropertyType.IsEnum || property.PropertyType == typeof(string))
                && name.IndexOf("Target", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Position", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Motion", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Point", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }
            try
            {
                var value = property.GetValue(statement, null);
                var text = Text(value);
                if (string.IsNullOrWhiteSpace(text) && value != null) text = TryText(value, "Name");
                if (!string.IsNullOrWhiteSpace(text)) parts.Add(name + "=" + Clean(text));
            }
            catch { }
        }
        return "DETAILS=" + string.Join(";", parts);
    }

    private static void Capture(string reason)
    {
        try
        {
            var controller = KrlExecutor == null ? null : TryGet(KrlExecutor, "KrlController");
            var positionData = controller == null ? null : TryGet(controller, "PositionData");
            var joints = positionData == null ? null : Invoke(positionData, "GetCurrentJointValues");
            var matrix = TcpNode == null ? new Matrix() : TcpNode.TransformationInWorld;
            var state = Interpreter == null ? string.Empty : Text(TryGet(Interpreter, "State"));
            var mode = Interpreter == null ? string.Empty : Text(TryGet(Interpreter, "StepMode"));
            var sample = "STATE=" + state + ",MODE=" + mode + "\t" + JointText(joints) + "\t" + MatrixText(matrix);
            lock (Gate)
            {
                if (!string.Equals(sample, LastSample, StringComparison.Ordinal))
                {
                    Samples.Add("SAMPLE\t" + Elapsed() + "\t" + reason + "\t" + sample);
                    LastSample = sample;
                }
            }
        }
        catch (Exception exception)
        {
            lock (Gate) Events.Add("CAPTURE_ERROR\t" + Elapsed() + "\t" + reason + "\t" + Clean(Unwrap(exception).Message));
        }
    }

    private static string JointText(object joints)
    {
        if (joints == null) return "JOINTS=Unavailable";
        return "JOINTS=" + string.Join(",", Enumerable.Range(1, 6).Select(index => "A" + index + "=" + Number(TryGet(joints, "A" + index))));
    }

    private static string MatrixText(Matrix matrix)
    {
        return "TCP=X=" + Number(matrix.Px) + ",Y=" + Number(matrix.Py) + ",Z=" + Number(matrix.Pz)
            + ",NX=" + Number(matrix.Nx) + ",NY=" + Number(matrix.Ny) + ",NZ=" + Number(matrix.Nz)
            + ",OX=" + Number(matrix.Ox) + ",OY=" + Number(matrix.Oy) + ",OZ=" + Number(matrix.Oz)
            + ",AX=" + Number(matrix.Ax) + ",AY=" + Number(matrix.Ay) + ",AZ=" + Number(matrix.Az);
    }

    private static object FindDirectory(object directory, string fullPath)
    {
        if (string.Equals(Text(Get(directory, "FullPath")), fullPath, StringComparison.OrdinalIgnoreCase)) return directory;
        foreach (var child in AsObjects(Get(directory, "Directories") as IEnumerable))
        {
            var found = FindDirectory(child, fullPath);
            if (found != null) return found;
        }
        return null;
    }

    private static void PumpUntil(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            PumpOnce();
            Thread.Sleep(10);
        }
    }

    private static void PumpOnce()
    {
        var current = Application.Current;
        if (current == null) return;
        var dispatcher = current.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(delegate(object state)
        {
            ((DispatcherFrame)state).Continue = false;
            return null;
        }), frame);
        Dispatcher.PushFrame(frame);
    }

    private static void RunWithDispatcherPump(System.Action action)
    {
        var task = Task.Run(action);
        while (!task.IsCompleted) PumpOnce();
        task.GetAwaiter().GetResult();
    }

    private static Type ResolveType(string fullName)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(fullName, false)).FirstOrDefault(candidate => candidate != null);
        if (type == null) throw new TypeLoadException(fullName);
        return type;
    }

    private static PropertyInfo FindProperty(object target, string name)
    {
        if (target == null) return null;
        var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property != null) return property;
        foreach (var contract in target.GetType().GetInterfaces())
        {
            property = contract.GetProperty(name);
            if (property != null) return property;
        }
        return null;
    }

    private static object Get(object target, string property)
    {
        var info = FindProperty(target, property);
        if (info == null) throw new MissingMemberException(TypeOf(target), property);
        return info.GetValue(target, null);
    }

    private static object TryGet(object target, string property)
    {
        try
        {
            var info = FindProperty(target, property);
            return info == null ? null : info.GetValue(target, null);
        }
        catch { return null; }
    }

    private static void Set(object target, string property, object value)
    {
        var info = FindProperty(target, property);
        if (info == null || !info.CanWrite) throw new MissingMemberException(TypeOf(target), property);
        info.SetValue(target, value, null);
    }

    private static void SetEnum(object target, string property, string name)
    {
        var info = FindProperty(target, property);
        if (info == null || !info.CanWrite) throw new MissingMemberException(TypeOf(target), property);
        info.SetValue(target, Enum.Parse(info.PropertyType, name, false), null);
    }

    private static object Invoke(object target, string method, params object[] arguments)
    {
        var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Concat(target.GetType().GetInterfaces().SelectMany(type => type.GetMethods()))
            .Where(candidate => candidate.Name == method && candidate.GetParameters().Length == arguments.Length)
            .Distinct()
            .ToList();
        var selected = methods.FirstOrDefault(candidate => Compatible(candidate.GetParameters(), arguments));
        if (selected == null) throw new MissingMethodException(TypeOf(target), method);
        return selected.Invoke(target, arguments);
    }

    private static bool Compatible(ParameterInfo[] parameters, object[] arguments)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (arguments[index] != null && !parameters[index].ParameterType.IsInstanceOfType(arguments[index])) return false;
        }
        return true;
    }

    private static IEnumerable<object> AsObjects(IEnumerable values)
    {
        if (values == null) yield break;
        foreach (var value in values) yield return value;
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
        return exception;
    }

    private static string NameOf(object component) { return TryText(component, "Name"); }
    private static string TypeOf(object value) { return value == null ? string.Empty : value.GetType().FullName; }
    private static string Text(object value) { return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture); }
    private static string TryText(object value, string property) { return Text(TryGet(value, property)); }
    private static string Number(object value) { return value == null ? string.Empty : Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture); }
    private static string Clean(string value) { return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("\t", " "); }
    private static string Elapsed() { return (DateTime.UtcNow - StartedUtc).TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture); }

    private static void RequireNewAbsolute(string path, bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new InvalidOperationException("Absolute path required.");
        if (mustExist && !File.Exists(path)) throw new FileNotFoundException("Input file missing.", path);
        if (!mustExist && File.Exists(path)) throw new IOException("Output must be a new file.");
    }

    private static void WriteNew(string path, IEnumerable<string> lines)
    {
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            foreach (var line in lines) writer.WriteLine(line);
            writer.Flush();
            stream.Flush(true);
        }
    }

    private static void Trace(string path, string value)
    {
        using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.WriteLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\t" + Clean(value));
            writer.Flush();
            stream.Flush(true);
        }
    }
}
