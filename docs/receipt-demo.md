# Receipt demo

The file-only path gives an agent a result that can be checked again by another process.

```powershell
dotnet run --project src/KukaLab.Cli -- krl candidate-intake --source samples/raw-krl --output .local/receipt.json
dotnet run --project src/KukaLab.Cli -- receipt verify --receipt .local/receipt.json
```

The verifier recomputes the receipt payload hash. If the receipt or either candidate file is edited after intake, verification fails closed and reports the mismatch. A passing receipt proves the recorded bytes and pairing; it does not prove simulator execution or physical safety.
