using System.Text;
using Unity.Collections;

namespace Game.Core
{
    /// 닉네임을 FixedString64Bytes(UTF-8 61바이트)에 안전하게 담는다.
    /// 초과분은 문자 경계에서 절단 — 생성자 직접 호출은 초과 시 예외가 나므로 금지.
    public static class NicknameUtil
    {
        const int MaxBytes = 61;

        public static FixedString64Bytes ToFixed(string raw)
        {
            return new FixedString64Bytes(ClampUtf8(raw, MaxBytes));
        }

        /// maxBytes 이내로 문자 경계 절단
        public static string ClampUtf8(string raw, int maxBytes)
        {
            var s = raw ?? "";
            if (Encoding.UTF8.GetByteCount(s) <= maxBytes) return s;
            var sb = new StringBuilder();
            int bytes = 0;
            foreach (var c in s)
            {
                int b = Encoding.UTF8.GetByteCount(c.ToString());
                if (bytes + b > maxBytes) break;
                bytes += b;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
