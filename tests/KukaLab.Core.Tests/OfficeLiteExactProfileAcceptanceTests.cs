using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class OfficeLiteExactProfileAcceptanceTests
{
    [Fact]
    public void Matching_runtime_download_and_active_baseline_are_accepted_against_trusted_controller_baseline()
    {
        var trusted = CreateBaselineReceipt("trusted", CreateExactBaseline());
        var active = CreateBaselineReceipt("active", CreateExactBaseline());
        var runner = new OfficeLiteExactProfileAcceptanceRunner(new AcceptingInputVerifier());

        var outcome = runner.Run(
            new OfficeLiteExactProfileAcceptanceRequest
            {
                ProfileReadbackReceipt = CreateProfileReceipt(),
                ActiveProjectDownloadReceipt = CreateDownloadReceipt(),
                ActiveControllerBaselineReceipt = active,
                TrustedControllerBaselineReceipt = trusted
            },
            "test-exact-profile-accepted");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, outcome.ExitCode);
        Assert.True(outcome.Receipt.Payload.Summary.Accepted);
        Assert.True(outcome.Receipt.Payload.Summary.RuntimeRobotIdentityMatched);
        Assert.True(outcome.Receipt.Payload.Summary.RuntimeKssMatched);
        Assert.True(outcome.Receipt.Payload.Summary.ActiveProjectMatched);
        Assert.True(outcome.Receipt.Payload.Summary.DownloadedProjectBound);
        Assert.True(outcome.Receipt.Payload.Summary.MachineDataIdentityMatched);
        Assert.True(outcome.Receipt.Payload.Summary.CabinetMatched);
        Assert.True(outcome.Receipt.Payload.Summary.AxisLimitsMatched);
        Assert.True(outcome.Receipt.Payload.Summary.DetailedAxesMatched);
        Assert.True(outcome.Receipt.Payload.Summary.ToolDataMatched);
        Assert.True(outcome.Receipt.Payload.Summary.BaseDataMatched);
        Assert.True(outcome.Receipt.Payload.Summary.LoadDataMatched);
        Assert.True(OfficeLiteExactProfileAcceptanceReceiptVerifier.Verify(
            outcome.Receipt,
            reverifyInputs: false).Succeeded);
    }

    [Fact]
    public void Vendor_frame_round_trip_sub_micro_drift_is_accepted()
    {
        var request = CreateExactRequest();
        var activeProfile = request.ActiveControllerBaselineReceipt.Payload.Baseline!.ControllerProfile;
        request = WithActiveProfile(request, activeProfile with
        {
            ToolData = activeProfile.ToolData
                .Select((value, index) => index == 0 ? value with { X = value.X + 0.0000005 } : value)
                .ToList()
        });
        var runner = new OfficeLiteExactProfileAcceptanceRunner(new AcceptingInputVerifier());

        var outcome = runner.Run(request, "test-exact-profile-vendor-frame-round-trip");

        Assert.Equal(EnvironmentTerminalClassification.Ready, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(0, outcome.ExitCode);
        Assert.True(outcome.Receipt.Payload.Summary.ToolDataMatched);
        Assert.True(outcome.Receipt.Payload.Summary.Accepted);
    }

    [Fact]
    public void Vendor_frame_round_trip_above_micro_tolerance_is_rejected()
    {
        var request = CreateExactRequest();
        var activeProfile = request.ActiveControllerBaselineReceipt.Payload.Baseline!.ControllerProfile;
        request = WithActiveProfile(request, activeProfile with
        {
            ToolData = activeProfile.ToolData
                .Select((value, index) => index == 0 ? value with { X = value.X + 0.000002 } : value)
                .ToList()
        });
        var runner = new OfficeLiteExactProfileAcceptanceRunner(new AcceptingInputVerifier());

        var outcome = runner.Run(request, "test-exact-profile-vendor-frame-drift");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(2, outcome.ExitCode);
        Assert.False(outcome.Receipt.Payload.Summary.ToolDataMatched);
        Assert.False(outcome.Receipt.Payload.Summary.Accepted);
    }

    [Theory]
    [InlineData("runtime-robot", "runtime-robot-identity")]
    [InlineData("runtime-kss", "runtime-kss")]
    [InlineData("active-project", "active-project")]
    [InlineData("download-binding", "download-baseline-binding")]
    [InlineData("machine-data", "machine-data-identity")]
    [InlineData("cabinet", "cabinet")]
    [InlineData("controller-software", "controller-software")]
    [InlineData("axis-limits", "axis-limits")]
    [InlineData("tool-data", "tool-data")]
    [InlineData("base-data", "base-data")]
    [InlineData("load-data", "load-data")]
    [InlineData("kukasim-component", "kukasim-component")]
    public void Each_required_profile_dimension_fails_closed_when_it_drifts(
        string scenario,
        string expectedFailedCheck)
    {
        var request = MutateExactRequest(CreateExactRequest(), scenario);
        var runner = new OfficeLiteExactProfileAcceptanceRunner(new AcceptingInputVerifier());

        var outcome = runner.Run(request, $"test-exact-profile-{scenario}");

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.False(outcome.Receipt.Payload.Summary.Accepted);
        Assert.Contains(outcome.Receipt.Payload.Checks, check =>
            check.Id == expectedFailedCheck && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(OfficeLiteExactProfileAcceptanceReceiptVerifier.Verify(
            outcome.Receipt,
            reverifyInputs: false).Succeeded);
    }

    [Fact]
    public void Axis_drift_rejects_exact_profile_even_when_names_match()
    {
        var trustedBaseline = CreateExactBaseline();
        var activeBaseline = trustedBaseline with
        {
            Axes = trustedBaseline.Axes
                .Select(axis => axis.AxisNumber == 2
                    ? axis with { PositiveSoftwareLimitDegrees = axis.PositiveSoftwareLimitDegrees + 1 }
                    : axis)
                .ToList()
        };
        var runner = new OfficeLiteExactProfileAcceptanceRunner(new AcceptingInputVerifier());

        var outcome = runner.Run(
            new OfficeLiteExactProfileAcceptanceRequest
            {
                ProfileReadbackReceipt = CreateProfileReceipt(),
                ActiveProjectDownloadReceipt = CreateDownloadReceipt(),
                ActiveControllerBaselineReceipt = CreateBaselineReceipt("active-drift", activeBaseline),
                TrustedControllerBaselineReceipt = CreateBaselineReceipt("trusted", trustedBaseline)
            },
            "test-exact-profile-axis-drift");

        Assert.Equal(EnvironmentTerminalClassification.Failed, outcome.Receipt.Payload.TerminalClassification);
        Assert.Equal(2, outcome.ExitCode);
        Assert.False(outcome.Receipt.Payload.Summary.Accepted);
        Assert.False(outcome.Receipt.Payload.Summary.DetailedAxesMatched);
        Assert.Contains(outcome.Receipt.Payload.Checks, check =>
            check.Id == "detailed-axis-data" && check.Status == EnvironmentCheckStatus.Failed);
        Assert.True(OfficeLiteExactProfileAcceptanceReceiptVerifier.Verify(
            outcome.Receipt,
            reverifyInputs: false).Succeeded);
    }

    [Fact]
    public void Receipt_verifier_rejects_rehashed_summary_tamper()
    {
        var runner = new OfficeLiteExactProfileAcceptanceRunner(new AcceptingInputVerifier());
        var receipt = runner.Run(
            new OfficeLiteExactProfileAcceptanceRequest
            {
                ProfileReadbackReceipt = CreateProfileReceipt(),
                ActiveProjectDownloadReceipt = CreateDownloadReceipt(),
                ActiveControllerBaselineReceipt = CreateBaselineReceipt("active", CreateExactBaseline()),
                TrustedControllerBaselineReceipt = CreateBaselineReceipt("trusted", CreateExactBaseline())
            },
            "test-exact-profile-tamper").Receipt;
        var tamperedPayload = receipt.Payload with
        {
            Summary = receipt.Payload.Summary with { RuntimeRobotIdentity = "KR 3 R540" }
        };
        var tampered = receipt with
        {
            Payload = tamperedPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(tamperedPayload)
        };

        var verification = OfficeLiteExactProfileAcceptanceReceiptVerifier.Verify(
            tampered,
            reverifyInputs: false);

        Assert.False(verification.Succeeded);
        Assert.Contains(verification.Errors, error => error.Contains("summary", StringComparison.Ordinal));
    }

    private static OfficeLiteControllerProfileReadbackReceipt CreateProfileReceipt()
    {
        var lifecyclePayload = new OfficeLiteCyclePayload
        {
            ReceiptId = "cycle",
            TerminalClassification = EnvironmentTerminalClassification.Ready,
            GuestEndpoint = new GuestEndpointObservation { Address = "198.51.100.140" },
            WorkVisualServices = new WorkVisualServiceObservation { DeviceInfoEverOpen = true },
            CleanShutdownVerified = true
        };
        var lifecycle = new OfficeLiteCycleReceipt
        {
            Payload = lifecyclePayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(lifecyclePayload)
        };
        var payload = new OfficeLiteControllerProfileReadbackPayload
        {
            ReceiptId = "profile",
            TerminalClassification = EnvironmentTerminalClassification.Ready,
            EmbeddedScriptSha256 = OfficeLiteControllerProfileReadbackContract.EmbeddedScriptSha256,
            LifecycleReceipt = lifecycle,
            NegativeControlCommand = new WorkVisualRunnerCommandObservation { ExitCode = 42, CleanupVerified = true },
            LiveCommand = new WorkVisualRunnerCommandObservation { ExitCode = 0, CleanupVerified = true },
            Profile = new OfficeLiteControllerProfileObservation
            {
                Attempted = true,
                ControllerAddress = "198.51.100.140",
                RobotType = "#KR210R2700_2 C01 FLR",
                KssVersion = "V8.7.8.671",
                CurrentProjectName = OfficeLiteExactProfileAcceptanceContract.DefaultExpectedProjectName,
                ActiveProject = OfficeLiteExactProfileAcceptanceContract.DefaultExpectedProjectName,
                BaseProject = "BaseProject",
                InitialProject = "InitialProject",
                ProjectCount = 2
            },
            EnvironmentReusable = true,
            UnsupportedGaps = OfficeLiteControllerProfileReadbackContract.RequiredUnsupportedGaps.ToList()
        };
        return new OfficeLiteControllerProfileReadbackReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
    }

    private static OfficeLiteActiveProjectDownloadReceipt CreateDownloadReceipt()
    {
        var lifecyclePayload = new OfficeLiteCyclePayload
        {
            ReceiptId = "download-cycle",
            TerminalClassification = EnvironmentTerminalClassification.Ready,
            GuestEndpoint = new GuestEndpointObservation { Address = "198.51.100.140" },
            WorkVisualServices = new WorkVisualServiceObservation { DeviceInfoEverOpen = true },
            CleanShutdownVerified = true
        };
        var lifecycle = new OfficeLiteCycleReceipt
        {
            Payload = lifecyclePayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(lifecyclePayload)
        };
        var payload = new OfficeLiteActiveProjectDownloadPayload
        {
            ReceiptId = "download",
            TerminalClassification = EnvironmentTerminalClassification.Ready,
            EmbeddedScriptSha256 = OfficeLiteActiveProjectDownloadContract.EmbeddedScriptSha256,
            LifecycleReceipt = lifecycle,
            NegativeControlCommand = new WorkVisualRunnerCommandObservation { ExitCode = 42, CleanupVerified = true },
            LiveCommand = new WorkVisualRunnerCommandObservation { ExitCode = 0, CleanupVerified = true },
            ActiveProject = OfficeLiteExactProfileAcceptanceContract.DefaultExpectedProjectName,
            ProjectCount = 2,
            DownloadedProject = ProjectObservation(),
            ControllerToPcDownloadPerformed = true,
            EnvironmentReusable = true,
            UnsupportedGaps = OfficeLiteActiveProjectDownloadContract.RequiredUnsupportedGaps.ToList()
        };
        return new OfficeLiteActiveProjectDownloadReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
    }

    private static ControllerProjectBaselineReceipt CreateBaselineReceipt(
        string id,
        ControllerProjectSoftwareBaseline baseline)
    {
        var intakePayload = new WorkVisualProjectIntakePayload { ReceiptId = $"{id}-intake" };
        var intake = new WorkVisualProjectIntakeReceipt
        {
            Payload = intakePayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(intakePayload)
        };
        var extractionPayload = new WorkVisualProjectExtractionPayload
        {
            ReceiptId = $"{id}-extraction",
            ProjectIntakeReceipt = intake
        };
        var extraction = new WorkVisualProjectExtractionReceipt
        {
            Payload = extractionPayload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(extractionPayload)
        };
        var payload = new ControllerProjectBaselinePayload
        {
            ReceiptId = id,
            TerminalClassification = EnvironmentTerminalClassification.Ready,
            ExtractionReceipt = extraction,
            OriginalProject = ProjectObservation(),
            Baseline = baseline,
            EnvironmentReusable = true,
            UnsupportedGaps = ControllerProjectBaselineContract.RequiredUnsupportedGaps.ToList()
        };
        return new ControllerProjectBaselineReceipt
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };
    }

    private static EnvironmentFileObservation ProjectObservation() => new()
    {
        Id = "project",
        Path = "C:\\evidence\\deployment.analysis-copy.wvs",
        Exists = true,
        Bytes = 1234,
        Sha256 = new string('A', 64)
    };

    private static ControllerProjectSoftwareBaseline CreateExactBaseline()
    {
        var limits = new[]
        {
            (-185d, 185d), (-140d, -5d), (-120d, 168d),
            (-350d, 350d), (-125d, 125d), (-350d, 350d)
        };
        return new ControllerProjectSoftwareBaseline
        {
            ControllerProfile = new WorkVisualExtractedControllerProfile
            {
                TrafoName = NativeKssCandidateSubmissionContract.MachineDataIdentity,
                ModelName = NativeKssCandidateSubmissionContract.MachineDataIdentity,
                RobotIdentityConsistent = true,
                AxisMadaFileCount = 6,
                ToolDataCount = 16,
                BaseDataCount = 32,
                LoadDataCount = 16,
                ToolData = Enumerable.Range(1, 16).Select(index => Frame(index)).ToList(),
                BaseData = Enumerable.Range(1, 32).Select(index => Frame(index)).ToList(),
                LoadData = Enumerable.Range(1, 16).Select(index => new WorkVisualLoadData
                {
                    Index = index,
                    Mass = index == 1 ? 12.5 : -1,
                    CenterOfMass = Frame(index),
                    InertiaX = index,
                    InertiaY = index + 1,
                    InertiaZ = index + 2
                }).ToList(),
                ControllerSoftwareFamily = "KUKA V8.7",
                CabinetKind = NativeKssCandidateSubmissionContract.CabinetKind,
                AxisLimits = limits.Select((limit, index) => new WorkVisualAxisLimit
                {
                    AxisNumber = index + 1,
                    Negative = limit.Item1,
                    Positive = limit.Item2
                }).ToList()
            },
            Axes = limits.Select((limit, index) => new ControllerAxisMachineData
            {
                AxisNumber = index + 1,
                NegativeSoftwareLimitDegrees = limit.Item1,
                PositiveSoftwareLimitDegrees = limit.Item2,
                Direction = index % 2 == 0 ? 1 : -1,
                MasteringReferenceDegrees = index,
                GearRatioNumerator = 100 + index,
                GearRatioDenominator = 1,
                MotorMaximumRpm = 4500 + index,
                DerivedMaximumJointSpeedDegreesPerSecond = 100 + index,
                MotorFile = $"M{index + 1}.xml",
                ServoFile = $"S{index + 1}.xml",
                MasteringType = "EMD",
                SimulationMode = "Off"
            }).ToList(),
            KukaSimComponentComparison = new ControllerKukaSimComponentComparison
            {
                ExactComponentName = NativeKssCandidateSubmissionContract.RobotType,
                ExactC01NameMatch = true,
                ExactComponentPreferred = true
            }
        };
    }

    private static WorkVisualFrameData Frame(int index) => new()
    {
        Index = index,
        X = index,
        Y = index + 1,
        Z = index + 2,
        A = index + 3,
        B = index + 4,
        C = index + 5
    };

    private static OfficeLiteExactProfileAcceptanceRequest CreateExactRequest() => new()
    {
        ProfileReadbackReceipt = CreateProfileReceipt(),
        ActiveProjectDownloadReceipt = CreateDownloadReceipt(),
        ActiveControllerBaselineReceipt = CreateBaselineReceipt("active", CreateExactBaseline()),
        TrustedControllerBaselineReceipt = CreateBaselineReceipt("trusted", CreateExactBaseline())
    };

    private static OfficeLiteExactProfileAcceptanceRequest MutateExactRequest(
        OfficeLiteExactProfileAcceptanceRequest request,
        string scenario)
    {
        var profileReceipt = request.ProfileReadbackReceipt;
        var downloadReceipt = request.ActiveProjectDownloadReceipt;
        var activeReceipt = request.ActiveControllerBaselineReceipt;
        var activeBaseline = activeReceipt.Payload.Baseline!;
        var activeProfile = activeBaseline.ControllerProfile;

        return scenario switch
        {
            "runtime-robot" => request with
            {
                ProfileReadbackReceipt = profileReceipt with
                {
                    Payload = profileReceipt.Payload with
                    {
                        Profile = profileReceipt.Payload.Profile with { RobotType = "#KR3R540 C4SR" }
                    }
                }
            },
            "runtime-kss" => request with
            {
                ProfileReadbackReceipt = profileReceipt with
                {
                    Payload = profileReceipt.Payload with
                    {
                        Profile = profileReceipt.Payload.Profile with { KssVersion = "V8.7.7.999" }
                    }
                }
            },
            "active-project" => request with
            {
                ProfileReadbackReceipt = profileReceipt with
                {
                    Payload = profileReceipt.Payload with
                    {
                        Profile = profileReceipt.Payload.Profile with { CurrentProjectName = "InitialProject" }
                    }
                }
            },
            "download-binding" => request with
            {
                ActiveProjectDownloadReceipt = downloadReceipt with
                {
                    Payload = downloadReceipt.Payload with
                    {
                        DownloadedProject = downloadReceipt.Payload.DownloadedProject with
                        {
                            Sha256 = new string('B', 64)
                        }
                    }
                }
            },
            "machine-data" => WithActiveProfile(request, activeProfile with
            {
                TrafoName = "#KR3R540 C4SR",
                ModelName = "#KR3R540 C4SR"
            }),
            "cabinet" => WithActiveProfile(request, activeProfile with { CabinetKind = "KRC5_MICRO" }),
            "controller-software" => WithActiveProfile(
                request,
                activeProfile with { ControllerSoftwareFamily = "KUKA V8.6" }),
            "axis-limits" => WithActiveProfile(request, activeProfile with
            {
                AxisLimits = activeProfile.AxisLimits
                    .Select((value, index) => index == 0 ? value with { Positive = value.Positive + 1 } : value)
                    .ToList()
            }),
            "tool-data" => WithActiveProfile(request, activeProfile with
            {
                ToolData = activeProfile.ToolData
                    .Select((value, index) => index == 0 ? value with { X = value.X + 1 } : value)
                    .ToList()
            }),
            "base-data" => WithActiveProfile(request, activeProfile with
            {
                BaseData = activeProfile.BaseData
                    .Select((value, index) => index == 0 ? value with { X = value.X + 1 } : value)
                    .ToList()
            }),
            "load-data" => WithActiveProfile(request, activeProfile with
            {
                LoadData = activeProfile.LoadData
                    .Select((value, index) => index == 0 ? value with { Mass = value.Mass + 1 } : value)
                    .ToList()
            }),
            "kukasim-component" => request with
            {
                ActiveControllerBaselineReceipt = CreateBaselineReceipt(
                    "active-component-drift",
                    activeBaseline with
                    {
                        KukaSimComponentComparison = activeBaseline.KukaSimComponentComparison with
                        {
                            ExactComponentPreferred = false
                        }
                    })
            },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown drift scenario.")
        };
    }

    private static OfficeLiteExactProfileAcceptanceRequest WithActiveProfile(
        OfficeLiteExactProfileAcceptanceRequest request,
        WorkVisualExtractedControllerProfile profile) => request with
    {
        ActiveControllerBaselineReceipt = CreateBaselineReceipt(
            "active-profile-drift",
            request.ActiveControllerBaselineReceipt.Payload.Baseline! with { ControllerProfile = profile })
    };

    private sealed class AcceptingInputVerifier : IOfficeLiteExactProfileAcceptanceInputVerifier
    {
        public IReadOnlyList<string> Verify(OfficeLiteExactProfileAcceptanceRequest request) => [];
    }
}
