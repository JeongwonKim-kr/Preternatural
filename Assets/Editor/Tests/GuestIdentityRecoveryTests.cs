using System.Reflection;
using Game.Core;
using Game.Net;
using NUnit.Framework;

public class GuestIdentityRecoveryTests
{
    [Test]
    public void ConflictProfile_IsProcessSpecificAndUgsCompatible()
    {
        MethodInfo method = typeof(AuthProfile).GetMethod(
            "CreateConflictProfile",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null,
            "A lobby identity collision needs a deterministic per-process fallback profile.");

        string first = (string)method.Invoke(null, new object[] { "default", 42, 255L });
        string second = (string)method.Invoke(null, new object[] { "default", 43, 255L });

        Assert.That(first, Is.EqualTo("guest-42-ff"));
        Assert.That(second, Is.EqualTo("guest-43-ff"));
        Assert.That(first, Does.Match("^[a-zA-Z0-9_-]{1,30}$"));
        Assert.That(second, Is.Not.EqualTo(first));
    }

    [TestCase(false, 0, "player is already a member of the lobby", true)]
    [TestCase(false, 1, "player is already a member of the lobby", false)]
    [TestCase(true, 0, "player is already a member of the lobby", false)]
    [TestCase(false, 0, "session not found", false)]
    [TestCase(false, 0, null, false)]
    public void JoinConflict_RecoversOnlyOnceBeforeSessionExists(
        bool inSession,
        int recoveryAttempts,
        string errorMessage,
        bool expected)
    {
        MethodInfo method = typeof(SessionManager).GetMethod(
            "ShouldRecoverGuestIdentity",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null,
            "Guest join recovery must be bounded to one retry for the confirmed lobby-member collision.");

        bool actual = (bool)method.Invoke(null, new object[]
        {
            inSession,
            recoveryAttempts,
            errorMessage
        });
        Assert.That(actual, Is.EqualTo(expected));
    }
}
