using UnityEngine;
using System.Collections.Generic;

namespace Match3
{
    public enum GridType
    {
        Square,
        Hexagon
    }

    [CreateAssetMenu(fileName = "LevelData_", menuName = "Match3/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Grid Settings")]
        [SerializeField]
        private GridType m_gridType = GridType.Square;
        public GridType GridType => m_gridType;

        [SerializeField]
        private Vector2Int m_gridSize;
        public Vector2Int GridSize => m_gridSize;

        [Header("Tile Data")]
        [SerializeField]
        private List<Vector2Int> m_tilePositions = new List<Vector2Int>();
        public List<Vector2Int> TilePositions => m_tilePositions;

#if UNITY_EDITOR
        // 에디터 전용 함수: 타일맵에서 데이터를 채우기 위해 사용
        public void SetTilePositions(List<Vector2Int> positions)
        {
            m_tilePositions = positions;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        /// <summary>
        /// 런타임(무한 모드 등)에서 절차적으로 레벨 데이터를 설정하기 위해 사용합니다.
        /// </summary>
        public void SetupRuntimeLevel(GridType gridType, Vector2Int gridSize, List<Vector2Int> tilePositions)
        {
            m_gridType = gridType;
            m_gridSize = gridSize;
            m_tilePositions = tilePositions;
        }
    }
}
