using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Caliburn.Micro;
using VisualComponents.Create3D;

public static class KukaLabComponentLoadSmoke
{
    private const string ResultSchemaIdentity = "kuka.lab.kukasim-component-smoke-result";
    private const int ResultSchemaVersion = 1;

    public static void Main()
    {
        var resultPath = Environment.GetEnvironmentVariable("KUKA_LAB_RESULT_PATH");
        var componentPath = Environment.GetEnvironmentVariable("KUKA_LAB_COMPONENT_PATH");
        var layoutPath = Environment.GetEnvironmentVariable("KUKA_LAB_LAYOUT_PATH");
        var exitCode = 2;

        try
        {
            RequireAbsolutePath(resultPath, "KUKA_LAB_RESULT_PATH");
            RequireAbsolutePath(componentPath, "KUKA_LAB_COMPONENT_PATH");
            RequireAbsolutePath(layoutPath, "KUKA_LAB_LAYOUT_PATH");

            if (!File.Exists(componentPath))
            {
                throw new FileNotFoundException("The declared component source does not exist.", componentPath);
            }

            if (File.Exists(resultPath) || File.Exists(layoutPath))
            {
                throw new IOException("The result and layout outputs must both be new files.");
            }

            var application = IoC.Get<IApplication>();
            if (application == null)
            {
                throw new InvalidOperationException("The Visual Components application service is unavailable.");
            }

            if (!application.Initialized || !application.IsReady)
            {
                throw new InvalidOperationException("The Visual Components application is not ready for a component load.");
            }

            bool isComponent;
            var loaded = application.LoadLayout(
                new Uri(Path.GetFullPath(componentPath)),
                new Vector3(0.0, 0.0, 0.0),
                out isComponent);
            if (loaded == null || loaded.Length == 0)
            {
                throw new InvalidOperationException("KUKA.Sim returned no loaded components.");
            }

            if (!isComponent)
            {
                throw new InvalidOperationException("The declared source was not classified as a component.");
            }

            application.SaveLayout(new Uri(Path.GetFullPath(layoutPath)));
            if (!File.Exists(layoutPath))
            {
                throw new IOException("KUKA.Sim did not create the requested layout evidence file.");
            }

            var names = new List<string>();
            for (var index = 0; index < loaded.Length; index++)
            {
                names.Add(loaded[index] == null ? string.Empty : loaded[index].Name ?? string.Empty);
            }

            WriteResult(
                resultPath,
                "Passed",
                componentPath,
                ComputeSha256(componentPath),
                layoutPath,
                true,
                true,
                loaded.Length,
                names,
                application.Initialized,
                application.IsReady,
                application.ValidLicenseExists,
                GetEngineVersion(),
                true,
                string.Empty,
                string.Empty);
            exitCode = 0;
        }
        catch (Exception exception)
        {
            TryWriteFailureResult(resultPath, componentPath, layoutPath, exception);
        }
        finally
        {
            var current = Application.Current;
            if (current != null)
            {
                AppLoaderHelper.ExitApp(current, exitCode);
            }

            // KUKA.Sim 4.10 can leave the native engine process alive after WPF Shutdown.
            // The result and layout have already been closed and flushed above, so terminate
            // the isolated smoke process deterministically with the same exit code.
            Environment.Exit(exitCode);
        }
    }

