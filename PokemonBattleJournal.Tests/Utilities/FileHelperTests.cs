using PokemonBattleJournal.Utilities;

namespace PokemonBattleJournal.Tests.Utilities;

// FileHelper branches on DeviceInfo.Platform == DevicePlatform.Unknown (unit test env).
// All tests run in that env so they exercise the test-safe branches.
public class FileHelperTests
{
    [Test]
    public void GetAppDataPath_TestEnvironment_ReturnsTestResponse()
    {
        string result = FileHelper.GetAppDataPath();
        result.ShouldBe("Test Response");
    }

    [Test]
    public void Exists_TestEnvironment_ReturnsTrue()
    {
        bool result = FileHelper.Exists("any/path/does/not/matter.db");
        result.ShouldBeTrue();
    }

    [Test]
    public void CreateFile_TestEnvironment_DoesNotThrow()
    {
        Should.NotThrow(() => FileHelper.CreateFile("irrelevant.txt"));
    }

    [Test]
    public void DeleteFile_TestEnvironment_DoesNotThrow()
    {
        Should.NotThrow(() => FileHelper.DeleteFile("irrelevant.txt"));
    }

    [Test]
    public async Task ReadFileAsync_TestEnvironment_ReturnsTestResponse()
    {
        string result = await FileHelper.ReadFileAsync("any/path.txt");
        result.ShouldBe("Test Response");
    }

    [Test]
    public async Task WriteFileAsync_TestEnvironment_DoesNotThrow()
    {
        await Should.NotThrowAsync(() => FileHelper.WriteFileAsync("any/path.txt", "content"));
    }
}
