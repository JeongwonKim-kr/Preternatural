namespace Game.Core
{
    /// SessionError enum 이름 → 한국어 안내문.
    /// 문자열 키로 받아 패키지 의존 없이 테스트 가능하게 유지.
    public static class SessionErrorMessages
    {
        public static string For(string errorName) => errorName switch
        {
            "SessionNotFound" => "방을 찾을 수 없습니다. 코드를 확인해 주세요.",
            "SessionDeleted" => "방이 닫혔습니다.",
            "SessionFull" => "방이 가득 찼습니다. (최대 4명)",
            "LobbyAlreadyExists" => "이미 이 방에 참가 중인 계정입니다. 같은 기기에서 두 번째로 실행한 경우 " +
                                     "잠시 후 다시 시도해 주세요.",
            "InvalidParameter" => "잘못된 방 코드 형식입니다.",
            "InvalidSessionIdentifier" => "잘못된 방 코드 형식입니다.",
            "RateLimitExceeded" => "요청이 너무 잦습니다. 잠시 후 다시 시도해 주세요.",
            "NotAuthorized" => "로그인이 아직 준비되지 않았습니다. 잠시 후 다시 시도해 주세요.",
            "NetworkSetupFailed" => "네트워크 연결에 실패했습니다.",
            _ => $"연결에 실패했습니다. 잠시 후 다시 시도해 주세요. ({errorName})",
        };
    }
}
