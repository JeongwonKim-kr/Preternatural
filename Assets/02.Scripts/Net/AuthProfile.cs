using System.Security.Cryptography;
using System.Text;

namespace Game.Core
{
    /// UGS 익명 인증 프로필 결정.
    /// 에디터(메인/MPPM 가상 플레이어)는 클론별 dataPath가 다르므로 그 해시로 격리하고,
    /// 로컬 다중 빌드 테스트는 -authProfile 인자로 격리한다.
    /// 프로필 미분리 시 같은 익명 PlayerId가 되어 두 번째 참가가 409로 거부된다.
    public static class AuthProfile
    {
        public static string Resolve(string[] commandLineArgs, string dataPath, bool isEditor)
        {
            for (int i = 0; i < commandLineArgs.Length - 1; i++)
                if (commandLineArgs[i] == "-authProfile")
                    return Sanitize(commandLineArgs[i + 1]);

            if (isEditor) return "ed" + StableHash(dataPath);
            return "default";
        }

        static string Sanitize(string raw)
        {
            var sb = new StringBuilder();
            foreach (var c in raw)
                if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_') sb.Append(c);
            var s = sb.Length == 0 ? "default" : sb.ToString();
            return s.Length <= 30 ? s : s.Substring(0, 30);
        }

        static string StableHash(string input)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return $"{bytes[0]:x2}{bytes[1]:x2}{bytes[2]:x2}{bytes[3]:x2}";
        }
    }
}
