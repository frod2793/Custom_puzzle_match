using UnityEngine;
using System.Collections.Generic;

namespace Match3
{
    public interface IGridManager
    {
        float CellSize { get; }
        // gridSize 대신 LevelData를 직접 받아 초기화하도록 변경
        void Initialize(LevelData levelData);
        Vector3 GetWorldPosition(int x, int y);
        Vector2Int GetGridPosition(Vector3 worldPosition);
        List<Vector2Int> GetNeighbors(int x, int y);
    }
}
