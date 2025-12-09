using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Reflection;

namespace Match3.Editor
{
    [CustomEditor(typeof(Match3.GameBoard))]
    public class GameBoardEditor : UnityEditor.Editor
    {
        private int m_selectedLevelIndex = 0;

        private void OnEnable()
        {
            // 에디터가 활성화될 때 EditorPrefs에서 마지막으로 선택한 레벨 ID를 불러옵니다.
            m_selectedLevelIndex = EditorPrefs.GetInt("Match3.Editor.SelectedLevelID", 0);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            Match3.GameBoard gameBoard = (Match3.GameBoard)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = new Color(0.8f, 0.8f, 1.0f);
                EditorGUILayout.LabelField("Test Level Loader", EditorStyles.miniBoldLabel);
                GUI.backgroundColor = Color.white;

                // --- 레벨 선택 UI ---
                FieldInfo levelDatabaseField = typeof(Match3.GameBoard).GetField("m_levelDatabase", BindingFlags.NonPublic | BindingFlags.Instance);
                LevelDatabase levelDatabase = levelDatabaseField?.GetValue(gameBoard) as LevelDatabase;

                if (levelDatabase != null && levelDatabase.Levels != null && levelDatabase.Levels.Count > 0)
                {
                    string[] levelOptions = levelDatabase.Levels
                        .Select((level, index) => $"ID: {index} - {(level != null ? level.name : "NULL")}")
                        .ToArray();

                    // 인덱스가 범위를 벗어나지 않도록 방어 코드 추가
                    if (m_selectedLevelIndex >= levelOptions.Length)
                    {
                        m_selectedLevelIndex = 0;
                    }

                    EditorGUI.BeginChangeCheck();
                    m_selectedLevelIndex = EditorGUILayout.Popup("Select Level", m_selectedLevelIndex, levelOptions);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        // 에디터에서 레벨을 선택하면 즉시 EditorPrefs에 저장합니다.
                        GameSettings.SetSelectedLevelForEditor(m_selectedLevelIndex);
                        EditorUtility.SetDirty(target);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("LevelDatabase is not assigned or has no levels.", MessageType.Warning);
                }

                // --- 플레이 모드 전용 UI ---
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    EditorGUILayout.Space(5);
                    
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.IntField("Selected Level ID", GameSettings.SelectedLevelID);
                    }

                    if (GUILayout.Button("Load Level in Play Mode"))
                    {
                        gameBoard.LoadLevelForEditor(GameSettings.SelectedLevelID);
                    }
                }
                 if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Select a level from the dropdown. It will be loaded automatically when you enter Play Mode.", MessageType.Info);
                }
            }
        }
    }
}
