using System.Diagnostics;
using GradingSystem.Worker.Options;
using GradingSystem.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GradingSystem.Tests.Worker.Services;

/// <summary>
/// Phase 5 (pe-grading-scale-reliability), Step 4: a student process that exits naturally
/// right as the timeout's kill attempt arrives must not surface as an unhandled error.
/// CleanupAsync already guards each Kill with a HasExited check + try/catch — these tests
/// pin that behavior down with a real OS process that has already exited by the time
/// CleanupAsync runs, which is the steady state the race resolves to.
/// </summary>
public class ArtifactRunnerCleanupRaceTests
{
    private static ArtifactRunner CreateRunner()
    {
        var opts = Options.Create(new WorkerOptions());
        var config = new ConfigurationBuilder().Build();
        return new ArtifactRunner(opts, config, NullLogger<ArtifactRunner>.Instance);
    }

    private static Process StartAlreadyExitingProcess()
    {
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", "/c exit 0")
            : new ProcessStartInfo("/bin/sh", "-c \"exit 0\"");
        psi.UseShellExecute = false;
        var process = Process.Start(psi)!;
        process.WaitForExit(5000);
        return process;
    }

    [Fact]
    public async Task CleanupAsync_QuestionAppProcessAlreadyExited_DoesNotThrow()
    {
        var runner = CreateRunner();
        var sandboxPath = Path.Combine(Path.GetTempPath(), "rtk-cleanup-race-" + Guid.NewGuid());
        var ctx = new StudentContext { SandboxPath = sandboxPath };

        var questionId = Guid.NewGuid();
        var exitedProcess = StartAlreadyExitingProcess();
        Assert.True(exitedProcess.HasExited, "Process must have already exited before CleanupAsync runs.");
        ctx.QuestionApps[questionId] = new QuestionApp { Process = exitedProcess, Port = 0 };

        var ex = await Record.ExceptionAsync(() => runner.CleanupAsync(ctx));

        Assert.Null(ex);
    }

    [Fact]
    public async Task CleanupAsync_GivenApiProcessAlreadyExited_DoesNotThrow()
    {
        var runner = CreateRunner();
        var sandboxPath = Path.Combine(Path.GetTempPath(), "rtk-cleanup-race-" + Guid.NewGuid());
        var exitedProcess = StartAlreadyExitingProcess();
        Assert.True(exitedProcess.HasExited, "Process must have already exited before CleanupAsync runs.");
        var ctx = new StudentContext { SandboxPath = sandboxPath, GivenApiProcess = exitedProcess };

        var ex = await Record.ExceptionAsync(() => runner.CleanupAsync(ctx));

        Assert.Null(ex);
    }
}
