using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public class NicknameUtilTests
    {
        [Test]
        public void 한글_25자는_예외없이_61바이트_이내로_절단()
        {
            var raw = new string('가', 25); // UTF-8 75바이트
            var fs = NicknameUtil.ToFixed(raw);
            Assert.LessOrEqual(fs.Length, 61);          // Length = UTF-8 바이트 수
            Assert.AreEqual(20, fs.ToString().Length);   // 61/3 = 20자 (문자 경계 보존)
        }

        [Test]
        public void ASCII_70자는_61바이트로_절단()
        {
            var fs = NicknameUtil.ToFixed(new string('a', 70));
            Assert.AreEqual(61, fs.Length);
        }

        [Test]
        public void 짧은_닉네임은_그대로()
        {
            Assert.AreEqual("무명", NicknameUtil.ToFixed("무명").ToString());
        }

        [Test]
        public void null은_빈_문자열()
        {
            Assert.AreEqual("", NicknameUtil.ToFixed(null).ToString());
        }
    }
}
