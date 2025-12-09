using UnityEngine;
using System.Collections.Generic;

namespace Match3
{
    public class SquareGridManager : IGridManager
    {
        private Vector2Int m_gridSize;
        
        public float CellSize { get; private set; } = 1.0f;

        // IGridManager 인터페이스 변경에 맞춰 Grid 매개변수를 추가합니다.
        public void Initialize(LevelData levelData, Grid unityGrid)
        {
            m_gridSize = levelData.GridSize;
            // SquareGridManager는 unityGrid를 직접 사용하지는 않지만,
            // 인터페이스 계약을 준수하기 위해 매개변수를 받습니다.
            if (unityGrid != null)
            {
                CellSize = unityGrid.cellSize.x;
            }
        }

        public Vector3 GetLocalPosition(int x, int y)
        {
            float xOffset = (m_gridSize.x - 1) * CellSize / 2.0f;
            float yOffset = (m_gridSize.y - 1) * CellSize / 2.0f;
            
            return new Vector3(x * CellSize - xOffset, y * CellSize - yOffset, 0);
        }

        public Vector2Int GetGridPosition(Vector3 worldPosition)
        {
            float xOffset = (m_gridSize.x - 1) * CellSize / 2.0f;
            float yOffset = (m_gridSize.y - 1) * CellSize / 2.0f;

            int x = Mathf.RoundToInt((worldPosition.x + xOffset) / CellSize);
            int y = Mathf.RoundToInt((worldPosition.y + yOffset) / CellSize);
            return new Vector2Int(x, y);
        }

        public List<Vector2Int> GetNeighbors(int x, int y)
        {
            var neighbors = new List<Vector2Int>
            {
                new Vector2Int(x, y + 1), // 상
                new Vector2Int(x, y - 1), // 하
                new Vector2Int(x - 1, y), // 좌
                new Vector2Int(x + 1, y)  // 우
            };
            return neighbors;
        }
    }
}
