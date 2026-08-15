using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Debug = UnityEngine.Debug;

namespace Game.Core
{
    /// UGS 익명 인증 프로필 결정.
    /// 에디터(메인/MPPM 가상 플레이어)는 클론별 dataPath가 다르므로 그 해시로 격리하고,
    /// 로컬 다중 빌드 테스트는 -authProfile 인자로 격리한다.
    /// -authProfile도 없이 같은 기기에서 빌드를 두 번 실행하면(사용자가 앱을 그냥 두 번 열기 등)
    /// 둘 다 "default" 프로필이 되어 같은 익명 PlayerId를 쓰게 되고, 두 번째 참가가 세션 SDK에
    /// "player is already a member of the lobby"로 거부된다(첫 번째 연결도 함께 밀려남).
    /// 이를 막기 위해 -authProfile이 없는 비-에디터 경로는 persistentDataPath 아래 PID claim 파일로
    /// 같은 기기의 동시 인스턴스를 자동으로 분리한다 — ResolveLocalSlotProfile 참고.
    ///
    /// 최초 구현은 FileShare.None으로 잠금 파일을 열어 배타 점유를 흉내냈으나, macOS/Mono 실측
    /// (빌드를 두 번 연속 실행)에서 둘 다 "default"를 받는 것으로 확인됐다 — Mono의 유닉스 FileStream
    /// 구현은 FileShare 제약을 커널 레벨(fcntl/flock)로 걸지 않고 프로세스 내부적으로만 관리하므로,
    /// 별도 프로세스에서 같은 파일을 FileShare.None으로 열어도 충돌 없이 성공해 버린다(같은 프로세스
    /// 안에서 반복 호출한 검증만으로는 이 구멍이 드러나지 않았다). 그래서 파일 잠금이 아니라 PID
    /// 생존 여부로 슬롯을 정하는 방식으로 교체했다.
    public static class AuthProfile
    {
        const string ClaimFilePrefix = "authslot_";
        const string ClaimFileSuffix = ".claim";

        /// 재사용된 PID를 다른 프로세스로 오인하지 않도록, claim 파일에 PID뿐 아니라 그 PID의 실제
        /// OS 프로세스 시작 시각(Ticks)도 함께 적어 둔다. 스캔 시 그 PID가 지금 살아있어도 시작 시각이
        /// 다르면(=그 PID가 죽고 다른 프로세스가 같은 번호를 재사용) 죽은 것으로 간주해 정리한다.
        static string s_claimFilePath;
        static bool s_quitHandlerRegistered;

        public static string Resolve(string[] commandLineArgs, string dataPath, string persistentDataPath, bool isEditor)
        {
            for (int i = 0; i < commandLineArgs.Length - 1; i++)
                if (commandLineArgs[i] == "-authProfile")
                    return Sanitize(commandLineArgs[i + 1]);

            if (isEditor) return "ed" + StableHash(dataPath);
            return ResolveLocalSlotProfile(persistentDataPath);
        }

