using GradingSystem.Domain.Entities;
using GradingSystem.Worker.Services.Lab;

namespace GradingSystem.Tests.Worker.Services;

public class SourceAnalyzerTests
{
    [Fact]
    public void FileContainsAny_WhenProjectGlobAndPackageInDirectoryPackagesProps_Passes()
    {
        using var temp = new TempSourceTree();
        temp.Write("src/StudentService/StudentService.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        temp.Write("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);

        var result = new SourceAnalyzer().Check(
            TestCase("file-contains-any:**/*.csproj:Polly|Microsoft.Extensions.Http.Resilience"),
            temp.Root,
            Guid.NewGuid());

        Assert.True(result.Passed, result.ErrorMessage ?? result.ActualResponse);
        Assert.Contains("Directory.Packages.props", result.ActualResponse);
    }

    [Fact]
    public void FileContainsAny_WhenMultipleGlobsAndUsageInCode_Passes()
    {
        using var temp = new TempSourceTree();
        temp.Write("src/CourseService/CourseService.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        temp.Write("src/CourseService/Program.cs", "builder.Services.AddStackExchangeRedisCache(options => { });");

        var result = new SourceAnalyzer().Check(
            TestCase("file-contains-any:**/*.csproj;**/*.cs:StackExchange.Redis|AddStackExchangeRedisCache"),
            temp.Root,
            Guid.NewGuid());

        Assert.True(result.Passed);
        Assert.Contains("Program.cs", result.ActualResponse);
        Assert.Contains("AddStackExchangeRedisCache", result.ActualResponse);
    }

    [Fact]
    public void FileContainsAny_WhenMultipleGlobsHaveNoMatches_Fails()
    {
        using var temp = new TempSourceTree();
        temp.Write("src/CourseService/CourseService.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");

        var result = new SourceAnalyzer().Check(
            TestCase("file-contains-any:**/*.csproj;**/*.cs:MassTransit|AddMassTransit"),
            temp.Root,
            Guid.NewGuid());

        Assert.False(result.Passed);
        Assert.Contains("None of the", result.ErrorMessage);
    }

    [Fact]
    public void IntegrationSignal_WhenPackageInDirectoryPackagesProps_Passes()
    {
        using var temp = new TempSourceTree();
        temp.Write("src/CourseService/CourseService.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        temp.Write("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="MassTransit.RabbitMQ" Version="8.3.6" />
              </ItemGroup>
            </Project>
            """);

        var result = new SourceAnalyzer().Check(
            TestCase("integration-signal:rabbitmq"),
            temp.Root,
            Guid.NewGuid());

        Assert.True(result.Passed, result.ErrorMessage ?? result.ActualResponse);
        Assert.Contains("MassTransit", result.ActualResponse);
    }

    [Fact]
    public void IntegrationSignal_WhenUsageInCode_Passes()
    {
        using var temp = new TempSourceTree();
        temp.Write("src/Api/Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        temp.Write("src/Api/Program.cs", "builder.Services.AddOpenTelemetry().WithTracing(builder => { });");

        var result = new SourceAnalyzer().Check(
            TestCase("integration-signal:opentelemetry"),
            temp.Root,
            Guid.NewGuid());

        Assert.True(result.Passed, result.ErrorMessage ?? result.ActualResponse);
        Assert.Contains("Program.cs", result.ActualResponse);
    }

    [Fact]
    public void IntegrationSignal_WhenOnlyWeakComposeKeywordExists_Fails()
    {
        using var temp = new TempSourceTree();
        temp.Write("src/Api/Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        temp.Write("docker-compose.yml", """
            services:
              redis:
                image: redis:7
            """);

        var result = new SourceAnalyzer().Check(
            TestCase("integration-signal:redis"),
            temp.Root,
            Guid.NewGuid());

        Assert.False(result.Passed);
        Assert.Contains("strong signal", result.ErrorMessage);
    }

    [Fact]
    public void IntegrationSignal_WhenUnknownIntegration_Fails()
    {
        using var temp = new TempSourceTree();
        temp.Write("src/Api/Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");

        var result = new SourceAnalyzer().Check(
            TestCase("integration-signal:kafka"),
            temp.Root,
            Guid.NewGuid());

        Assert.False(result.Passed);
        Assert.Contains("Unknown integration signal 'kafka'", result.ErrorMessage);
    }

    private static LabTestCase TestCase(string urlTemplate) => new()
    {
        HttpMethod = "SOURCE",
        UrlTemplate = urlTemplate,
        ExpectedStatusCode = 200,
        MatchMode = LabTestCaseMatchMode.StatusOnly,
        Score = 1,
    };

    private sealed class TempSourceTree : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "source_analyzer_tests", Guid.NewGuid().ToString("N"));

        public void Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
