using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public class StandaloneDisplaySettingsTests
    {
        [Test]
        public void macOS_기본_창_설정은_고정_16대9이다()
        {
            Assert.AreEqual(FullScreenMode.Windowed, PlayerSettings.fullScreenMode);
            Assert.AreEqual(1920, PlayerSettings.defaultScreenWidth);
            Assert.AreEqual(1080, PlayerSettings.defaultScreenHeight);
            Assert.IsFalse(PlayerSettings.resizableWindow);
        }
    }
}
