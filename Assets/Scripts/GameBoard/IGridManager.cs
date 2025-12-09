using UnityEngine;
using System.Collections.Generic;

namespace Match3
{
    public interface IGridManager
    {
        float CellSize { get; }
        void Initialize(LevelData levelData, Grid unityGrid); // Grid 컴포넌트 추가
        /// <summary>
        /// 그리드 좌표(x, y)에 해당하는 로컬 좌표를 반환합니다.
        /// </summary>
        Vector3 GetLocalPosition(int x, int y);
        Vector2Int GetGridPosition(Vector3 worldPosition);
        List<Vector2Int> GetNeighbors(int x, int y);
    }
}
