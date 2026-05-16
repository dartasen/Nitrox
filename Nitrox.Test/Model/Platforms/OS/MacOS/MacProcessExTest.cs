using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Nitrox.Test.Model.Platforms;

namespace Nitrox.Model.Platforms.OS.MacOS;

[TestClass]
[SupportedOSPlatform("macos")]
public class MacProcessExTest
{
    [OSTestMethod(OperatingSystems.OSX)]
    public void Constructor_WithCurrentProcessId_ShouldExposeTaskPortHandle()
    {
        // Arrange
        int currentProcessId = Environment.ProcessId;

        // Act
        using MacProcessEx process = new(currentProcessId);

        // Assert
        process.Handle.Should().NotBe(IntPtr.Zero);
    }

    [OSTestMethod(OperatingSystems.OSX)]
    public void Dispose_ForCurrentProcess_ShouldNotInvalidateFutureSelfTaskPortLookups()
    {
        // Arrange
        int currentProcessId = Environment.ProcessId;

        // Act
        using (MacProcessEx process = new(currentProcessId))
        {
        }

        using MacProcessEx processAfterDispose = new(currentProcessId);

        // Assert
        processAfterDispose.Handle.Should().NotBe(IntPtr.Zero);
    }

    [OSTestMethod(OperatingSystems.OSX)]
    public void ReadMemory_ForCurrentProcess_ShouldReadPinnedManagedBuffer()
    {
        // Arrange
        byte[] expected = [0xDE, 0xAD, 0xBE, 0xEF];
        GCHandle pinnedBuffer = GCHandle.Alloc(expected, GCHandleType.Pinned);

        try
        {
            using MacProcessEx process = new(Environment.ProcessId);

            // Act
            byte[] actual = process.ReadMemory(pinnedBuffer.AddrOfPinnedObject(), expected.Length);

            // Assert
            actual.Should().Equal(expected);
        }
        finally
        {
            pinnedBuffer.Free();
        }
    }

    [OSTestMethod(OperatingSystems.OSX)]
    public void WriteMemory_ForCurrentProcess_ShouldMutatePinnedManagedBuffer()
    {
        // Arrange
        byte[] target = [0x00, 0x00, 0x00, 0x00];
        byte[] expected = [0xDE, 0xAD, 0xBE, 0xEF];
        GCHandle pinnedBuffer = GCHandle.Alloc(target, GCHandleType.Pinned);

        try
        {
            using MacProcessEx process = new(Environment.ProcessId);

            // Act
            int bytesWritten = process.WriteMemory(pinnedBuffer.AddrOfPinnedObject(), expected);

            // Assert
            bytesWritten.Should().Be(expected.Length);
            target.Should().Equal(expected);
        }
        finally
        {
            pinnedBuffer.Free();
        }
    }

    [OSTestMethod(OperatingSystems.OSX)]
    public void WriteMemory_ThenReadMemory_ForCurrentProcess_ShouldRoundTripPinnedManagedBuffer()
    {
        // Arrange
        byte[] target = [0x00, 0x00, 0x00, 0x00];
        byte[] expected = [0xDE, 0xAD, 0xBE, 0xEF];
        GCHandle pinnedBuffer = GCHandle.Alloc(target, GCHandleType.Pinned);

        try
        {
            using MacProcessEx process = new(Environment.ProcessId);

            // Act
            int bytesWritten = process.WriteMemory(pinnedBuffer.AddrOfPinnedObject(), expected);
            byte[] actual = process.ReadMemory(pinnedBuffer.AddrOfPinnedObject(), expected.Length);

            // Assert
            bytesWritten.Should().Be(expected.Length);
            target.Should().Equal(expected);
            actual.Should().Equal(expected);
        }
        finally
        {
            pinnedBuffer.Free();
        }
    }

    [OSTestMethod(OperatingSystems.OSX)]
    public void SuspendAndResume_ForChildProcess_ShouldPauseAndContinueProgress()
    {
        // Arrange
        string progressFile = Path.Combine(Path.GetTempPath(), $"Nitrox_MacProcessEx_{Guid.NewGuid():N}.txt");
        using Process child = StartProgressWriter(progressFile);

        try
        {
            WaitUntil(() => File.Exists(progressFile) && new FileInfo(progressFile).Length > 0,
                      TimeSpan.FromSeconds(5),
                      "Expected the child process to begin writing progress data.");

            using MacProcessEx process = CreateChildProcessOrMarkInconclusive(child.Id);

            // Act
            process.Suspend();
            long suspendedSize = new FileInfo(progressFile).Length;
            Thread.Sleep(TimeSpan.FromMilliseconds(400));

            process.Resume();

            // Assert
            new FileInfo(progressFile).Length.Should().Be(suspendedSize);
            WaitUntil(() => new FileInfo(progressFile).Length > suspendedSize,
                      TimeSpan.FromSeconds(5),
                      "Expected the child process to continue writing after resume.");
        }
        finally
        {
            CleanupProcess(child);
            TryDelete(progressFile);
        }
    }

    [OSTestMethod(OperatingSystems.OSX)]
    public void Terminate_ForChildProcess_ShouldExitProcess()
    {
        // Arrange
        string progressFile = Path.Combine(Path.GetTempPath(), $"Nitrox_MacProcessEx_{Guid.NewGuid():N}.txt");
        using Process child = StartProgressWriter(progressFile);

        try
        {
            WaitUntil(() => File.Exists(progressFile) && new FileInfo(progressFile).Length > 0,
                      TimeSpan.FromSeconds(5),
                      "Expected the child process to begin writing progress data.");

            using MacProcessEx process = CreateChildProcessOrMarkInconclusive(child.Id);

            // Act
            process.Terminate();

            // Assert
            child.WaitForExit(5000).Should().BeTrue();
            child.HasExited.Should().BeTrue();
        }
        finally
        {
            CleanupProcess(child);
            TryDelete(progressFile);
        }
    }

    private static MacProcessEx CreateChildProcessOrMarkInconclusive(int pid)
    {
        try
        {
            return new MacProcessEx(pid);
        }
        catch (UnauthorizedAccessException ex)
        {
            Assert.Inconclusive(ex.Message);
            throw;
        }
    }

    private static Process StartProgressWriter(string progressFile)
    {
        ProcessStartInfo startInfo = new("/bin/sh")
        {
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("while true; do printf . >> \"$1\"; sleep 0.1; done");
        startInfo.ArgumentList.Add("progress-writer");
        startInfo.ArgumentList.Add(progressFile);

        Process? child = Process.Start(startInfo);
        child.Should().NotBeNull();
        return child!;
    }

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout, string failureMessage)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(50);
        }

        Assert.Fail(failureMessage);
    }

    private static void CleanupProcess(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        process.Kill(entireProcessTree: true);
        process.WaitForExit(5000);
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
