using UnityEngine;
using System.Collections.Generic;

namespace Match3
{
    public class HexGridManager : IGridManager
    {
        private Vector2Int m_gridSize;
        private const float k_OuterRadius = 0.577f;
        
        public float CellSize => k_OuterRadius * 2f;

        // 인터페이스와 일치하도록 파라미터를 LevelData로 변경
        public void Initialize(LevelData levelData)
        {
            m_gridSize = levelData.GridSize;
        }

        public Vector3 GetWorldPosition(int q, int r)
        {
            float width = CellSize;
            float height = Mathf.Sqrt(3f) * k_OuterRadius;
            
            float x = width * 3.0f / 4.0f * q;
            float y = height * (r + q / 2.0f);
            
            float xOffset = (m_gridSize.x - 1) * width * 3.0f / 8.0f;
            float yOffset = (m_gridSize.y - 1) * height / 4.0f;

            return new Vector3(x - xOffset, y - yOffset, 0);
        }

        public Vector2Int GetGridPosition(Vector3 worldPosition)
        {
            float width = CellSize;
            float height = Mathf.Sqrt(3f) * k_OuterRadius;

            float xOffset = (m_gridSize.x - 1) * width * 3.0f / 8.0f;
            float yOffset = (m_gridSize.y - 1) * height / 4.0f;
            worldPosition.x += xOffset;
            worldPosition.y += yOffset;

            float q = (2.0f / 3.0f * worldPosition.x) / k_OuterRadius;
            float r = (-1.0f / 3.0f * worldPosition.x + Mathf.Sqrt(3.0f) / 3.0f * worldPosition.y) / k_OuterRadius;
            
            return AxialRound(q, r);
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

        private Vector2Int AxialRound(float q, float r)
        {
            float s = -q - r;
            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);

            float q_diff = Mathf.Abs(rq - q);
            float r_diff = Mathf.Abs(rr - r);
            float s_diff = Mathf.Abs(rs - s);

            if (q_diff > r_diff && q_diff > s_diff)
            {
                rq = -rr - rs;
            }
            else if (r_diff > s_diff)
            {
                rr = -rq - rs;
            }
            
            return new Vector2Int(rq, rr);
        }
    }
}