    private static void TryWriteFailureResult(
        string resultPath,
        string componentPath,
        string layoutPath,
        Exception exception)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(resultPath) || !Path.IsPathRooted(resultPath) || File.Exists(resultPath))
            {
                return;
            }

            WriteResult(
                resultPath,
                "Failed",
                componentPath ?? string.Empty,
                File.Exists(componentPath) ? ComputeSha256(componentPath) : string.Empty,
                layoutPath ?? string.Empty,
                !string.IsNullOrWhiteSpace(layoutPath) && File.Exists(layoutPath),
                false,
                0,
                new List<string>(),
                false,
                false,
                false,
                GetEngineVersion(),
                true,
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.Message ?? string.Empty);
        }
        catch
        {
            // The host will still exit. Absence of a result is classified by the outer adapter.
        }
    }

    private static void WriteResult(
        string resultPath,
        string status,
        string componentPath,
        string componentSha256,
        string layoutPath,
        bool layoutSaved,
        bool isComponent,
        int loadedComponentCount,
        IList<string> loadedComponentNames,
        bool applicationInitialized,
        bool applicationReady,
        bool validLicenseExists,
        string engineVersion,
        bool exitRequested,
        string errorType,
        string errorMessage)
    {
        var json = new StringBuilder();
        json.Append('{');
        AppendString(json, "schemaIdentity", ResultSchemaIdentity, true);
        AppendNumber(json, "schemaVersion", ResultSchemaVersion);
        AppendString(json, "status", status, false);
        AppendString(json, "componentPath", Path.GetFullPath(componentPath), false);
        AppendString(json, "componentSha256", componentSha256, false);
        AppendString(json, "layoutPath", string.IsNullOrWhiteSpace(layoutPath) ? string.Empty : Path.GetFullPath(layoutPath), false);
        AppendBoolean(json, "layoutSaved", layoutSaved);
        AppendBoolean(json, "isComponent", isComponent);
        AppendNumber(json, "loadedComponentCount", loadedComponentCount);
        json.Append(",\"loadedComponentNames\":[");
        for (var index = 0; index < loadedComponentNames.Count; index++)
        {
            if (index > 0)
            {
                json.Append(',');
            }

            json.Append('\"').Append(JsonEscape(loadedComponentNames[index] ?? string.Empty)).Append('\"');
        }

        json.Append(']');
        AppendBoolean(json, "applicationInitialized", applicationInitialized);
        AppendBoolean(json, "applicationReady", applicationReady);
        AppendBoolean(json, "validLicenseExists", validLicenseExists);
        AppendString(json, "engineVersion", engineVersion, false);
        AppendBoolean(json, "exitRequested", exitRequested);
        AppendString(json, "errorType", errorType, false);
        AppendString(json, "errorMessage", errorMessage, false);
        json.Append('}');

        var parent = Path.GetDirectoryName(Path.GetFullPath(resultPath));
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
        {
            throw new DirectoryNotFoundException("The result directory does not exist.");
        }

        using (var stream = new FileStream(resultPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(json.ToString());
            writer.WriteLine();
            writer.Flush();
            stream.Flush(true);
        }
    }

    private static void AppendString(StringBuilder json, string name, string value, bool first)
    {
        if (!first)
        {
            json.Append(',');
        }

        json.Append('\"').Append(JsonEscape(name)).Append("\":\"")
            .Append(JsonEscape(value ?? string.Empty)).Append('\"');
    }

    private static void AppendBoolean(StringBuilder json, string name, bool value)
    {
        json.Append(",\"").Append(JsonEscape(name)).Append("\":")
            .Append(value ? "true" : "false");
    }

    private static void AppendNumber(StringBuilder json, string name, int value)
    {
        json.Append(",\"").Append(JsonEscape(name)).Append("\":").Append(value);
    }

    private static string JsonEscape(string value)
    {
        var escaped = new StringBuilder(value.Length + 16);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            switch (character)
            {
                case '\"':
                    escaped.Append("\\\"");
                    break;
                case '\\':
                    escaped.Append("\\\\");
                    break;
                case '\b':
                    escaped.Append("\\b");
                    break;
                case '\f':
                    escaped.Append("\\f");
                    break;
                case '\n':
                    escaped.Append("\\n");
                    break;
                case '\r':
                    escaped.Append("\\r");
                    break;
                case '\t':
                    escaped.Append("\\t");
                    break;
                default:
                    if (character < 0x20)
                    {
                        escaped.Append("\\u").Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        escaped.Append(character);
                    }

                    break;
            }
        }

        return escaped.ToString();
    }

    private static string ComputeSha256(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var hash = SHA256.Create())
        {
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }

    private static string GetEngineVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        return assembly == null ? string.Empty : assembly.GetName().Version.ToString();
    }

    private static void RequireAbsolutePath(string path, string variableName)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            throw new InvalidOperationException(variableName + " must contain an absolute path.");
        }
    }
}
