using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Caliburn.Micro;
using VisualComponents.Create3D;

public static class KukaLabExactC01ToolLayout
{
    private sealed class ToolFrame
    {
        public ToolFrame(int index, double x, double y, double z, double a, double b, double c)
        {
            Index = index;
            X = x;
            Y = y;
            Z = z;
            A = a;
            B = b;
            C = c;
        }

        public int Index { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }
        public double A { get; private set; }
        public double B { get; private set; }
        public double C { get; private set; }
    }

    private static readonly ToolFrame[] TrustedTools =
    {
        new ToolFrame(1, 43.2754822, 1.76041901, 362.449127, -175.725479, -3.62929, -1.34368074),
        new ToolFrame(2, 42.0254631, -0.188042551, 363.029572, -63.4070, 31.4478798, -144.104477),
        new ToolFrame(4, 154.032776, -112.002007, 47.8532028, 124.133026, 85.7807770, -58.8789215)
    };

    public static void Main()
    {
        var resultPath = Environment.GetEnvironmentVariable("KUKA_LAB_RESULT_PATH");
        var componentPath = Environment.GetEnvironmentVariable("KUKA_LAB_COMPONENT_PATH");
        var layoutPath = Environment.GetEnvironmentVariable("KUKA_LAB_LAYOUT_PATH");
        var exitCode = 2;

        try
        {
            RequireNewAbsolutePath(resultPath, "KUKA_LAB_RESULT_PATH");
            RequireNewAbsolutePath(layoutPath, "KUKA_LAB_LAYOUT_PATH");
            if (string.IsNullOrWhiteSpace(componentPath) || !Path.IsPathRooted(componentPath) || !File.Exists(componentPath))
            {
                throw new FileNotFoundException("The exact C01 component is missing.", componentPath);
            }

            var application = IoC.Get<IApplication>();
            if (application == null || !application.Initialized || !application.IsReady)
            {
                throw new InvalidOperationException("KUKA.Sim is not ready.");
            }

            bool isComponent;
            var loaded = application.LoadLayout(
                new Uri(Path.GetFullPath(componentPath)),
                new Vector3(0.0, 0.0, 0.0),
                out isComponent);
            if (!isComponent || loaded == null || loaded.Length == 0)
            {
                throw new InvalidOperationException("KUKA.Sim did not load the declared component.");
            }

            var component = loaded.SingleOrDefault(item => item != null
                && string.Equals(item.Name, "KR 210 R2700-2 C01", StringComparison.Ordinal));
            if (component == null)
            {
                throw new InvalidOperationException("The loaded component set does not contain the exact KR 210 R2700-2 C01 robot.");
            }

            var robot = component.GetRobot();
            if (robot == null || robot.RobotController == null || robot.RobotController.Tools.Count < 16)
            {
                throw new InvalidOperationException("The exact C01 robot controller does not expose 16 tool frames.");
            }

            var evidence = new List<string>();
            foreach (var tool in TrustedTools)
            {
                var frame = robot.RobotController.Tools[tool.Index - 1];
                var expectedName = "TOOL_DATA[" + tool.Index.ToString(CultureInfo.InvariantCulture) + "]";
                if (!string.Equals(frame.Name, expectedName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Unexpected tool frame order: " + frame.Name + " at index " + tool.Index);
                }

                var matrix = CreateKukaFrame(tool);
                frame.TransformationInReference = matrix;
                var observed = frame.TransformationInReference;
                if (Math.Abs(observed.Px - tool.X) > 0.0001
                    || Math.Abs(observed.Py - tool.Y) > 0.0001
                    || Math.Abs(observed.Pz - tool.Z) > 0.0001)
                {
                    throw new InvalidOperationException("Tool frame translation did not persist in the KUKA.Sim object model: " + expectedName);
                }

                evidence.Add(FormatTool(tool, observed));
            }

            component.Rebuild(true, true);
            application.SaveLayout(new Uri(Path.GetFullPath(layoutPath)));
            if (!File.Exists(layoutPath))
            {
                throw new IOException("KUKA.Sim did not save the tool-synchronized layout.");
            }

            WriteResult(resultPath, "Passed", component.Name, layoutPath, evidence, string.Empty, string.Empty);
            exitCode = 0;
        }
        catch (Exception exception)
        {
            TryWriteFailure(resultPath, layoutPath, exception);
        }
        finally
        {
            var current = Application.Current;
            if (current != null)
            {
                AppLoaderHelper.ExitApp(current, exitCode);
            }

            Environment.Exit(exitCode);
        }
    }

    private static Matrix CreateKukaFrame(ToolFrame tool)
    {
        var a = DegreesToRadians(tool.A);
        var b = DegreesToRadians(tool.B);
        var c = DegreesToRadians(tool.C);
        var ca = Math.Cos(a);
        var sa = Math.Sin(a);
        var cb = Math.Cos(b);
        var sb = Math.Sin(b);
        var cc = Math.Cos(c);
        var sc = Math.Sin(c);

        // KUKA ABC is Rz(A) * Ry(B) * Rx(C). Matrix columns are N, O, A, P.
        return new Matrix(
            ca * cb,
            sa * cb,
            -sb,
            0.0,
            ca * sb * sc - sa * cc,
            sa * sb * sc + ca * cc,
            cb * sc,
            0.0,
            ca * sb * cc + sa * sc,
            sa * sb * cc - ca * sc,
            cb * cc,
            0.0,
            tool.X,
            tool.Y,
            tool.Z,
            1.0);
    }

    private static double DegreesToRadians(double value)
    {
        return value * Math.PI / 180.0;
    }

    private static string FormatTool(ToolFrame tool, Matrix matrix)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "TOOL_DATA[{0}]|X={1:R}|Y={2:R}|Z={3:R}|A={4:R}|B={5:R}|C={6:R}|matrix={7}",
            tool.Index,
            tool.X,
            tool.Y,
            tool.Z,
            tool.A,
            tool.B,
            tool.C,
            matrix);
    }

    private static void RequireNewAbsolutePath(string path, string variable)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            throw new InvalidOperationException(variable + " must be an absolute path.");
        }

        if (File.Exists(path))
        {
            throw new IOException(variable + " must name a new file: " + path);
        }

        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
        {
            throw new DirectoryNotFoundException(variable + " parent directory is missing: " + parent);
        }
    }

    private static void TryWriteFailure(string resultPath, string layoutPath, Exception exception)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(resultPath) && Path.IsPathRooted(resultPath) && !File.Exists(resultPath))
            {
                WriteResult(resultPath, "Failed", string.Empty, layoutPath, new List<string>(), exception.GetType().FullName, exception.Message);
            }
        }
        catch
        {
        }
    }

    private static void WriteResult(
        string resultPath,
        string status,
        string componentName,
        string layoutPath,
        IList<string> tools,
        string errorType,
        string errorMessage)
    {
        var json = new StringBuilder();
        json.Append("{\"schemaIdentity\":\"kuka.lab.exact-c01-tool-layout-result\",\"schemaVersion\":1");
        AppendString(json, "status", status);
        AppendString(json, "componentName", componentName ?? string.Empty);
        AppendString(json, "layoutPath", string.IsNullOrWhiteSpace(layoutPath) ? string.Empty : Path.GetFullPath(layoutPath));
        json.Append(",\"tools\":[");
        for (var index = 0; index < tools.Count; index++)
        {
            if (index > 0) json.Append(',');
            json.Append('\"').Append(Escape(tools[index])).Append('\"');
        }
        json.Append(']');
        AppendString(json, "errorType", errorType ?? string.Empty);
        AppendString(json, "errorMessage", errorMessage ?? string.Empty);
        json.Append('}');

        using (var stream = new FileStream(resultPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.WriteLine(json.ToString());
            writer.Flush();
            stream.Flush(true);
        }
    }

    private static void AppendString(StringBuilder json, string name, string value)
    {
        json.Append(",\"").Append(Escape(name)).Append("\":\"").Append(Escape(value)).Append('\"');
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}
