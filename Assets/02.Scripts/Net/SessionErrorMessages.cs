namespace Game.Core
{
    /// SessionError enum name to player-facing English copy.
    /// 문자열 키로 받아 패키지 의존 없이 테스트 가능하게 유지.
    public static class SessionErrorMessages
    {
        public static string For(string errorName) => errorName switch
        {
            "SessionNotFound" => "ROOM NOT FOUND. CHECK THE CODE.",
            "SessionDeleted" => "THE ROOM WAS CLOSED.",
            "SessionFull" => "THE ROOM IS FULL. (MAX 4)",
            "LobbyAlreadyExists" => "THIS ACCOUNT IS ALREADY IN THE ROOM. " +
                                     "WAIT A MOMENT AND TRY AGAIN.",
            "InvalidParameter" => "INVALID ROOM CODE.",
            "InvalidSessionIdentifier" => "INVALID ROOM CODE.",
            "RateLimitExceeded" => "TOO MANY REQUESTS. TRY AGAIN SOON.",
            "NotAuthorized" => "SIGN-IN IS NOT READY. TRY AGAIN SOON.",
            "NetworkSetupFailed" => "NETWORK CONNECTION FAILED.",
            _ => $"CONNECTION FAILED. TRY AGAIN SOON. ({errorName})",
        };
    }
}
