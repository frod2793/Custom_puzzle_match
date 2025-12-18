using UnityEngine;
using System.Collections.Generic;

namespace Match3
{
    /// <summary>
    /// 게임의 모든 레벨 데이터 목록을 관리하는 ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Match3/Level Database", order = 0)]
    public class LevelDatabase : ScriptableObject
    {
        [SerializeField]
        private List<LevelData> m_Squarelevels;
        
        [SerializeField]
        private List<LevelData> m_HaxagonLevels;

        public IReadOnlyList<LevelData> Squarelevels => m_Squarelevels;
        public IReadOnlyList<LevelData> HaxagonLevels => m_HaxagonLevels;

        public LevelData GetLevel(int levelID)
        {
            if (GameSettings.CurrentBoardMode == BoardMode.Square)
            {
                if (levelID < 0 || levelID >= m_Squarelevels.Count)
                {
                    Debug.LogError($"[LevelDatabase] 유효하지 않은 스퀘어 레벨 ID입니다: {levelID}");
                    return null;
                }
                return m_Squarelevels[levelID];
            }
            else if (GameSettings.CurrentBoardMode == BoardMode.Hexagon)
            {
                if (levelID < 0 || levelID >= m_HaxagonLevels.Count)
                {
                    Debug.LogError($"[LevelDatabase] 유효하지 않은 핵사 레벨 ID입니다: {levelID}");
                    return null;
                }
                return m_HaxagonLevels[levelID];
            }
            return null;
        }
    }
}
