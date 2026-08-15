using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    /// UGS 익명 인증 프로필 결정.
    /// 에디터(메인/MPPM 가상 플레이어)는 클론별 dataPath가 다르므로 그 해시로 격리하고,
    /// 로컬 다중 빌드 테스트는 -authProfile 인자로 격리한다.
    /// -authProfile도 없이 같은 기기에서 빌드를 두 번 실행하면(사용자가 앱을 그냥 두 번 열기 등)
    /// 둘 다 "default" 프로필이 되어 같은 익명 PlayerId를 쓰게 되고, 두 번째 참가가 세션 SDK에
    /// "player is already a member of the lobby"로 거부된다(첫 번째 연결도 함께 밀려남).
    /// 이를 막기 위해 -authProfile이 없는 비-에디터 경로는 persistentDataPath 아래 슬롯 잠금 파일로
    /// 같은 기기의 동시 인스턴스를 자동으로 분리한다 — ResolveLocalSlotProfile 참고.
    /// 프로필 미분리 시 같은 익명 PlayerId가 되어 두 번째 참가가 409로 거부된다.
    public static class AuthProfile
    {
        /// 동시 실행 가능한 로컬 인스턴스 상한. 코옵 최대 인원(4)보다 넉넉히 잡아 같은 기기에서
        /// 테스트용으로 여러 창을 띄우는 경우까지 커버한다.
        const int MaxLocalSlots = 8;
        const string SlotFilePrefix = "authslot_";

        /// 슬롯 잠금 파일 핸들. 앱 수명 동안 static으로 들고 있어야 잠금이 유지된다 — 로컬 변수로 두면
        /// GC가 FileStream을 회수하며 파일 핸들이 닫혀 잠금이 조기에 풀리고, 곧이어 뜬 다른 인스턴스가
        /// 같은 슬롯을 다시 점유해 버릴 수 있다. 프로세스가 종료되면 OS가 핸들을 회수해 자동으로
        /// 슬롯이 반납되므로 재실행해도 번호가 무한정 쌓이지 않는다.
        static FileStream s_slotLock;

        public static string Resolve(string[] commandLineArgs, string dataPath, string persistentDataPath, bool isEditor)
        {
            for (int i = 0; i < commandLineArgs.Length - 1; i++)
                if (commandLineArgs[i] == "-authProfile")
                    return Sanitize(commandLineArgs[i + 1]);

            if (isEditor) return "ed" + StableHash(dataPath);
            return ResolveLocalSlotProfile(persistentDataPath);
        }

        /// persistentDataPath 아래 authslot_N.lock 파일을 FileShare.None으로 순서대로 열어보며 처음
        /// 성공하는 슬롯 번호로 프로필을 정한다. 슬롯 0은 항상 "default"(기존 계정과 동일하게 유지 —
        /// 단독 실행 사용자는 지금까지와 똑같은 프로필을 쓴다), 1번부터는 "p1", "p2" ... 로 분리된다.
        /// 전부 점유돼 있거나 파일 I/O가 실패하면 예외로 죽지 않고 "default"로 폴백하며 경고를 남긴다
        /// (그 경우 여전히 같은 기기 동시 실행 시 세션 충돌이 재발할 수 있음을 로그로 알린다).
        internal static string ResolveLocalSlotProfile(string persistentDataPath)
        {
            try
            {
                Directory.CreateDirectory(persistentDataPath);
                for (int slot = 0; slot < MaxLocalSlots; slot++)
                {
                    var path = Path.Combine(persistentDataPath, $"{SlotFilePrefix}{slot}.lock");
                    try
                    {
                        var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                        // 이전 잠금이 남아 있다면(정상 경로에서는 Resolve가 프로세스당 1회만 불리므로
                        // 없어야 하지만) GC의 비결정적 회수에 기대지 않고 여기서 명시적으로 닫아,
                        // 그 슬롯이 즉시 다른 인스턴스에 재사용 가능하도록 한다.
                        s_slotLock?.Dispose();
                        s_slotLock = fs; // static 참조 유지 — 앱 종료 전까지 잠금 보존
                        return slot == 0 ? "default" : $"p{slot}";
                    }
                    catch (IOException)
                    {
                        // 다른 인스턴스가 이미 이 슬롯을 점유 중 — 다음 슬롯 시도
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthProfile] 프로필 슬롯 잠금 파일 처리 실패 — \"default\"로 폴백: {e.Message}");
                return "default";
            }

            Debug.LogWarning($"[AuthProfile] 사용 가능한 프로필 슬롯이 없습니다(최대 {MaxLocalSlots}개 전부 점유) — " +
                              "\"default\"로 폴백. 같은 기기에서 여러 인스턴스를 실행 중이면 계정이 겹쳐 세션 참가가 거부될 수 있습니다.");
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
