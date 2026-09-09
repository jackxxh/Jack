using System.Text;
using System.Security.Cryptography;
using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class KukaSimOfficeLiteVirtualLoopTests
{
    [Fact]
    public void OfficeLite_probe_preserves_vendor_null_operation_context_without_constructing_spoc_context()
    {
        using var stream = typeof(KukaSimOfficeLiteVirtualLoopRunner).Assembly.GetManifestResourceStream(
            KukaSimOfficeLiteVirtualLoopContract.EmbeddedKukaSimScriptResource);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var probe = reader.ReadToEnd();

        Assert.Contains("source=ServiceHostConnector.OperationContext", probe, StringComparison.Ordinal);
        Assert.Contains("NullSimulationOperationContext", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("source=ServiceHostConnector.userAccess", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("OnlineSimulationOperationContext\")", probe, StringComparison.Ordinal);
    }

    [Fact]
    public void OfficeLite_probe_uses_the_vendor_high_level_simulation_entry()
    {
        using var stream = typeof(KukaSimOfficeLiteVirtualLoopRunner).Assembly.GetManifestResourceStream(
            KukaSimOfficeLiteVirtualLoopContract.EmbeddedKukaSimScriptResource);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var probe = reader.ReadToEnd();

        Assert.Contains("Invoke(simulationControlModel, \"SwitchRunState\")", probe, StringComparison.Ordinal);
        Assert.Contains("KUKA_LAB_SYNCHRONIZED_LAYOUT_PATH", probe, StringComparison.Ordinal);
        Assert.Contains("SYNCHRONIZED_LAYOUT_LOAD", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("FilterControllerUploadSimulationExtension(simulationControl, component)", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("using (var controllerSyncFilter", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("simulationExtensionProvider", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("FilterSimulationExtensions", probe, StringComparison.Ordinal);
        Assert.Contains("BCO_HIGH_LEVEL_START", probe, StringComparison.Ordinal);
        Assert.Contains("native KSS BCO run after online simulation initialization", probe, StringComparison.Ordinal);
        Assert.Contains("PROGRAM_READY_AFTER_CONTROLLER_ATTACH", probe, StringComparison.Ordinal);
        Assert.Contains("SetPublic(config, \"ProgramModuleName\", requestedProgramModule)", probe, StringComparison.Ordinal);
        Assert.Contains("SetPublicEnum(config, \"ProgramStepMode\", \"Go\")", probe, StringComparison.Ordinal);
        Assert.Contains("VerifyControllerSelectedProgramAndGo(runtime, programPath)", probe, StringComparison.Ordinal);
        var postAttachVerifier = probe[probe.IndexOf("private static object VerifyControllerSelectedProgramAndGo", StringComparison.Ordinal)..];
        postAttachVerifier = postAttachVerifier[..postAttachVerifier.IndexOf("private static void TraceControllerDiagnostics", StringComparison.Ordinal)];
        Assert.DoesNotContain("Invoke(robot, \"Select\"", postAttachVerifier, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke(robot, \"Deselect\"", postAttachVerifier, StringComparison.Ordinal);
        Assert.Contains("TraceControllerDiagnostics(address, robot, \"BEFORE_HIGH_LEVEL_START\")", probe, StringComparison.Ordinal);
        Assert.True(
            probe.IndexOf("SetPublic(config, \"ProgramModuleName\", requestedProgramModule)", StringComparison.Ordinal)
            < probe.IndexOf("Invoke(simulationControlModel, \"SwitchRunState\")", StringComparison.Ordinal));
        Assert.True(
            probe.IndexOf("SIMULATION_INITIALIZED cycle=", StringComparison.Ordinal)
            < probe.IndexOf("PROGRAM_READY_AFTER_CONTROLLER_ATTACH", StringComparison.Ordinal));
        Assert.True(
            probe.IndexOf("PROGRAM_READY_AFTER_CONTROLLER_ATTACH", StringComparison.Ordinal)
            < probe.IndexOf("BCO_HIGH_LEVEL_START cycle=", StringComparison.Ordinal));
        Assert.Contains("\"$MODE_OP\", \"$COULD_START_MOTION\", \"$DRIVES_ON\", \"$ON_PATH\"", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeSimulationWithoutModal", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke(simulationService, \"SwitchRunState\"", probe, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkVisual_prepare_deselects_a_retained_program_before_the_isolated_transaction()
    {
        using var stream = typeof(KukaSimOfficeLiteVirtualLoopRunner).Assembly.GetManifestResourceStream(
            KukaSimOfficeLiteVirtualLoopContract.EmbeddedWorkVisualScriptResource);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var probe = reader.ReadToEnd();

        Assert.Contains("PREPARE_SELECTED_BEFORE=", probe, StringComparison.Ordinal);
        Assert.Contains("PREPARE_MODE_BEFORE=", probe, StringComparison.Ordinal);
        Assert.Contains("PREPARE_T1_VERIFIED=", probe, StringComparison.Ordinal);
        Assert.Contains("new TimestampedValue(\"#T1\")", probe, StringComparison.Ordinal);
        Assert.Contains("robot.Deselect();", probe, StringComparison.Ordinal);
        Assert.Contains("PREPARE_DESELECTED=", probe, StringComparison.Ordinal);
        Assert.True(
            probe.IndexOf("robot.Deselect();", StringComparison.Ordinal)
            < probe.IndexOf("repository.CreateDirectory(transactionRoot);", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkVisual_transaction_waits_for_the_RuntimeManager_application_not_only_its_TCP_port()
    {
        using var stream = typeof(KukaSimOfficeLiteVirtualLoopRunner).Assembly.GetManifestResourceStream(
            KukaSimOfficeLiteVirtualLoopContract.EmbeddedWorkVisualScriptResource);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var probe = reader.ReadToEnd();

        Assert.Contains("GetRobotInterpreterAfterWarmup", probe, StringComparison.Ordinal);
        Assert.Contains("DateTime.UtcNow.AddSeconds(180)", probe, StringComparison.Ordinal);
        Assert.Contains("RUNTIME_MANAGER_WARMUP_RETRY=", probe, StringComparison.Ordinal);
        Assert.Contains("new RuntimeManagerFacade(controllerAddress", probe, StringComparison.Ordinal);
        Assert.Contains("Thread.Sleep(5000)", probe, StringComparison.Ordinal);
        Assert.DoesNotContain("IContextChannel", probe, StringComparison.Ordinal);
    }

    [Fact]
    public void Official_loop_preflights_the_probe_and_uses_the_public_attach_contract()
    {
        var labRoot = TestEnvironment.FindLabRoot();
        var runner = File.ReadAllText(Path.Combine(labRoot, "tools", "exact-c01-official-loop.ps1"));
        using var probeStream = typeof(KukaSimOfficeLiteVirtualLoopRunner).Assembly.GetManifestResourceStream(
            KukaSimOfficeLiteVirtualLoopContract.EmbeddedKukaSimScriptResource);
        Assert.NotNull(probeStream);
        using var probeReader = new StreamReader(probeStream!);
        var probe = probeReader.ReadToEnd();

        Assert.Contains("Invoke-ProbeCompilePreflight", runner, StringComparison.Ordinal);
        Assert.Contains("probe-compile-preflight-", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("official-sync-dialog", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Process", runner, StringComparison.Ordinal);
        Assert.Contains("PumpUntil(IsHostStartupComplete", probe, StringComparison.Ordinal);
        Assert.Contains("IsSplashWindow", probe, StringComparison.Ordinal);
        Assert.Contains("HOST_STARTUP_COMPLETE", probe, StringComparison.Ordinal);
        Assert.Contains("current.MainWindow.IsLoaded", probe, StringComparison.Ordinal);
        Assert.Contains("current.MainWindow.IsVisible", probe, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.ApplicationIdle", probe, StringComparison.Ordinal);
        Assert.Contains("current.Dispatcher.BeginInvoke", probe, StringComparison.Ordinal);
        Assert.Contains("OnlineConnectionMode\", \"ViewOnly", probe, StringComparison.Ordinal);
        Assert.Contains("OnlineSimulationMode\", \"Attach", probe, StringComparison.Ordinal);
        Assert.Contains("IControllerConnector.Connect", probe, StringComparison.Ordinal);
        Assert.True(
            probe.IndexOf("current.Dispatcher.BeginInvoke", StringComparison.Ordinal)
            < probe.IndexOf("private static void Run()", StringComparison.Ordinal));
    }

    [Fact]
    public void Ready_loop_uses_go_high_level_start_twice_per_cycle_and_restores_environment()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var officeLite = ReadyOfficeLite(time, environment.VmxPath);
        var workVisual = new QueueWorkVisualPlatform(Cleanup(), Prepare(), Cleanup());
        var kukaSim = new FakeKukaSimPlatform();

        var outcome = new KukaSimOfficeLiteVirtualLoopRunner(officeLite, workVisual, kukaSim, time, environment.HashForTest)
            .Run(environment.Request, "test-kukasim-officelite-loop-ready");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, outcome.ExitCode);
        Assert.True(outcome.Receipt.Payload.CompositeConnectionValidated);
        Assert.True(outcome.Receipt.Payload.DisconnectReconnectValidated);
        Assert.True(outcome.Receipt.Payload.FailureInjectionValidated);
        Assert.True(outcome.Receipt.Payload.HighLevelStartValidated);
        Assert.Equal(environment.Request.SynchronizedLayoutPath, outcome.Receipt.Payload.SynchronizedLayoutPath);
        Assert.Contains(outcome.Receipt.Payload.Files, file =>
            file.Id == "controller-synchronized-layout" && file.Exists);
        Assert.True(outcome.Receipt.Payload.TemporaryProbeScriptsRemoved);
        Assert.False(File.Exists(outcome.Receipt.Payload.MaterializedKukaSimScriptPath));
        Assert.False(File.Exists(outcome.Receipt.Payload.MaterializedWorkVisualScriptPath));
        Assert.True(officeLite.SnapshotRestored);
        Assert.False(officeLite.Running);
        Assert.Equal(3, workVisual.Invocations.Count);
        Assert.Equal(3, kukaSim.Invocations.Count);
        Assert.Equal(["valid:1", "valid:2", "negative:0"], kukaSim.Invocations);
        Assert.Equal(3, outcome.Receipt.Payload.KukaSimCommands.Count);
        Assert.Contains("-action=cleanup", workVisual.Invocations[0]);
        Assert.Contains("-action=prepare", workVisual.Invocations[1]);
        Assert.Contains("-action=cleanup", workVisual.Invocations[2]);
        Assert.True(outcome.Receipt.Payload.WorkVisual.PreCleanupVerified);
        Assert.Equal(2, outcome.Receipt.Payload.Result!.Cycles.Count);
        Assert.All(outcome.Receipt.Payload.Result.Cycles, cycle =>
        {
            Assert.Equal(2, cycle.StartCount);
            Assert.Equal("Go", cycle.Mode);
            Assert.Equal("End", cycle.State);
        });
        Assert.True(KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(outcome.Receipt, environment.HashForTest).Succeeded);
    }

    [Fact]
    public void Wrong_start_count_fails_without_promoting_virtual_loop()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var outcome = new KukaSimOfficeLiteVirtualLoopRunner(
            ReadyOfficeLite(time, environment.VmxPath),
            new QueueWorkVisualPlatform(Cleanup(), Prepare(), Cleanup()),
            new FakeKukaSimPlatform(startCount: 1),
            time, environment.HashForTest).Run(environment.Request, "test-kukasim-officelite-loop-start-count");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.HighLevelStartValidated);
        Assert.False(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.True(outcome.Receipt.Payload.TemporaryProbeScriptsRemoved);
        Assert.True(KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(outcome.Receipt, environment.HashForTest).Succeeded);
    }

    [Fact]
    public void Negative_endpoint_connecting_is_failed_closed()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var outcome = new KukaSimOfficeLiteVirtualLoopRunner(
            ReadyOfficeLite(time, environment.VmxPath),
            new QueueWorkVisualPlatform(Cleanup(), Prepare(), Cleanup()),
            new FakeKukaSimPlatform(negativeConnected: true),
            time, environment.HashForTest).Run(environment.Request, "test-kukasim-officelite-loop-negative");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.FailureInjectionValidated);
        Assert.True(outcome.Receipt.Payload.TemporaryProbeScriptsRemoved);
        Assert.True(KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(outcome.Receipt, environment.HashForTest).Succeeded);
    }

    [Fact]
    public void Missing_scene_synchronization_is_failed_closed()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var outcome = new KukaSimOfficeLiteVirtualLoopRunner(
            ReadyOfficeLite(time, environment.VmxPath),
            new QueueWorkVisualPlatform(Cleanup(), Prepare(), Cleanup()),
            new FakeKukaSimPlatform(sceneSynchronized: false),
            time, environment.HashForTest).Run(environment.Request, "test-kukasim-officelite-loop-scene-sync");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.CompositeConnectionValidated);
        Assert.True(KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(outcome.Receipt, environment.HashForTest).Succeeded);
    }

    [Fact]
    public void Missing_explicit_disconnect_is_failed_closed()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var outcome = new KukaSimOfficeLiteVirtualLoopRunner(
            ReadyOfficeLite(time, environment.VmxPath),
            new QueueWorkVisualPlatform(Cleanup(), Prepare(), Cleanup()),
            new FakeKukaSimPlatform(canDisconnect: false),
            time, environment.HashForTest).Run(environment.Request, "test-kukasim-officelite-loop-disconnect");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.DisconnectReconnectValidated);
        Assert.True(KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(outcome.Receipt, environment.HashForTest).Succeeded);
    }

    [Fact]
    public void Forced_kukasim_cleanup_is_failed_closed()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var outcome = new KukaSimOfficeLiteVirtualLoopRunner(
            ReadyOfficeLite(time, environment.VmxPath),
            new QueueWorkVisualPlatform(Cleanup(), Prepare(), Cleanup()),
            new FakeKukaSimPlatform(forcedTerminationUsed: true),
            time, environment.HashForTest).Run(environment.Request, "test-kukasim-officelite-loop-forced-cleanup");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.EnvironmentReusable);
        Assert.All(outcome.Receipt.Payload.KukaSimCommands, command => Assert.True(command.ForcedTerminationUsed));
        Assert.True(KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(outcome.Receipt, environment.HashForTest).Succeeded);
    }

    [Fact]
    public void Rehashed_ready_receipt_tamper_is_rejected()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var receipt = new KukaSimOfficeLiteVirtualLoopRunner(
            ReadyOfficeLite(time, environment.VmxPath),
            new QueueWorkVisualPlatform(Cleanup(), Prepare(), Cleanup()),
            new FakeKukaSimPlatform(),
            time, environment.HashForTest).Run(environment.Request, "test-kukasim-officelite-loop-tamper").Receipt;
        var tamperedResult = receipt.Payload.Result! with
        {
            Cycles = receipt.Payload.Result.Cycles.Select((cycle, index) => index == 0
                ? cycle with { StartCount = 1 }
                : cycle).ToList()
        };
        var tamperedPayload = receipt.Payload with
        {
            Result = tamperedResult,
            ResultCanonicalSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedResult)
        };
        var tampered = receipt with
        {
            Payload = tamperedPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedPayload)
        };

        Assert.False(KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(tampered, environment.HashForTest).Succeeded);
    }

    [Fact]
    public void Error_only_kukasim_result_remains_json_serializable_and_failed_closed()
    {
        using var environment = TestEnvironment.Create();
        var time = new ManualTimeProvider();
        var outcome = new KukaSimOfficeLiteVirtualLoopRunner(
            ReadyOfficeLite(time, environment.VmxPath),
            new QueueWorkVisualPlatform(Cleanup(), Prepare(), Cleanup()),
            new ErrorKukaSimPlatform(),
            time, environment.HashForTest).Run(environment.Request, "test-kukasim-officelite-loop-error-result");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.NotNull(outcome.Receipt.Payload.Result);
        Assert.Null(outcome.Receipt.Payload.Result!.RepeatabilityMillimeters);
        Assert.Contains("component load failed", outcome.Receipt.Payload.Result.ErrorMessage, StringComparison.Ordinal);
        var json = ReceiptSerialization.ToJson(outcome.Receipt);
        Assert.Contains("\"repeatabilityMillimeters\": null", json, StringComparison.Ordinal);
        Assert.True(KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(outcome.Receipt, environment.HashForTest).Succeeded);
    }

    [Fact]
    public void Missing_controller_synchronized_layout_blocks_before_vendor_side_effects()
    {
        using var environment = TestEnvironment.Create();
        File.Delete(environment.Request.SynchronizedLayoutPath);
        var time = new ManualTimeProvider();
        var officeLite = ReadyOfficeLite(time, environment.VmxPath);
        var workVisual = new QueueWorkVisualPlatform(Prepare(), Cleanup());
        var kukaSim = new FakeKukaSimPlatform();

        var outcome = new KukaSimOfficeLiteVirtualLoopRunner(
            officeLite, workVisual, kukaSim, time, environment.HashForTest)
            .Run(environment.Request, "test-kukasim-officelite-loop-missing-layout");

        Assert.Equal(EnvironmentTerminalClassification.Blocked, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(officeLite.Running);
        Assert.Empty(workVisual.Invocations);
        Assert.Empty(kukaSim.Invocations);
        Assert.Contains(outcome.Receipt.Payload.Checks, check =>
            check.Id == "controller-synchronized-layout" && check.Status == EnvironmentCheckStatus.Blocked);
        Assert.True(KukaSimOfficeLiteVirtualLoopReceiptVerifier.Verify(outcome.Receipt, environment.HashForTest).Succeeded);
    }

    private static FakeOfficeLitePlatform ReadyOfficeLite(ManualTimeProvider time, string vmxPath) => new(time, vmxPath)
    {
        ProbeReady = true,
        OpenServicePorts = OfficeLiteServiceDiagnosticContract.WorkVisualServicePorts.ToHashSet()
    };

    private static WorkVisualProcessResult Prepare() => new(
        0, false, true, 10,
        string.Join("\r\n", [
            "KSS_COMPOSITION_TRANSACTION_BEGIN=True",
            "UPLOAD_VERIFIED=True",
            "GO_CONFIRMED=True",
            "NO_RELATED_ERRORS=True",
            "PREPARE_VERIFIED=True",
            string.Empty]),
        string.Empty);

    private static WorkVisualProcessResult Cleanup() => new(
        0, false, true, 10,
        "KSS_COMPOSITION_TRANSACTION_BEGIN=True\r\nCLEANUP_VERIFIED=True\r\n",
        string.Empty);

    private sealed class QueueWorkVisualPlatform(params WorkVisualProcessResult[] results) : IWorkVisualRunnerPlatform
    {
        private readonly Queue<WorkVisualProcessResult> _results = new(results);
        public List<List<string>> Invocations { get; } = [];
        public WorkVisualProcessResult Run(string executablePath, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout)
        {
            Invocations.Add(arguments.ToList());
            return _results.Dequeue();
        }
    }

    private sealed class FakeKukaSimPlatform(
        int startCount = 2,
        bool negativeConnected = false,
        bool sceneSynchronized = true,
        bool canDisconnect = true,
        bool forcedTerminationUsed = false)
        : IKukaSimOfficeLiteVirtualLoopPlatform
    {
        public List<string> Invocations { get; } = [];
        public IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(string launcherPath, string enginePath) => [];

        public KukaSimProcessResult Run(KukaSimOfficeLiteVirtualLoopProcessRequest request)
        {
            Invocations.Add(request.Phase + ":" + request.Cycle);
            var cycleSuccess = startCount == 2;
            var lines = new List<string>
            {
                "APPLICATION\tTrue\tTrue\tTrue",
                "COMPONENT\tKR 210 R2700-2 C01\tTrue\t2\tFlangeNode",
                "CONTRACT\tController\tViewOnly\tAttach\tHighLevelStart"
            };
            var overallSuccess = request.Phase == "valid" ? cycleSuccess : !negativeConnected;
            if (request.Phase == "valid")
            {
                lines.Add(Cycle(request.Cycle, startCount, cycleSuccess && sceneSynchronized, sceneSynchronized, 365));
                lines.Add($"DISCONNECT\t{request.Cycle}\tState=NoConnection\tMethod=IControllerConnector.Disconnect\tCanDisconnect={canDisconnect}\tSuccess={canDisconnect}");
            }
            else
            {
                lines.Add($"NEGATIVE_ENDPOINT\tConnected={negativeConnected}\tState=NoConnection\tSuccess=True");
            }
            lines.Add($"SUCCESS\t{overallSuccess}");
            File.WriteAllLines(request.ResultPath, lines, new UTF8Encoding(false));
            File.WriteAllText(request.TracePath, "0\tENTERED\r\n1\tHIGH_LEVEL_START cycle=1 count=1\r\n", new UTF8Encoding(false));
            return new KukaSimProcessResult(overallSuccess ? 0 : 4, true, false, forcedTerminationUsed, true, 50, [4321], string.Empty);
        }

        private static string Cycle(int cycle, int count, bool success, bool sceneSynchronized, double x) =>
            $"CYCLE\t{cycle}\tConnected=True\tInterface=ServiceHost\tActive=VRC\tStartCount={count}" +
            $"\tMode=Go\tState=End\tDeselected=True\tSimulationInitialized=True\tCanUpdatePosition=True" +
            $"\tSceneSynchronized={sceneSynchronized}\tMaxDisplacementMm=10\tFinal={x},0,625\tSuccess={success}";
    }

    private sealed class ErrorKukaSimPlatform : IKukaSimOfficeLiteVirtualLoopPlatform
    {
        public IReadOnlyList<KukaSimProcessObservation> FindRunningProcesses(string launcherPath, string enginePath) => [];

        public KukaSimProcessResult Run(KukaSimOfficeLiteVirtualLoopProcessRequest request)
        {
            File.WriteAllText(
                request.ResultPath,
                "ERROR\tSystem.InvalidOperationException\tcomponent load failed\r\n",
                new UTF8Encoding(false));
            File.WriteAllText(request.TracePath, "0\tENTERED\r\n1\tERROR component load failed\r\n", new UTF8Encoding(false));
            return new KukaSimProcessResult(2, true, false, false, true, 50, [4322], string.Empty);
        }
    }

    private sealed record FakeOfficeLitePlatform(ManualTimeProvider Time, string VmxPath) : IOfficeLiteHostPlatform
    {
        public bool Running { get; set; }
        public bool ProbeReady { get; init; }
        public HashSet<int> OpenServicePorts { get; init; } = [];
        public bool SnapshotRestored { get; private set; }

        public VmrunExecutionResult RunVmrun(string vmrunPath, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            var command = arguments.Count >= 3 ? arguments[2] : string.Empty;
            return command switch
            {
                "listSnapshots" => Result($"Total snapshots: 1{Environment.NewLine}{KukaSimOfficeLiteVirtualLoopContract.DefaultSnapshotName}"),
                "list" => Result(Running ? $"Total running VMs: 1{Environment.NewLine}{VmxPath}" : "Total running VMs: 0"),
                "start" => Start(),
                "stop" => Stop(),
                "revertToSnapshot" => Revert(),
                _ => new VmrunExecutionResult(1, false, 1, string.Empty, "unsupported")
            };
        }

        public string? TryResolveDhcpAddress(string vmxPath, string dhcpLeasePath) => "198.51.100.128";
        public GuestProbeResult ProbeGuest(string? address, int port, int timeoutMilliseconds, DateTimeOffset observedAtUtc) =>
            ProbeReady
                ? new GuestProbeResult(0, observedAtUtc, address, true, true, "ready", new GuestTlsIdentity(
                    address!, "KUKA Roboter GmbH", "KUKA Roboter GmbH", "4DCFB182A3EF5E53FC908D7999273595E02BC430", "Tls12", true))
                : new GuestProbeResult(0, observedAtUtc, address, true, false, "not-ready", null);
        public IReadOnlyList<GuestTcpPortProbeResult> ProbeTcpPorts(string? address, IReadOnlyList<int> ports, int timeoutMilliseconds, DateTimeOffset observedAtUtc) =>
            ports.Select(port => new GuestTcpPortProbeResult(port, observedAtUtc,
                OpenServicePorts.Contains(port) ? GuestTcpPortState.Open : GuestTcpPortState.Timeout,
                OpenServicePorts.Contains(port) ? "Open" : "Timeout", 1)).ToList();
        public void Delay(TimeSpan duration) => Time.Advance(duration);
        private VmrunExecutionResult Start() { Running = true; return Result(string.Empty); }
        private VmrunExecutionResult Stop() { Running = false; return Result(string.Empty); }
        private VmrunExecutionResult Revert() { SnapshotRestored = true; return Result(string.Empty); }
        private static VmrunExecutionResult Result(string output) => new(0, false, 1, output, string.Empty);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class TestEnvironment : IDisposable
    {
        private TestEnvironment(string root, string vmxPath, KukaSimOfficeLiteVirtualLoopRequest request)
        {
            Root = root;
            VmxPath = vmxPath;
            Request = request;
        }
        public string Root { get; }
        public string VmxPath { get; }
        public KukaSimOfficeLiteVirtualLoopRequest Request { get; }

        public static TestEnvironment Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "kuka-lab-virtual-loop-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var vmrun = Path.Combine(root, "vmrun.exe");
            File.WriteAllText(vmrun, "synthetic vmrun");
            var installRoot = Path.Combine(root, "KUKA.Sim 4.10.2");
            Directory.CreateDirectory(Path.Combine(installRoot, "KUKA"));
            var engine = Path.Combine(installRoot, "VisualComponents.Engine.exe");
            var launcher = Path.Combine(installRoot, "VisualComponents.Engine.Launcher.exe");
            var bootstrap = Path.Combine(installRoot, KukaSimInstallationDiscovery.BootstrapPluginFileName);
            var online = Path.Combine(installRoot, "KUKA", "Kuka.Sim.Programming.Online.dll");
            var wrapper = Path.Combine(installRoot, "VisualComponents.KRC.VRCWrapper.dll");
            var component = Path.Combine(root, "components", "KR 210 R2700-2 C01.vcmx");
            var synchronizedLayout = Path.Combine(root, "layouts", "exact-c01-controller-synchronized.vcmx");
            var runner = Path.Combine(root, "wvsr.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(component)!);
            Directory.CreateDirectory(Path.GetDirectoryName(synchronizedLayout)!);
            foreach (var file in new[] { engine, launcher, bootstrap, online, wrapper, component, synchronizedLayout, runner })
            {
                File.WriteAllText(file, "synthetic " + Path.GetFileName(file));
            }
            var vmx = Path.Combine(root, "OfficeLite-Work", "8.7.8-build04", "runs", "kr210-c01-item9", "KR210-C01-Item9.vmx");
            Directory.CreateDirectory(Path.GetDirectoryName(vmx)!);
            File.WriteAllLines(vmx,
            [
                "ethernet0.address = \"00:0C:29:3F:C7:D7\"",
                "isolation.tools.hgfs.disable = \"TRUE\"",
                "sharedFolder0.present = \"FALSE\"",
                "sharedFolder0.enabled = \"FALSE\"",
                "sharedFolder0.readAccess = \"FALSE\"",
                "sharedFolder0.writeAccess = \"FALSE\"",
                "sharedFolder.maxNum = \"0\"",
                "hgfs.mapRootShare = \"FALSE\""
            ]);
            var labRoot = FindLabRoot();
            var request = KukaSimOfficeLiteVirtualLoopRequest.CreateDefault(
                root, labRoot, Path.Combine(root, "evidence"), true, "test-authorization",
                vmrunPath: vmrun, vmxPath: vmx, runnerPath: runner, enginePath: engine, componentPath: component,
                synchronizedLayoutPath: synchronizedLayout,
                readinessTimeoutSeconds: 5,
                serviceObservationSeconds: 1, runnerTimeoutSeconds: 5, kukaSimTimeoutSeconds: 30) with
            {
                OfficeLite = OfficeLiteCycleRequest.CreateDefault(root, vmrun, readinessTimeoutSeconds: 5) with
                {
                    VmxPath = vmx,
                    ProbeIntervalMilliseconds = 500,
                    ShutdownTimeoutSeconds = 2,
                    DiagnoseWorkVisualServices = true,
                    ServiceObservationSeconds = 1,
                    ServiceProbeIntervalMilliseconds = 500,
                    ServiceConnectTimeoutMilliseconds = 250
                }
            };
            return new TestEnvironment(root, vmx, request);
        }

        public string HashForTest(string path)
        {
            return Path.GetFileName(path) switch
            {
                "Kuka.Sim.Programming.Online.dll" => KukaSimOfficeLiteVirtualLoopContract.OnlineAssemblySha256,
                "VisualComponents.KRC.VRCWrapper.dll" => KukaSimOfficeLiteVirtualLoopContract.VrcWrapperSha256,
                "KR 210 R2700-2 C01.vcmx" => KukaSimOfficeLiteVirtualLoopContract.ExactC01ComponentSha256,
                _ => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
            };
        }

        internal static string FindLabRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "fixtures", "minimal-ptp-lin-valid", "controller-files", "LAB_MINIMAL.src"))) return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate 04_kuka_lab root.");
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
