using System.Text.Json;

using KukaLab.Core;

namespace KukaLab.Core.Tests;

public sealed class ControllerObservationTests
{
    [Fact]
    public void Owner_observation_is_bound_without_promoting_pending_controller_truth()
    {
        var outcome = CreateOutcome();
        var payload = outcome.Receipt.Payload;

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(ControllerObservationClassification.OwnerObservedManual, payload.Classification);
        Assert.Equal("KR C5", payload.ObservedFacts.ControllerFamily);
        Assert.Equal("KR C5 dualcab AC", payload.ObservedFacts.CabinetModel);
        Assert.Equal("8.7.8", payload.ObservedFacts.KssVersion);
        Assert.Equal("B671", payload.ObservedFacts.KssBuild);
        Assert.Equal("192.0.2.147", payload.ObservedFacts.KliAddress);
        Assert.Equal(ControllerObservationContract.RequiredPendingEvidence, payload.PendingEvidence);
        Assert.False(payload.NetworkTrafficSent);
        Assert.False(payload.ControllerReadAttempted);
        Assert.False(payload.ControllerWriteAttempted);
        Assert.False(payload.VendorApplicationInvoked);
        Assert.False(payload.MotionCommandSent);
        Assert.False(payload.HardwareSerialStored);
        Assert.True(ControllerObservationReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Fact]
    public void Evidence_references_are_normalized_and_sorted()
    {
        var outcome = new ControllerObservationRunner().Run(
            CreateRequest() with
            {
                EvidenceReferences =
                [
                    " onsite-smartpad-kli-config-20260827 ",
                    "onsite-cabinet-nameplate-20260827"
                ]
            },
            "controller-observation-order");

        Assert.Equal(
            ["onsite-cabinet-nameplate-20260827", "onsite-smartpad-kli-config-20260827"],
            outcome.Receipt.Payload.EvidenceReferences);
        Assert.True(ControllerObservationReceiptVerifier.Verify(outcome.Receipt).Succeeded);
    }

    [Theory]
    [InlineData("8.7", "B671", "192.0.2.147", 24)]
    [InlineData("8.7.8", "671", "192.0.2.147", 24)]
    [InlineData("8.7.8", "B671", "127.0.0.1", 24)]
    [InlineData("8.7.8", "B671", "192.0.2.147", 31)]
    public void Invalid_observation_facts_are_rejected(
        string version,
        string build,
        string address,
        int prefixLength)
    {
        var request = CreateRequest() with
        {
            KssVersion = version,
            KssBuild = build,
            KliAddress = address,
            PrefixLength = prefixLength
        };

        Assert.ThrowsAny<ArgumentException>(() =>
            new ControllerObservationRunner().Run(request, "controller-observation-invalid"));
    }

    [Fact]
    public void Verifier_rejects_pending_evidence_promotion_even_when_rehashed()
    {
        var receipt = CreateOutcome().Receipt;
        var payload = receipt.Payload with
        {
            PendingEvidence =
            [
                ControllerObservationPendingEvidence.TechnologyPackages,
                ControllerObservationPendingEvidence.ToolBaseLoad
            ]
        };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var result = ControllerObservationReceiptVerifier.Verify(tampered);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("pending evidence", StringComparison.Ordinal));
    }

    [Fact]
    public void Verifier_rejects_runtime_or_serial_claims_even_when_rehashed()
    {
        var receipt = CreateOutcome().Receipt;
        var payload = receipt.Payload with
        {
            HardwareSerialStored = true,
            ControllerReadAttempted = true
        };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var result = ControllerObservationReceiptVerifier.Verify(tampered);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("runtime/controller effects", StringComparison.Ordinal));
    }

    [Fact]
    public void Verifier_rejects_observed_fact_change_with_only_payload_rehash()
    {
        var receipt = CreateOutcome().Receipt;
        var payload = receipt.Payload with
        {
            ObservedFacts = receipt.Payload.ObservedFacts with { KssBuild = "B999" }
        };
        var tampered = receipt with
        {
            Payload = payload,
            PayloadSha256 = ReceiptSerialization.ComputeCanonicalSha256(payload)
        };

        var result = ControllerObservationReceiptVerifier.Verify(tampered);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("fingerprint", StringComparison.Ordinal));
    }

    [Fact]
    public void Receipt_writer_is_create_new_and_strict_readback_verifies()
    {
        var receipt = CreateOutcome().Receipt;
        var root = Path.Combine(Path.GetTempPath(), $"kuka-lab-controller-observation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "observation.receipt.json");

            ControllerObservationReceiptWriter.WriteNew(path, receipt);
            var readback = ReceiptSerialization.ControllerObservationFromJson(File.ReadAllText(path));

            Assert.True(ControllerObservationReceiptVerifier.Verify(readback).Succeeded);
            Assert.Throws<IOException>(() => ControllerObservationReceiptWriter.WriteNew(path, receipt));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Strict_readback_rejects_unknown_receipt_members()
    {
        var receipt = CreateOutcome().Receipt;
        var json = ReceiptSerialization.ToJson(receipt);
        var tampered = json.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"unexpected\": true,",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => ReceiptSerialization.ControllerObservationFromJson(tampered));
    }

    private static ControllerObservationOutcome CreateOutcome() =>
        new ControllerObservationRunner().Run(CreateRequest(), "controller-observation-valid");

    private static ControllerObservationRequest CreateRequest() => new()
    {
        ControllerFamily = " KR C5 ",
        CabinetModel = "KR C5 dualcab AC",
        KssVersion = "8.7.8",
        KssBuild = "b671",
        KliAddress = "192.0.2.147",
        PrefixLength = 24,
        ObservedOnLocalDate = "2026-08-27",
        EvidenceReferences =
        [
            "onsite-cabinet-nameplate-20260827",
            "onsite-smartpad-kss-info-20260827",
            "onsite-smartpad-kli-config-20260827",
            "onsite-xf5-switch-topology-20260827"
        ]
    };
}
