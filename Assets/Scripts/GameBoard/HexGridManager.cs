using UnityEngine;
using System.Collections.Generic;

namespace Match3
{
    public class HexGridManager : IGridManager
    {
        private Vector2Int m_gridSize;
        private Grid m_unityGrid; // Unity Grid 컴포넌트 참조

        // Unity Grid 컴포넌트의 CellSize를 사용합니다.
        public float CellSize => m_unityGrid != null ? m_unityGrid.cellSize.x : 1f;

        public void Initialize(LevelData levelData, Grid unityGrid)
        {
            m_gridSize = levelData.GridSize;
            m_unityGrid = unityGrid; // Grid 컴포넌트 할당
        }

        public Vector3 GetLocalPosition(int q, int r)
        {
            if (m_unityGrid == null)
            {
                Debug.LogError("Unity Grid reference is null in HexGridManager.");
                return Vector3.zero;
            }
            // Unity Grid 컴포넌트의 CellToLocal 메서드를 사용하여 정확한 로컬 위치를 얻습니다.
            return m_unityGrid.CellToLocal(new Vector3Int(q, r, 0));
        }

        public Vector2Int GetGridPosition(Vector3 worldPosition)
        {
            if (m_unityGrid == null)
            {
                Debug.LogError("Unity Grid reference is null in HexGridManager.");
                return Vector2Int.zero;
            }
            // World to Cell 변환 시, Grid 컴포넌트의 WorldToCell 메서드를 사용합니다.
            Vector3Int cell = m_unityGrid.WorldToCell(worldPosition);
            return new Vector2Int(cell.x, cell.y);
        }
        
        private static readonly Vector2Int[] s_axialDirections = 
        {
            new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1),
            new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1)
        };

        public List<Vector2Int> GetNeighbors(int q, int r)
        {
            var neighbors = new List<Vector2Int>();
            foreach (var dir in s_axialDirections)
            {
                neighbors.Add(new Vector2Int(q + dir.x, r + dir.y));
            }
            return neighbors;
        }
    }
}
