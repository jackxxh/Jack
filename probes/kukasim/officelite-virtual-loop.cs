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
using VisualComponents.Create3D;

public static class KukaLabOfficeLiteVirtualLoopProbe
{
    private static readonly List<string> TraceLines = new List<string>();
    private static readonly object TraceGate = new object();
    private static DateTime StartedUtc;
    private static string TracePath;

    public static void Main()
    {
        var current = Application.Current;
        if (current == null) throw new InvalidOperationException("KUKA.Sim WPF application is unavailable.");

        // The installed bootstrap invokes Main synchronously while KUKA.Sim can
        // still be finishing its startup screen.  Return to the vendor callback
        // immediately, then enter the Lab workflow only when the dispatcher is
        // idle.  This avoids modifying the installed bootstrap and prevents a
        // vendor modal window from opening behind the startup screen.
        current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new System.Action(Run));
    }

    private static void Run()
    {
        var resultPath = Environment.GetEnvironmentVariable("KUKA_LAB_RESULT_PATH");
        var tracePath = Environment.GetEnvironmentVariable("KUKA_LAB_TRACE_PATH");
        var componentPath = Environment.GetEnvironmentVariable("KUKA_LAB_COMPONENT_PATH");
        var synchronizedLayoutPath = Environment.GetEnvironmentVariable("KUKA_LAB_SYNCHRONIZED_LAYOUT_PATH");
        var controllerAddress = Environment.GetEnvironmentVariable("KUKA_LAB_CONTROLLER_ADDRESS");
        var programPath = Environment.GetEnvironmentVariable("KUKA_LAB_PROGRAM_PATH");
        var phase = Environment.GetEnvironmentVariable("KUKA_LAB_PHASE");
        var cycleText = Environment.GetEnvironmentVariable("KUKA_LAB_CYCLE");
        var exitCode = 2;

        try
        {
            RequireNewAbsolute(resultPath, false);
            RequireNewAbsolute(tracePath, false);
            RequireNewAbsolute(componentPath, true);
            RequireNewAbsolute(synchronizedLayoutPath, true);
            if (string.IsNullOrWhiteSpace(controllerAddress)) throw new ArgumentException("Controller address is required.");
            if (string.IsNullOrWhiteSpace(programPath)) throw new ArgumentException("Controller program path is required.");
            if (!string.Equals(phase, "valid", StringComparison.Ordinal) && !string.Equals(phase, "negative", StringComparison.Ordinal))
                throw new ArgumentException("Phase must be valid or negative.");
            int cycle;
            if (!int.TryParse(cycleText, NumberStyles.Integer, CultureInfo.InvariantCulture, out cycle) || cycle < 0 || cycle > 2)
                throw new ArgumentException("Cycle must be 0, 1, or 2.");
            if (string.Equals(phase, "valid", StringComparison.Ordinal) && cycle == 0)
                throw new ArgumentException("A valid phase requires cycle 1 or 2.");
            if (string.Equals(phase, "negative", StringComparison.Ordinal) && cycle != 0)
                throw new ArgumentException("The negative phase requires cycle 0.");
            StartedUtc = DateTime.UtcNow;
            TracePath = tracePath;
            Trace("ENTERED phase=" + phase + " cycle=" + cycle);

            // ApplicationIdle above releases the vendor startup callback first. This
            // second gate verifies the real main window before loading the layout.
            PumpUntil(IsHostStartupComplete, TimeSpan.FromSeconds(60), "KUKA.Sim host startup completion");
            Trace("HOST_STARTUP_COMPLETE mainWindow=" + Clean(Application.Current.MainWindow.Title));

            var application = IoC.Get<IApplication>();
            if (!application.Initialized || !application.IsReady || !application.ValidLicenseExists)
            {
                throw new InvalidOperationException("KUKA.Sim application is not initialized, ready, and licensed.");
            }

            bool loadedAsComponent;
            var loaded = application.LoadLayout(
                new Uri(Path.GetFullPath(synchronizedLayoutPath)),
                new Vector3(0, 0, 0),
                out loadedAsComponent);
            if (loaded == null || loaded.Length == 0)
            {
                throw new InvalidOperationException("The controller-synchronized KR 210 R2700-2 C01 layout did not load.");
            }
            Trace("SYNCHRONIZED_LAYOUT_LOAD source=" + Clean(Path.GetFullPath(synchronizedLayoutPath))
                + " loadedAsComponent=" + loadedAsComponent
                + " count=" + loaded.Length
                + " names=" + string.Join(",", loaded.Select(item => item == null ? "<null>" : Clean(item.Name))));

            var managerProviderType = ResolveType("Kuka.Sim.ProgrammingCore.Programs.IProgramManagerProvider");
            var managerProvider = IoC.GetInstance(managerProviderType, null);
            var allManagers = AsObjects(Get(managerProvider, "Managers") as IEnumerable).ToList();
            Trace("PROGRAM_MANAGERS " + string.Join("|", allManagers.Select(item =>
            {
                var itemExecutor = TryGet(item, "KrlExecutor");
                return Text(TryGet(TryGet(item, "Component"), "Name"))
                    + ":enabled=" + Text(TryGet(itemExecutor, "IsEnabled"))
                    + ":programManager=" + (TryGet(itemExecutor, "ProgramManager") != null);
            })));
            var matchingManagers = allManagers
                .Where(item => string.Equals(Text(TryGet(Get(item, "Component"), "Name")), "KR 210 R2700-2 C01", StringComparison.Ordinal))
                .ToList();
            if (matchingManagers.Count != 1)
            {
                throw new InvalidOperationException("Expected exactly one managed KR 210 R2700-2 C01 component but found " + matchingManagers.Count + ".");
            }
            var manager = matchingManagers[0];
            var component = Get(manager, "Component") as ISimComponent;
            if (component == null) throw new InvalidCastException("Managed KR 210 R2700-2 C01 object is not an ISimComponent.");
            var robot = component.GetRobot();
            if (robot == null || robot.RobotController == null || robot.RobotController.FlangeNode == null)
                throw new InvalidOperationException("The managed KR 210 R2700-2 C01 robot/flange API is unavailable.");
            var flangeNode = robot.RobotController.FlangeNode as IVisualObject;
            if (flangeNode == null) throw new InvalidOperationException("The KR 210 R2700-2 C01 controller flange is not an IVisualObject.");
            var config = Get(manager, "SimulationConfiguration");
            var executor = Get(manager, "KrlExecutor");
            Trace("EXECUTOR_BASELINE type=" + executor.GetType().FullName
                + " enabled=" + Text(TryGet(executor, "IsEnabled"))
                + " programManager=" + (TryGet(executor, "ProgramManager") != null));
            var connectionManagerType = ResolveType("Kuka.Sim.ProgrammingCore.Online.IOnlineConnectionManager");
            var connectionManager = IoC.GetInstance(connectionManagerType, null);
            if (connectionManager == null) throw new InvalidOperationException("KUKA.Sim online connection manager is unavailable.");
            var simulationControlType = ResolveType("Kuka.Sim.ProgrammingCore.ISimulationControl");
            var simulationControl = IoC.GetInstance(simulationControlType, null);
            if (simulationControl == null) throw new InvalidOperationException("KUKA.Sim programming simulation control is unavailable.");
            Trace("SIMULATION_FACTORIES types=" + PrivateCollectionTypes(simulationControl, "simulationControllerFactories"));

            var result = new List<string>
            {
                "APPLICATION\t" + application.Initialized + "\t" + application.IsReady + "\t" + application.ValidLicenseExists,
                "COMPONENT\t" + Clean(component.Name) + "\tTrue\t" + loaded.Length + "\tFlangeNode",
                "CONTRACT\tController\tViewOnly\tAttach\tHighLevelStart"
            };

            bool success;
            if (string.Equals(phase, "valid", StringComparison.Ordinal))
            {
                var observed = RunCycle(cycle, application, config, executor, connectionManager, simulationControl, component, flangeNode, controllerAddress, programPath);
                result.Add(observed.ResultLine);
                var disconnected = Disconnect(observed.Handler, observed.Connector, "after-cycle-" + cycle);
                result.Add("DISCONNECT\t" + cycle + "\t" + disconnected);
                success = observed.Succeeded && disconnected.IndexOf("Success=True", StringComparison.Ordinal) >= 0;
            }
            else
            {
                var negative = RunNegative(config, connectionManager, component);
                result.Add("NEGATIVE_ENDPOINT\tConnected=" + negative.Connected + "\tState=" + Clean(negative.State) + "\tSuccess=" + negative.Succeeded);
                success = !negative.Connected && negative.Succeeded && string.Equals(negative.State, "NoConnection", StringComparison.OrdinalIgnoreCase);
            }
            result.Add("SUCCESS\t" + success);
            WriteNew(resultPath, result);
            exitCode = success ? 0 : 4;
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
                Trace("ERROR " + current);
                if (!string.IsNullOrWhiteSpace(tracePath) && Path.IsPathRooted(tracePath) && !File.Exists(tracePath)) WriteNew(tracePath, TraceLines);
            }
            catch { }
        }
        finally
        {
            var requestedExitCode = exitCode;
            Task.Run(delegate
            {
                Thread.Sleep(15000);
                Environment.Exit(requestedExitCode);
            });
            var current = Application.Current;
            if (current != null) AppLoaderHelper.ExitApp(current, exitCode); else Environment.Exit(exitCode);
        }
    }

    private static CycleResult RunCycle(
        int cycle,
        IApplication application,
        object config,
        object executor,
        object connectionManager,
        object simulationControl,
        ISimComponent component,
        IVisualObject tcpNode,
        string address,
        string programPath)
    {
        Trace("CYCLE_BEGIN cycle=" + cycle);
        Set(config, "OnlineAddress", address + ";" + address);
        // The candidate is already uploaded and selected through the bounded
        // WorkVisual RuntimeManager transaction. ViewOnly + Attach consumes the
        // live OfficeLite joint state without opening KUKA.Sim's separate
        // controller-upload workflow; native execution still occurs through the
        // controller RuntimeManager interpreter below.
        SetEnum(config, "OnlineConnectionMode", "ViewOnly");
        SetEnum(config, "OnlineSimulationMode", "Attach");
        SetEnum(config, "MotionExecution", "Controller");
        // The online programming controller reads its target KRL module from
        // SimulationConfiguration. Configure the public vendor property before
        // SwitchRunState initializes that controller.
        var requestedProgramModule = ProgramStem(programPath);
        Trace("PROGRAM_CONFIGURATION_BEFORE module=" + Clean(Text(TryGet(config, "ProgramModuleName")))
            + " stepMode=" + Text(TryGet(config, "ProgramStepMode")));
        SetPublic(config, "ProgramModuleName", requestedProgramModule);
        SetPublicEnum(config, "ProgramStepMode", "Go");
        Trace("PROGRAM_CONFIGURATION_AFTER module=" + Clean(Text(TryGet(config, "ProgramModuleName")))
            + " stepMode=" + Text(TryGet(config, "ProgramStepMode")));
        Trace("ONLINE_EXECUTOR onlineExecution=" + Text(TryGet(executor, "OnlineExecution"))
            + " simulationMode=" + Text(TryGet(executor, "OnlineSimulationMode"))
            + " connectionMode=" + Text(TryGet(executor, "OnlineConnectionMode"))
            + " enabled=" + Text(TryGet(executor, "IsEnabled"))
            + " programManager=" + (TryGet(executor, "ProgramManager") != null));

        var handler = GetConnectionHandler(connectionManager, component);
        if (handler == null) throw new InvalidOperationException("The online connection manager returned no connection handler.");
        var connector = TryInvoke(handler, "GetControllerConnector");
        if (connector == null) throw new InvalidOperationException("The online connection handler exposes no controller connector.");
        Trace("CONTROLLER_CONNECTION_BASELINE handlerState=" + Text(TryGet(handler, "State"))
            + " connectorState=" + Text(TryGet(connector, "ConnectionState"))
            + " canConnect=" + BoolValue(TryGet(connector, "CanConnect")));
        if (!string.Equals(Text(TryGet(handler, "State")), "Connected", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Text(TryGet(connector, "ConnectionState")), "Connected", StringComparison.OrdinalIgnoreCase)
            && BoolValue(TryGet(connector, "CanConnect")))
        {
            Invoke(connector, "Connect");
            Trace("CONTROLLER_CONNECTION_REQUEST method=IControllerConnector.Connect");
        }
        PumpUntil(delegate
        {
            handler = GetConnectionHandler(connectionManager, component) ?? handler;
            return string.Equals(Text(TryGet(handler, "State")), "Connected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Text(TryGet(connector, "ConnectionState")), "Connected", StringComparison.OrdinalIgnoreCase);
        }, TimeSpan.FromSeconds(90), "controller connection after public Connect");

        Trace("CONTROLLER_CONNECTION_READY handlerState=" + Text(TryGet(handler, "State"))
            + " connectorState=" + Text(TryGet(connector, "ConnectionState")));
        var canUpdatePosition = BoolValue(TryGet(connector, "CanUpdatePosition"));
        var runtime = Get(connector, "Runtime");
        if (runtime == null) throw new InvalidOperationException("Connected composite exposes no ServiceHost runtime.");
        var interfaceType = Text(TryGet(handler, "InterfaceType"));
        var activeConnector = Text(TryGet(connector, "ActiveConnector"));
        Thread.Sleep(750);
        PumpOnce();

        var robot = GetRobotInterpreter(runtime);
        ApplyControllerJointsToScene(component, connector);
        var selected = Text(TryGet(robot, "SelectedProgram"));
        var selectionState = Text(TryGet(robot, "State"));
        Trace("PROGRAM_SELECTION_BASELINE selected=" + Clean(selected)
            + " state=" + selectionState
            + " mode=" + Text(TryGet(robot, "Mode")));
        if (!SameProgram(selected, programPath))
        {
            if (!string.IsNullOrWhiteSpace(selected))
            {
                Invoke(robot, "Deselect");
                PumpUntil(delegate
                {
                    robot = GetRobotInterpreter(runtime);
                    return string.IsNullOrWhiteSpace(Text(TryGet(robot, "SelectedProgram")))
                        && string.Equals(Text(TryGet(robot, "State")), "Free", StringComparison.OrdinalIgnoreCase);
                }, TimeSpan.FromSeconds(5), "program deselection before online simulation initialization");
            }
            Invoke(robot, "Select", programPath);
        }
        PumpUntil(delegate
        {
            robot = GetRobotInterpreter(runtime);
            return SameProgram(Text(TryGet(robot, "SelectedProgram")), programPath)
                && IsReadyState(Text(TryGet(robot, "State")));
        }, TimeSpan.FromSeconds(10), "program selection before online simulation initialization");
        SetProgramModeGo(robot);
        PumpUntil(delegate
        {
            robot = GetRobotInterpreter(runtime);
            return string.Equals(Text(TryGet(robot, "Mode")), "Go", StringComparison.OrdinalIgnoreCase)
                && IsReadyState(Text(TryGet(robot, "State")));
        }, TimeSpan.FromSeconds(5), "Go mode before online simulation initialization");
        Trace("PROGRAM_READY_BEFORE_SIMULATION selected=" + Clean(Text(TryGet(robot, "SelectedProgram")))
            + " state=" + Text(TryGet(robot, "State"))
            + " mode=" + Text(TryGet(robot, "Mode")));

        var simulationService = Caliburn.Micro.IoC.Get<ISimulationService>();
        PumpUntil(delegate
        {
            return Application.Current != null
                && Application.Current.MainWindow != null
                && Application.Current.MainWindow.IsLoaded;
        }, TimeSpan.FromSeconds(10), "KUKA.Sim main-window lifecycle");
        var simulationControlModel = Get(simulationControl, "SimulationControlModel");
        var runStateHandlers = TryGetField(simulationControlModel, "SimulationRunStateChanged") as Delegate;
        var programmingLifecycleWired = runStateHandlers != null
            && runStateHandlers.GetInvocationList().Any(subscription => object.ReferenceEquals(subscription.Target, simulationControl));
        Trace("SIMULATION_LIFECYCLE before=" + programmingLifecycleWired
            + " handlers=" + (runStateHandlers == null ? 0 : runStateHandlers.GetInvocationList().Length));
        if (!programmingLifecycleWired)
        {
            Invoke(simulationControl, "OnMainWindowShown", application, EventArgs.Empty);
            runStateHandlers = TryGetField(simulationControlModel, "SimulationRunStateChanged") as Delegate;
            programmingLifecycleWired = runStateHandlers != null
                && runStateHandlers.GetInvocationList().Any(subscription => object.ReferenceEquals(subscription.Target, simulationControl));
        }
        if (!programmingLifecycleWired)
            throw new InvalidOperationException("KUKA.Sim programming simulation lifecycle did not attach to SimulationRunStateChanged.");
        Trace("SIMULATION_LIFECYCLE after=True handlers=" + runStateHandlers.GetInvocationList().Length);
        Trace("SIMULATION_ENTRY method=ISimulationControlViewModel.SwitchRunState model="
            + simulationControlModel.GetType().FullName + " mainWindowLoaded=True");
        Trace("SIMULATION_PREPARE running=" + simulationService.IsSimulationRunning
            + " atStart=" + BoolValue(TryGet(simulationControlModel, "IsSimulationAtStart"))
            + " paused=" + BoolValue(TryGet(simulationControlModel, "IsSimulationPaused")));
        if (!BoolValue(TryGet(simulationControlModel, "IsSimulationAtStart")))
        {
            simulationService.ResetSimulation(true);
            PumpUntil(delegate
            {
                return BoolValue(TryGet(simulationControlModel, "IsSimulationAtStart"))
                    && !simulationService.IsSimulationRunning
                    && !BoolValue(TryGet(simulationControl, "IsResettingSimulation"));
            }, TimeSpan.FromSeconds(10), "KUKA.Sim simulation start position");
        }
        Trace("SIMULATION_READY atStart=" + BoolValue(TryGet(simulationControlModel, "IsSimulationAtStart")));
        var operationContext = GetRemoteOperationContext(connector);
        using (var remoteOperating = StartRemoteOperating(operationContext))
        {
            Trace("REMOTE_OPERATION_READY context=" + operationContext.GetType().FullName
                + " hasAccess=" + BoolValue(TryInvoke(operationContext, "HasAccess")));
            robot = GetRobotInterpreter(runtime);
            var startCount = 0;
            var completed = false;
            remoteOperating.ThrowIfFailed();
            // Enter through KUKA Programming's public run-state pipeline so its
            // online controller attaches before native RuntimeManager Start().
            Invoke(simulationControlModel, "SwitchRunState");
            remoteOperating.ThrowIfFailed();
            object onlineKrlController = null;
            var onlineKrlControllerError = string.Empty;
            var initializationStartedUtc = DateTime.UtcNow;
            var nextInitializationTraceUtc = initializationStartedUtc;
            PumpUntil(delegate
            {
                onlineKrlController = TryInvokeWithError(simulationControl, "GetKrlController", out onlineKrlControllerError, component);
                if (DateTime.UtcNow >= nextInitializationTraceUtc)
                {
                    Trace("SIMULATION_INIT_POLL running=" + simulationService.IsSimulationRunning
                        + " initializing=" + Text(TryGet(simulationControl, "IsInitializingSimulation"))
                        + " state=" + Text(TryGet(simulationControl, "SimulationState"))
                        + " controller=" + (onlineKrlController != null)
                        + " controllerError=" + Clean(onlineKrlControllerError)
                        + " controllerTypes=" + PrivateCollectionTypes(simulationControl, "simulationControllers")
                        + " enabled=" + Text(TryGet(executor, "IsEnabled"))
                        + " programManager=" + (TryGet(executor, "ProgramManager") != null));
                    nextInitializationTraceUtc = DateTime.UtcNow.AddSeconds(5);
                }
                return simulationService.IsSimulationRunning
                    && !BoolValue(TryGet(simulationControl, "IsInitializingSimulation"))
                    && string.Equals(Text(TryGet(simulationControl, "SimulationState")), "Running", StringComparison.OrdinalIgnoreCase)
                    && onlineKrlController != null;
            }, TimeSpan.FromSeconds(300), "KUKA.Sim online simulation-controller initialization");
            remoteOperating.ThrowIfFailed();
            Trace("SIMULATION_INITIALIZED cycle=" + cycle + " canUpdatePosition=" + canUpdatePosition
                + " state=" + Text(TryGet(simulationControl, "SimulationState"))
                + " controller=" + onlineKrlController.GetType().FullName);
            robot = GetRobotInterpreter(runtime);
            Trace("PROGRAM_AFTER_SIMULATION_INIT selected=" + Clean(Text(TryGet(robot, "SelectedProgram")))
                + " state=" + Text(TryGet(robot, "State"))
                + " mode=" + Text(TryGet(robot, "Mode")));

            robot = VerifyControllerSelectedProgramAndGo(runtime, programPath);
            Trace("PROGRAM_READY_AFTER_CONTROLLER_ATTACH selected=" + Clean(Text(TryGet(robot, "SelectedProgram")))
                + " state=" + Text(TryGet(robot, "State"))
                + " mode=" + Text(TryGet(robot, "Mode")));
            TraceControllerDiagnostics(address, robot, "BEFORE_HIGH_LEVEL_START");

            if (string.Equals(Text(TryGet(robot, "State")), "Reset", StringComparison.OrdinalIgnoreCase))
            {
                var beforeBcoLine = LineText(robot);
                Invoke(robot, "Start");
                startCount++;
                Trace("BCO_HIGH_LEVEL_START cycle=" + cycle + " count=" + startCount
                    + " afterSimulationInitialization=True before=Reset line=" + beforeBcoLine);
                PumpUntil(delegate
                {
                    robot = GetRobotInterpreter(runtime);
                    var state = Text(TryGet(robot, "State"));
                    return string.Equals(state, "End", StringComparison.OrdinalIgnoreCase)
                        || (string.Equals(state, "Stop", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(LineText(robot), beforeBcoLine, StringComparison.Ordinal));
                }, TimeSpan.FromSeconds(180), "native KSS BCO run after online simulation initialization");
                completed = string.Equals(Text(TryGet(robot, "State")), "End", StringComparison.OrdinalIgnoreCase);
                Trace("BCO_READY cycle=" + cycle
                    + " state=" + Text(TryGet(robot, "State"))
                    + " line=" + LineText(robot));
            }

        var initial = Capture(cycle, "before-start", connector, robot, component, tcpNode);
        var samples = new List<Pose> { initial };
        for (var pulse = startCount + 1; pulse <= 2; pulse++)
        {
            robot = GetRobotInterpreter(runtime);
            var state = Text(TryGet(robot, "State"));
            if (string.Equals(state, "End", StringComparison.OrdinalIgnoreCase))
            {
                completed = true;
                break;
            }
            if (!string.Equals(state, "Stop", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("High-level Start is not allowed from state " + state + ".");
            }

            var beforeLine = LineText(robot);
            Invoke(robot, "Start");
            startCount++;
            Trace("HIGH_LEVEL_START cycle=" + cycle + " count=" + startCount + " before=" + state + " line=" + beforeLine);
            var observedActive = false;
            var deadline = DateTime.UtcNow.AddSeconds(180);
            var index = 0;
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(100);
                PumpOnce();
                robot = GetRobotInterpreter(runtime);
                state = Text(TryGet(robot, "State"));
                if (string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase)) observedActive = true;
                ApplyControllerJointsToScene(component, connector);
                samples.Add(Capture(cycle, "start-" + startCount + "-poll-" + index++, connector, robot, component, tcpNode));
                if (string.Equals(state, "End", StringComparison.OrdinalIgnoreCase))
                {
                    completed = true;
                    break;
                }
                if (index > 2 && string.Equals(state, "Stop", StringComparison.OrdinalIgnoreCase)
                    && (observedActive || !string.Equals(LineText(robot), beforeLine, StringComparison.Ordinal))) break;
            }
            if (completed) break;
            Thread.Sleep(500);
        }

        robot = GetRobotInterpreter(runtime);
        var sceneSynchronized = PumpUntilNoThrow(delegate
        {
            ApplyControllerJointsToScene(component, connector);
            return SceneMatchesConnector(component, connector, 0.05);
        }, TimeSpan.FromSeconds(10));
        var final = Capture(cycle, "after-run", connector, robot, component, tcpNode);
        samples.Add(final);
        var finalState = Text(TryGet(robot, "State"));
        var finalMode = Text(TryGet(robot, "Mode"));
        completed = completed || string.Equals(finalState, "End", StringComparison.OrdinalIgnoreCase);
        var maxDisplacement = samples.Max(sample => Distance(initial, sample));

        var cleanupState = Text(TryGet(robot, "State"));
        if (string.Equals(cleanupState, "Active", StringComparison.OrdinalIgnoreCase))
        {
            Invoke(robot, "Stop");
            PumpUntilNoThrow(delegate
            {
                robot = GetRobotInterpreter(runtime);
                return !string.Equals(Text(TryGet(robot, "State")), "Active", StringComparison.OrdinalIgnoreCase);
            }, TimeSpan.FromSeconds(20));
        }
        robot = GetRobotInterpreter(runtime);
        cleanupState = Text(TryGet(robot, "State"));
        if (!string.Equals(cleanupState, "Free", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(cleanupState, "End", StringComparison.OrdinalIgnoreCase))
        {
            Invoke(robot, "Reset");
            PumpUntilNoThrow(delegate
            {
                robot = GetRobotInterpreter(runtime);
                var state = Text(TryGet(robot, "State"));
                return string.Equals(state, "Reset", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(state, "End", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(state, "Free", StringComparison.OrdinalIgnoreCase);
            }, TimeSpan.FromSeconds(20));
        }
        robot = GetRobotInterpreter(runtime);
        if (!string.IsNullOrWhiteSpace(Text(TryGet(robot, "SelectedProgram")))) Invoke(robot, "Deselect");
        Thread.Sleep(750);
        robot = GetRobotInterpreter(runtime);
        var deselected = string.IsNullOrWhiteSpace(Text(TryGet(robot, "SelectedProgram")));
        var succeeded = completed && startCount == 2
            && string.Equals(finalMode, "Go", StringComparison.OrdinalIgnoreCase)
            && string.Equals(finalState, "End", StringComparison.OrdinalIgnoreCase) && canUpdatePosition && sceneSynchronized
            && deselected && maxDisplacement > 0.01;
        if (simulationService.IsSimulationRunning) simulationService.ResetSimulation(true);
        PumpUntilNoThrow(delegate
        {
            return !simulationService.IsSimulationRunning && !BoolValue(TryGet(simulationControl, "IsResettingSimulation"));
        }, TimeSpan.FromSeconds(10));
        Trace("CYCLE_END cycle=" + cycle + " sceneSync=" + sceneSynchronized + " success=" + succeeded);

            remoteOperating.ThrowIfFailed();
            return new CycleResult
            {
                Succeeded = succeeded,
                FinalPose = final,
                MaxDisplacementMillimeters = maxDisplacement,
                Handler = handler,
                Connector = connector,
                ResultLine = "CYCLE\t" + cycle
                    + "\tConnected=True"
                    + "\tInterface=" + Clean(interfaceType)
                    + "\tActive=" + Clean(activeConnector)
                    + "\tStartCount=" + startCount
                    + "\tMode=" + Clean(finalMode)
                    + "\tState=" + Clean(finalState)
                    + "\tDeselected=" + deselected
                    + "\tSimulationInitialized=True"
                    + "\tCanUpdatePosition=" + canUpdatePosition
                    + "\tSceneSynchronized=" + sceneSynchronized
                    + "\tMaxDisplacementMm=" + F(maxDisplacement)
                    + "\tFinal=" + F(final.X) + "," + F(final.Y) + "," + F(final.Z)
                    + "\tSuccess=" + succeeded
            };
        }
    }

    private static string Disconnect(object handler, object connector, string label)
    {
        var canDisconnect = BoolValue(TryInvoke(connector, "CanDisconnect"));
        if (canDisconnect) Invoke(connector, "Disconnect");
        var disconnected = PumpUntilNoThrow(delegate
        {
            return handler == null || string.Equals(Text(TryGet(handler, "State")), "NoConnection", StringComparison.OrdinalIgnoreCase);
        }, TimeSpan.FromSeconds(30));
        var state = handler == null ? "NoConnection" : Text(TryGet(handler, "State"));
        Trace("DISCONNECTED label=" + label + " method=IControllerConnector.Disconnect canDisconnect=" + canDisconnect + " state=" + state + " success=" + disconnected);
        return "State=" + Clean(state) + "\tMethod=IControllerConnector.Disconnect\tCanDisconnect=" + canDisconnect + "\tSuccess=" + disconnected;
    }

    private static NegativeResult RunNegative(object config, object connectionManager, ISimComponent component)
    {
        Set(config, "OnlineAddress", "127.0.0.1;127.0.0.1");
        SetEnum(config, "OnlineConnectionMode", "ViewOnly");
        SetEnum(config, "OnlineSimulationMode", "Attach");
        SetEnum(config, "MotionExecution", "Controller");
        var handler = GetConnectionHandler(connectionManager, component);
        var connected = false;
        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < deadline)
        {
            var state = handler == null ? "NoConnection" : Text(TryGet(handler, "State"));
            Trace("NEGATIVE_POLL state=" + state);
            if (string.Equals(state, "Connected", StringComparison.OrdinalIgnoreCase)) { connected = true; break; }
            PumpOnce();
            Thread.Sleep(100);
        }
        var connector = handler == null ? null : TryInvoke(handler, "GetControllerConnector");
        var canDisconnect = BoolValue(TryInvoke(connector, "CanDisconnect"));
        if (canDisconnect) Invoke(connector, "Disconnect");
        var noConnection = PumpUntilNoThrow(delegate
        {
            return handler == null || string.Equals(Text(TryGet(handler, "State")), "NoConnection", StringComparison.OrdinalIgnoreCase);
        }, TimeSpan.FromSeconds(15));
        var finalState = handler == null ? "NoConnection" : Text(TryGet(handler, "State"));
        Trace("NEGATIVE_END connected=" + connected + " state=" + finalState + " success=" + noConnection);
        return new NegativeResult { Connected = connected, State = finalState, Succeeded = noConnection };
    }

    private static object GetConnectionHandler(object manager, ISimComponent component)
    {
        try { return Invoke(manager, "GetConnectionHandler", component); }
        catch { return null; }
    }

    private static object GetRobotInterpreter(object runtime)
    {
        var interpreterType = ResolveType("KukaRoboter.Contracts.RuntimeManager.InterpreterType");
        var robot = Enum.Parse(interpreterType, "Robot", true);
        return Invoke(runtime, "GetInterpreter", robot);
    }

    private static RemoteOperatingLease StartRemoteOperating(object operationContext)
    {
        Trace("REMOTE_OPERATION_REQUEST context=" + operationContext.GetType().FullName
            + " hasAccess=" + BoolValue(TryInvoke(operationContext, "HasAccess")));
        var accessTask = Task.Run(delegate
        {
            Invoke(operationContext, "RequestAccess", CancellationToken.None);
        });
        PumpUntil(delegate { return accessTask.IsCompleted; }, TimeSpan.FromSeconds(30), "remote operation access");
        if (accessTask.IsFaulted) throw accessTask.Exception.GetBaseException();
        if (!BoolValue(TryInvoke(operationContext, "HasAccess")))
            throw new InvalidOperationException("KUKA.Sim remote operation access was not granted.");
        Invoke(operationContext, "Start");
        Invoke(operationContext, "Operate", -1.0d);
        return new RemoteOperatingLease(operationContext);
    }

    private static object GetRemoteOperationContext(object connector)
    {
        var operationContext = Get(connector, "OperationContext");
        var contextType = operationContext.GetType().FullName;
        var officeLiteNoSpoc = string.Equals(
            contextType,
            "Kuka.Sim.ProgrammingCore.Simulation.NullSimulationOperationContext",
            StringComparison.Ordinal);
        Trace("REMOTE_OPERATION_CONTEXT source=ServiceHostConnector.OperationContext"
            + " type=" + contextType
            + " runtime=" + (officeLiteNoSpoc ? "OfficeLite" : "Controller")
            + " noSpocRequired=" + officeLiteNoSpoc);
        return operationContext;
    }

    private static void SetProgramModeGo(object robot)
    {
        var modesType = ResolveType("KukaRoboter.Contracts.RuntimeManager.ProModes");
        Invoke(robot, "SetProgramMode", Enum.Parse(modesType, "Go", true));
    }

    private static object VerifyControllerSelectedProgramAndGo(object runtime, string programPath)
    {
        var robot = GetRobotInterpreter(runtime);
        var selected = Text(TryGet(robot, "SelectedProgram"));
        if (!SameProgram(selected, programPath))
        {
            throw new InvalidOperationException(
                "The attached OfficeLite controller selected '" + Clean(selected)
                + "' instead of requested module '" + Clean(programPath)
                + "' configured through SimulationConfiguration.ProgramModuleName.");
        }

        PumpUntil(delegate
        {
            robot = GetRobotInterpreter(runtime);
            return SameProgram(Text(TryGet(robot, "SelectedProgram")), programPath)
                && IsReadyState(Text(TryGet(robot, "State")));
        }, TimeSpan.FromSeconds(15), "program selection after controller attach");

        SetProgramModeGo(robot);
        PumpUntil(delegate
        {
            robot = GetRobotInterpreter(runtime);
            return SameProgram(Text(TryGet(robot, "SelectedProgram")), programPath)
                && string.Equals(Text(TryGet(robot, "Mode")), "Go", StringComparison.OrdinalIgnoreCase)
                && IsReadyState(Text(TryGet(robot, "State")));
        }, TimeSpan.FromSeconds(10), "Go mode after controller attach");
        return robot;
    }

    private static void TraceControllerDiagnostics(string address, object robot, string label)
    {
        Trace("CONTROLLER_DIAGNOSTIC " + label
            + " SelectedProgram=" + Clean(Text(TryGet(robot, "SelectedProgram")))
            + " Mode=" + Text(TryGet(robot, "Mode"))
            + " State=" + Text(TryGet(robot, "State")));

        object dataAccess = null;
        object messageWindow = null;
        try
        {
            dataAccess = Activator.CreateInstance(ResolveType("KukaRoboter.OnlineServicesFacade.DataAccessFacade"), new object[] { address });
            foreach (var name in new[] { "$MODE_OP", "$COULD_START_MOTION", "$DRIVES_ON", "$ON_PATH" })
            {
                var identifier = Activator.CreateInstance(
                    ResolveType("KukaRoboter.Contracts.DataAccess.DataIdentifier"),
                    new object[] { name });
                var value = Invoke(dataAccess, "Read", identifier);
                var current = TryGet(TryGet(value, "CurrentValue"), "Value");
                var available = BoolValue(TryGet(value, "Exists")) && BoolValue(TryGet(value, "IsAvailable"));
                Trace("CONTROLLER_DATA " + label + " " + name + "=" + (available ? Clean(Text(current)) : "<unavailable>"));
            }

            messageWindow = Activator.CreateInstance(
                ResolveType("KukaRoboter.OnlineServicesFacade.RuntimeMessageWindowFacade"),
                new object[] { address });
            var messages = AsObjects(Invoke(messageWindow, "GetMessages") as IEnumerable).ToList();
            Trace("CONTROLLER_MESSAGE_COUNT " + label + " count=" + messages.Count);
            foreach (var message in messages)
            {
                Trace("CONTROLLER_MESSAGE " + label
                    + " code=" + Clean(Text(TryGet(message, "MessageCode")))
                    + " number=" + Clean(Text(TryGet(message, "MessageNumber")))
                    + " type=" + Clean(Text(TryGet(message, "Type")))
                    + " text=" + Clean(Text(TryGet(message, "Text"))));
            }
        }
        catch (Exception exception)
        {
            var current = Unwrap(exception);
            Trace("CONTROLLER_DIAGNOSTIC_ERROR " + label + " " + current.GetType().FullName + ": " + Clean(current.Message));
        }
        finally
        {
            var messagesDisposable = messageWindow as IDisposable;
            if (messagesDisposable != null) messagesDisposable.Dispose();
            var dataDisposable = dataAccess as IDisposable;
            if (dataDisposable != null) dataDisposable.Dispose();
        }
    }

    private static Pose Capture(int cycle, string label, object connector, object robot, ISimComponent component, IVisualObject tcpNode)
    {
        var matrix = tcpNode.TransformationInWorld;
        var pose = new Pose(matrix.Px, matrix.Py, matrix.Pz);
        var joints = TryGet(connector, "JointValues") as IEnumerable;
        var jointText = joints == null ? "Unavailable" : string.Join(",", AsObjects(joints).Select(value => F(Convert.ToDouble(value, CultureInfo.InvariantCulture))));
        var sceneJoints = component.GetRobot() == null
            ? "Unavailable"
            : string.Join(",", component.GetRobot().RobotController.Joints.Select(item => F(item.Dof.Value)));
        Trace("SAMPLE\t" + cycle + "\t" + label
            + "\tState=" + Text(TryGet(robot, "State"))
            + "\tMode=" + Text(TryGet(robot, "Mode"))
            + "\tJoints=" + jointText
            + "\tSceneJoints=" + sceneJoints
            + "\tTcp=" + F(pose.X) + "," + F(pose.Y) + "," + F(pose.Z));
        return pose;
    }

    private static bool SceneMatchesConnector(ISimComponent component, object connector, double tolerance)
    {
        var connectorValues = AsObjects(TryGet(connector, "JointValues") as IEnumerable)
            .Select(value => Convert.ToDouble(value, CultureInfo.InvariantCulture)).Take(6).ToList();
        var robot = component.GetRobot();
        if (robot == null || connectorValues.Count != 6 || robot.RobotController.Joints.Count < 6) return false;
        for (var index = 0; index < 6; index++)
        {
            if (Math.Abs(robot.RobotController.Joints[index].Dof.Value - connectorValues[index]) > tolerance) return false;
        }
        return true;
    }

    private static bool ApplyControllerJointsToScene(ISimComponent component, object connector)
    {
        var connectorValues = AsObjects(TryGet(connector, "JointValues") as IEnumerable)
            .Select(value => Convert.ToDouble(value, CultureInfo.InvariantCulture)).Take(6).ToList();
        var robot = component.GetRobot();
        if (robot == null || connectorValues.Count != 6 || robot.RobotController.Joints.Count < 6) return false;

        var updated = false;
        for (var index = 0; index < 6; index++)
        {
            var dof = robot.RobotController.Joints[index].Dof;
            var valueProperty = dof.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (valueProperty == null || !valueProperty.CanWrite) continue;
            valueProperty.SetValue(dof, connectorValues[index], null);
            updated = true;
        }

        if (updated) PumpOnce();
        return updated;
    }

    private static string LineText(object robot)
    {
        var line = TryGet(robot, "Line");
        return line == null ? "<null>" : Text(TryGet(line, "File")) + ":" + Text(TryGet(line, "Line"));
    }

    private static bool SameProgram(string selected, string requested)
    {
        return !string.IsNullOrWhiteSpace(selected)
            && string.Equals(ProgramStem(selected), ProgramStem(requested), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadyState(string state)
    {
        return string.Equals(state, "Reset", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "Stop", StringComparison.OrdinalIgnoreCase);
    }

    private static string ProgramStem(string value)
    {
        var normalized = (value ?? string.Empty).Replace('\\', '/');
        var parts = normalized.Split('/');
        var leaf = parts.Length == 0 ? string.Empty : parts[parts.Length - 1];
        return leaf.EndsWith(".SRC", StringComparison.OrdinalIgnoreCase) ? leaf.Substring(0, leaf.Length - 4) : leaf;
    }

    private static bool PumpUntilNoThrow(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return true;
            PumpOnce();
            Thread.Sleep(50);
        }
        return predicate();
    }

    private static void PumpUntil(Func<bool> predicate, TimeSpan timeout, string label)
    {
        if (!PumpUntilNoThrow(predicate, timeout)) throw new TimeoutException("Timed out waiting for " + label + ".");
    }

    private static void PumpOnce()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new System.Action(delegate { frame.Continue = false; }));
        Dispatcher.PushFrame(frame);
    }

    private static bool IsHostStartupComplete()
    {
        var current = Application.Current;
        if (current == null || current.MainWindow == null
            || !current.MainWindow.IsLoaded || !current.MainWindow.IsVisible
            || IsSplashWindow(current.MainWindow))
        {
            return false;
        }

        foreach (Window window in current.Windows)
        {
            if (window.IsVisible && IsSplashWindow(window)) return false;
        }

        return true;
    }

    private static bool IsSplashWindow(Window window)
    {
        var typeName = window.GetType().Name ?? string.Empty;
        var title = window.Title ?? string.Empty;
        return typeName.IndexOf("Splash", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("SplashScreenWindow", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Type ResolveType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName, false, false);
            if (type != null) return type;
        }
        throw new TypeLoadException(fullName);
    }

    private static object Get(object target, string property)
    {
        var value = TryGet(target, property);
        if (value == null) throw new InvalidOperationException(target.GetType().FullName + "." + property + " returned null.");
        return value;
    }

    private static object TryGet(object target, string property)
    {
        if (target == null) return null;
        var targetType = target.GetType();
        var info = targetType.GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (info != null && info.GetGetMethod(true) != null) return info.GetValue(target, null);
        foreach (var contract in targetType.GetInterfaces())
        {
            info = contract.GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
            if (info != null && info.GetGetMethod() != null) return info.GetValue(target, null);
        }
        return null;
    }

    private static object TryGetField(object target, string field)
    {
        if (target == null) return null;
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info != null) return info.GetValue(target);
        }
        return null;
    }

    private static string PrivateCollectionTypes(object target, string field)
    {
        var values = TryGetField(target, field) as IEnumerable;
        if (values == null) return "Unavailable";
        var types = AsObjects(values).Select(value => value == null ? "<null>" : value.GetType().FullName).ToList();
        return types.Count == 0 ? "<empty>" : string.Join(",", types);
    }

    private static void Set(object target, string property, object value)
    {
        var info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (info == null || !info.CanWrite) throw new MissingMemberException(target.GetType().FullName, property);
        info.SetValue(target, value, null);
    }

    private static void SetEnum(object target, string property, string value)
    {
        var info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (info == null || !info.CanWrite || !info.PropertyType.IsEnum) throw new MissingMemberException(target.GetType().FullName, property);
        info.SetValue(target, Enum.Parse(info.PropertyType, value, true), null);
    }

    private static void SetPublic(object target, string property, object value)
    {
        var info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
        if (info == null || !info.CanWrite) throw new MissingMemberException(target.GetType().FullName, property);
        info.SetValue(target, value, null);
    }

    private static void SetPublicEnum(object target, string property, string value)
    {
        var info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
        if (info == null || !info.CanWrite || !info.PropertyType.IsEnum) throw new MissingMemberException(target.GetType().FullName, property);
        info.SetValue(target, Enum.Parse(info.PropertyType, value, true), null);
    }

    private static object Invoke(object target, string method, params object[] arguments)
    {
        var candidates = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(item => item.Name == method && item.GetParameters().Length == arguments.Length)
            .ToList();
        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            var compatible = true;
            for (var index = 0; index < parameters.Length; index++)
            {
                if (arguments[index] != null && !parameters[index].ParameterType.IsInstanceOfType(arguments[index]))
                {
                    compatible = false;
                    break;
                }
            }
            if (compatible) return candidate.Invoke(target, arguments);
        }
        throw new MissingMethodException(target.GetType().FullName, method);
    }

    private static object TryInvoke(object target, string method, params object[] arguments)
    {
        if (target == null) return null;
        try { return Invoke(target, method, arguments); }
        catch { return null; }
    }

    private static object TryInvokeWithError(object target, string method, out string error, params object[] arguments)
    {
        error = string.Empty;
        if (target == null) return null;
        try { return Invoke(target, method, arguments); }
        catch (Exception exception)
        {
            var current = Unwrap(exception);
            error = current.GetType().FullName + ": " + current.Message;
            return null;
        }
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

    private static void RequireNewAbsolute(string path, bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new ArgumentException("A rooted path is required.");
        if (mustExist && !File.Exists(path)) throw new FileNotFoundException(path);
        if (!mustExist && File.Exists(path)) throw new IOException("Create-new output already exists: " + path);
    }

    private static void Trace(string text)
    {
        var line = ((DateTime.UtcNow - StartedUtc).TotalMilliseconds).ToString("F3", CultureInfo.InvariantCulture) + "\t" + Clean(text);
        lock (TraceGate)
        {
            TraceLines.Add(line);
            if (string.IsNullOrWhiteSpace(TracePath)) return;
            using (var stream = new FileStream(TracePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.WriteLine(line);
                writer.Flush();
            }
        }
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

    private static string Text(object value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool BoolValue(object value)
    {
        return value is bool && (bool)value;
    }

    private static string F(double value)
    {
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }

    private static string Clean(string value)
    {
        return (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
    }

    private static double Distance(Pose left, Pose right)
    {
        return Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2) + Math.Pow(left.Z - right.Z, 2));
    }

    private sealed class CycleResult
    {
        public bool Succeeded { get; set; }
        public Pose FinalPose { get; set; }
        public double MaxDisplacementMillimeters { get; set; }
        public object Handler { get; set; }
        public object Connector { get; set; }
        public string ResultLine { get; set; }
    }

    private sealed class NegativeResult
    {
        public bool Connected { get; set; }
        public string State { get; set; }
        public bool Succeeded { get; set; }
    }

    private sealed class RemoteOperatingLease : IDisposable
    {
        private readonly object operationContext;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Task pulseTask;
        private Exception pulseError;
        private bool disposed;

        public RemoteOperatingLease(object operationContext)
        {
            this.operationContext = operationContext;
            pulseTask = Task.Run(delegate
            {
                while (!cancellation.IsCancellationRequested)
                {
                    try
                    {
                        Invoke(operationContext, "Operate", -1.0d);
                        if (cancellation.Token.WaitHandle.WaitOne(500)) return;
                    }
                    catch (Exception exception)
                    {
                        pulseError = Unwrap(exception);
                        return;
                    }
                }
            });
        }

        public void ThrowIfFailed()
        {
            if (pulseError != null)
                throw new InvalidOperationException("KUKA.Sim remote operation pulse failed.", pulseError);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            cancellation.Cancel();
            try { pulseTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
            try
            {
                Invoke(operationContext, "Stop");
                var disposable = operationContext as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
            finally
            {
                cancellation.Dispose();
                Trace("REMOTE_OPERATION_STOPPED");
            }
        }
    }

    private struct Pose
    {
        public Pose(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X;
        public double Y;
        public double Z;
    }
}
