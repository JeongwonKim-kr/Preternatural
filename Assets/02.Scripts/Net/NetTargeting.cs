using System.Collections.Generic;
using UnityEngine;

namespace Game.Net
{
    /// 몬스터 호스트 권위 타겟 선정 — 순수 로직 (테스트 대상).
    public static class NetTargeting
    {
        public struct Candidate { public Vector3 Position; public bool Alive; public bool Hidden; }

        public static int Nearest(Vector3 from, IReadOnlyList<Candidate> candidates)
        {
            int best = -1; float bestSqr = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!candidates[i].Alive || candidates[i].Hidden) continue;
                float d = (candidates[i].Position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }
            return best;
        }
    }
}
