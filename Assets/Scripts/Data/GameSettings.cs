using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Match3
{
    public enum BoardMode { Square, Hexagon }
    public enum PlayMode { Stage, Infinite }

    /// <summary>
    /// 선택된 레벨 ID와 같이 씬 간에 공유되어야 하는 설정을 저장하는 정적 클래스입니다.
    /// </summary>
    public static class GameSettings
    {
        public static BoardMode CurrentBoardMode { get; set; } = BoardMode.Square;
        public static PlayMode CurrentPlayMode { get; set; } = PlayMode.Stage;

        /// <summary>
        /// 사용자가 타이틀 씬 또는 에디터에서 선택한 레벨의 ID (LevelDatabase의 인덱스)
        /// </summary>
        public static int SelectedLevelID { get; set; } = -1;

#if UNITY_EDITOR
        private const string k_SelectedLevelPrefKey = "Match3.Editor.SelectedLevelID";

        // 플레이 모드 시작 시 EditorPrefs에서 값을 읽어와 복원합니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            SelectedLevelID = EditorPrefs.GetInt(k_SelectedLevelPrefKey, -1);
        }

        // 에디터용 편의 함수: 레벨 ID를 EditorPrefs에 저장합니다.
        public static void SetSelectedLevelForEditor(int levelID)
        {
            EditorPrefs.SetInt(k_SelectedLevelPrefKey, levelID);
            SelectedLevelID = levelID;
        }
#endif
    }
}
