using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class NativeKssCandidateSubmissionTests
{
    [Fact]
    public void Verified_raw_candidate_produces_exact_c01_bounded_submission_plan()
    {
        using var fixture = CandidateFixture.Create();

        var plan = new NativeKssCandidateSubmissionPlanner().Plan(
            new NativeKssCandidateSubmissionRequest
            {
                CandidateRoot = fixture.SourceRoot,
                CandidateReceiptPath = fixture.ReceiptPath,
                ProgramRelativeStem = "CELL",
                MaximumStartCommands = 12
            });

        Assert.Equal(fixture.CandidateId, plan.CandidateId);
        Assert.Equal("CELL", plan.ProgramRelativeStem);
        Assert.Equal("CELL", plan.ProgramName);
        Assert.Equal("CELL.src", plan.Source.RelativePath);
        Assert.Equal("CELL.dat", plan.Data.RelativePath);
        Assert.Equal("KR 210 R2700-2 C01", plan.RequiredProfile.RobotType);
        Assert.Equal("#KR210R2700_2 C01 FLR", plan.RequiredProfile.MachineDataIdentity);
        Assert.Equal("KRC5", plan.RequiredProfile.CabinetKind);
        Assert.Equal("8.7.8", plan.RequiredProfile.KssVersion);
        Assert.Equal(12, plan.MaximumStartCommands);
        Assert.Matches(@"^KRC:\\R1\\Program\\KLAB_CAND_[0-9A-F]{16}$", plan.ControllerTransactionRoot);
    }

    private sealed class CandidateFixture : IDisposable
    {
        private CandidateFixture(string root)
        {
            Root = root;
            SourceRoot = Path.Combine(root, "source");
            ReceiptPath = Path.Combine(root, "evidence", "raw-krl.receipt.json");
            Directory.CreateDirectory(SourceRoot);
            File.WriteAllText(
                Path.Combine(SourceRoot, "CELL.src"),
                "DEF CELL()\n BAS(#INITMOV,0)\n PTP HOME\nEND\n");
            File.WriteAllText(
                Path.Combine(SourceRoot, "CELL.dat"),
                "DEFDAT CELL PUBLIC\n DECL E6AXIS HOME={A1 0,A2 -90,A3 90,A4 0,A5 0,A6 0}\nENDDAT\n");

            var outcome = new RawKrlCandidateRunner().Run(
                new RawKrlCandidateRequest
                {
                    SourceRoot = SourceRoot,
                    ReceiptOutputPath = ReceiptPath
                },
                "test-native-submission-candidate");
            RawKrlCandidateReceiptWriter.WriteNew(ReceiptPath, SourceRoot, outcome.Receipt);
            CandidateId = outcome.Receipt.Payload.CandidateId;
        }

        public string Root { get; }
        public string SourceRoot { get; }
        public string ReceiptPath { get; }
        public string CandidateId { get; }

        public static CandidateFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-native-submission-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new CandidateFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
