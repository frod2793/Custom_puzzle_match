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
        private List<LevelData> m_levels;

        public IReadOnlyList<LevelData> Levels => m_levels;

        public LevelData GetLevel(int levelID)
        {
            if (levelID < 0 || levelID >= m_levels.Count)
            {
                Debug.LogError($"[LevelDatabase] 유효하지 않은 레벨 ID입니다: {levelID}");
                return null;
            }
            return m_levels[levelID];
        }
    }
}
