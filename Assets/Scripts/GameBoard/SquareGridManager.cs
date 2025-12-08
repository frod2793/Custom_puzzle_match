using UnityEngine;
using System.Collections.Generic;

namespace Match3
{
    public class SquareGridManager : IGridManager
    {
        private Vector2Int m_gridSize;
        
        public float CellSize { get; private set; } = 1.0f;

        public void Initialize(LevelData levelData)
        {
            m_gridSize = levelData.GridSize;
        }

        public Vector3 GetLocalPosition(int x, int y)
        {
            float xOffset = (m_gridSize.x - 1) * CellSize / 2.0f;
            float yOffset = (m_gridSize.y - 1) * CellSize / 2.0f;
            
            return new Vector3(x * CellSize - xOffset, y * CellSize - yOffset, 0);
        }

        public Vector2Int GetGridPosition(Vector3 worldPosition)
        {
            // 이 메서드는 현재 GameBoard에서 직접 사용되지 않으므로,
            // 만약 사용하게 된다면 worldPosition을 localPosition으로 변환하는 로직이 필요합니다.
            // 예: Vector3 localPosition = gridTransform.InverseTransformPoint(worldPosition);
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