        /// persistentDataPath 아래 authslot_<pid>.claim 파일들로 같은 기기의 동시 인스턴스를 센다.
        /// 1) 자기 PID로 claim 파일을 쓴다(내용: 자기 프로세스의 실제 시작 시각 Ticks).
        /// 2) 디렉터리의 모든 claim 파일을 스캔해, 그 PID가 죽었거나(Process.GetProcessById 실패/
        ///    HasExited) 시작 시각이 기록과 다르면(PID 재사용) 파일을 지운다.
        /// 3) 살아남은(=진짜 살아있는) PID들을 오름차순 정렬해 자기 순번을 구한다.
        /// 4) 순번 0 = "default"(기존 계정과 동일하게 유지 — 단독 실행 사용자는 지금까지와 같은
        ///    프로필), 1번부터는 "p1", "p2" ...
        /// 파일 I/O가 실패하면 예외로 죽지 않고 "default"로 폴백하며 경고를 남긴다.
        internal static string ResolveLocalSlotProfile(string persistentDataPath)
        {
            try
            {
                Directory.CreateDirectory(persistentDataPath);

                var self = Process.GetCurrentProcess();
                int pid = self.Id;
                long startTicks = SafeStartTimeTicks(self);

                s_claimFilePath = Path.Combine(persistentDataPath, $"{ClaimFilePrefix}{pid}{ClaimFileSuffix}");
                File.WriteAllText(s_claimFilePath, startTicks.ToString(CultureInfo.InvariantCulture));
                RegisterQuitCleanup();

                var alivePids = ScanAndCleanClaims(persistentDataPath);
                if (!alivePids.Contains(pid)) alivePids.Add(pid); // 방금 쓴 자기 claim이 스캔에 안 잡혔을 극히 드문 경우의 안전망
                alivePids.Sort();

                int index = alivePids.IndexOf(pid);
                if (index < 0) index = 0; // 이론상 도달 불가 — 폴백
                return index == 0 ? "default" : $"p{index}";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthProfile] PID 기반 프로필 슬롯 처리 실패 — \"default\"로 폴백: {e.Message}");
                return "default";
            }
        }

        /// claim 디렉터리를 스캔해 죽은(또는 PID 재사용된) 항목은 삭제하고, 실제로 살아있는 PID
        /// 목록을 돌려준다.
        static List<int> ScanAndCleanClaims(string persistentDataPath)
        {
            var alive = new List<int>();
            string[] files;
            try { files = Directory.GetFiles(persistentDataPath, $"{ClaimFilePrefix}*{ClaimFileSuffix}"); }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthProfile] claim 디렉터리 스캔 실패: {e.Message}");
                return alive;
            }

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file); // authslot_<pid>
                if (!name.StartsWith(ClaimFilePrefix, StringComparison.Ordinal)) continue;
                var pidStr = name.Substring(ClaimFilePrefix.Length);
                if (!int.TryParse(pidStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var filePid))
                {
                    TryDelete(file);
                    continue;
                }

                if (IsClaimAlive(filePid, file)) alive.Add(filePid);
                else TryDelete(file);
            }
            return alive;
        }

        /// filePid가 실제로 살아있고, claim 파일에 적힌 시작 시각이 그 프로세스의 실제 시작 시각과
        /// 일치하면(=PID가 재사용되지 않았으면) true.
        static bool IsClaimAlive(int filePid, string claimFilePath)
        {
            Process proc;
            try { proc = Process.GetProcessById(filePid); }
            catch (ArgumentException) { return false; } // 그 PID의 프로세스가 없음
            catch (InvalidOperationException) { return false; }

            try { if (proc.HasExited) return false; }
            catch { return false; }

            long recordedTicks;
            try
            {
                var text = File.ReadAllText(claimFilePath).Trim();
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out recordedTicks))
                    return true; // 파싱 실패 — 형식은 낡았지만 생존 여부는 확인됐으니 보수적으로 살아있다고 본다
            }
            catch { return true; }

            long actualTicks = SafeStartTimeTicks(proc);
            if (actualTicks == 0) return true; // 시작 시각을 못 읽는 환경 — 생존 확인만으로 판단(성능 저하 없음, 오탐만 완화 못 함)

            // OS/파일시스템의 시각 해상도 차이를 감안해 2초 이내 오차는 같은 프로세스로 본다.
            return Math.Abs(actualTicks - recordedTicks) <= TimeSpan.FromSeconds(2).Ticks;
        }

        static long SafeStartTimeTicks(Process p)
        {
            try { return p.StartTime.Ticks; }
            catch { return 0; } // 권한/플랫폼 제약으로 못 읽는 경우 — 재사용 감지만 못 할 뿐, 생존 판정 자체는 여전히 유효
        }

        static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { /* 다음 스캔에서 다시 시도됨 — 무해 */ }
        }

        /// 앱 종료 시 자기 claim 파일을 지운다. 실패하거나(강제 종료 등) 아예 호출되지 않아도
        /// 무해하다 — 다음 실행이 죽은 PID로 판정해 청소한다.
        static void RegisterQuitCleanup()
        {
            if (s_quitHandlerRegistered) return;
            s_quitHandlerRegistered = true;
            UnityEngine.Application.quitting += () =>
            {
                if (string.IsNullOrEmpty(s_claimFilePath)) return;
                TryDelete(s_claimFilePath);
            };
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
