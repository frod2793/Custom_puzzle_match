namespace Match3
{
    /// <summary>
    /// 선택된 레벨 ID와 같이 씬 간에 공유되어야 하는 설정을 저장하는 정적 클래스입니다.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>
        /// 사용자가 타이틀 씬에서 선택한 레벨의 ID (LevelDatabase의 인덱스)
        /// </summary>
        public static int SelectedLevelID { get; set; } = -1; // -1은 선택되지 않음을 의미
    }
}
