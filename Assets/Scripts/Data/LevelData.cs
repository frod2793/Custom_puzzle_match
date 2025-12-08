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
    }
}
