using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace Match3.Editor
{
    [CustomEditor(typeof(LevelData))]
    public class LevelDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            LevelData levelData = (LevelData)target;

            if (GUILayout.Button("Load from Tilemap in Scene"))
            {
                LoadDataFromTilemap(levelData);
            }
        }

        private void LoadDataFromTilemap(LevelData levelData)
        {
            var tilemap = FindObjectOfType<Tilemap>();
            if (tilemap == null)
            {
                Debug.LogError("No active Tilemap found in the scene. Please add a Tilemap.");
                return;
            }

            tilemap.CompressBounds();
            BoundsInt bounds = tilemap.cellBounds;
            var tilePositions = new List<Vector2Int>();

            for (int y = bounds.min.y; y < bounds.max.y; y++)
            {
                for (int x = bounds.min.x; x < bounds.max.x; x++)
                {
                    Vector3Int cellPosition = new Vector3Int(x, y, 0);
                    if (tilemap.HasTile(cellPosition))
                    {
                        tilePositions.Add((Vector2Int)cellPosition);
                    }
                }
            }

            levelData.SetTilePositions(tilePositions);
            Debug.Log($"Loaded {tilePositions.Count} tiles from {tilemap.name} into {levelData.name}.");
        }
    }
}
