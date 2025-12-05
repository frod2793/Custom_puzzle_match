using UnityEngine;
using UnityEditor;
using GravitySpinMatch.Managers;
using GravitySpinMatch.Game;
using UnityEngine.UI;
using TMPro;

namespace GravitySpinMatch.Editor
{
    public class SceneSetupTool
    {
        [MenuItem("GravitySpinMatch/Setup Scene")]
        public static void SetupScene()
        {
            // 1. Create Managers
            GameManager gameManager = EnsureGameObject<GameManager>("GameManager");
            BoardManager boardManager = EnsureGameObject<BoardManager>("Board");
            CustomThemeManager themeManager = EnsureGameObject<CustomThemeManager>("ThemeManager");
            InputManager inputManager = EnsureGameObject<InputManager>("InputManager");
            UIManager uiManager = EnsureGameObject<UIManager>("UIManager");
            ScoreManager scoreManager = EnsureGameObject<ScoreManager>("ScoreManager");

            // 2. Link Dependencies
            // CustomThemeManager needs BoardManager
            SerializedObject themeSO = new SerializedObject(themeManager);
            themeSO.FindProperty("m_boardManager").objectReferenceValue = boardManager;
            themeSO.ApplyModifiedProperties();

            // UIManager needs CustomThemeManager
            SerializedObject uiSO = new SerializedObject(uiManager);
            uiSO.FindProperty("m_customThemeManager").objectReferenceValue = themeManager;
            uiSO.ApplyModifiedProperties();

            // 3. Setup UI
            GameObject canvasObj = GameObject.Find("Canvas");
            if (canvasObj == null)
            {
                EditorApplication.ExecuteMenuItem("GameObject/UI/Canvas");
                canvasObj = GameObject.Find("Canvas");
            }
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            
            // EventSystem
            if (GameObject.Find("EventSystem") == null)
            {
                EditorApplication.ExecuteMenuItem("GameObject/UI/Event System");
            }

            // Import Button
            GameObject importBtnObj = EnsureUIElement("ImportButton", canvas.transform);
            Button importBtn = importBtnObj.GetComponent<Button>();
            if (importBtn == null) importBtn = importBtnObj.AddComponent<Button>();
            // Add text
            GameObject btnTextObj = EnsureUIElement("Text (TMP)", importBtnObj.transform);
            TextMeshProUGUI btnText = btnTextObj.GetComponent<TextMeshProUGUI>();
            if (btnText == null) btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "이미지 가져오기";
            // Position
            RectTransform btnRect = importBtnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0, 1);
            btnRect.anchorMax = new Vector2(0, 1);
            btnRect.pivot = new Vector2(0, 1);
            btnRect.anchoredPosition = new Vector2(20, -20);
            btnRect.sizeDelta = new Vector2(160, 50);

            // Score Text
            GameObject scoreTextObj = EnsureUIElement("ScoreText", canvas.transform);
            TextMeshProUGUI scoreText = scoreTextObj.GetComponent<TextMeshProUGUI>();
            if (scoreText == null) scoreText = scoreTextObj.AddComponent<TextMeshProUGUI>();
            scoreText.text = "점수: 0";
            scoreText.alignment = TextAlignmentOptions.TopRight;
            // Position
            RectTransform scoreRect = scoreTextObj.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(1, 1);
            scoreRect.anchorMax = new Vector2(1, 1);
            scoreRect.pivot = new Vector2(1, 1);
            scoreRect.anchoredPosition = new Vector2(-20, -20);
            scoreRect.sizeDelta = new Vector2(300, 50);

            // Moves Text
            GameObject movesTextObj = EnsureUIElement("MovesText", canvas.transform);
            TextMeshProUGUI movesText = movesTextObj.GetComponent<TextMeshProUGUI>();
            if (movesText == null) movesText = movesTextObj.AddComponent<TextMeshProUGUI>();
            movesText.text = "이동: 20";
            movesText.alignment = TextAlignmentOptions.Top;
            // Position
            RectTransform movesRect = movesTextObj.GetComponent<RectTransform>();
            movesRect.anchorMin = new Vector2(0.5f, 1);
            movesRect.anchorMax = new Vector2(0.5f, 1);
            movesRect.pivot = new Vector2(0.5f, 1);
            movesRect.anchoredPosition = new Vector2(0, -20);
            movesRect.sizeDelta = new Vector2(200, 50);

            // Combo Text
            GameObject comboTextObj = EnsureUIElement("ComboText", canvas.transform);
            TextMeshProUGUI comboText = comboTextObj.GetComponent<TextMeshProUGUI>();
            if (comboText == null) comboText = comboTextObj.AddComponent<TextMeshProUGUI>();
            comboText.text = "";
            comboText.fontSize = 48;
            comboText.alignment = TextAlignmentOptions.Center;
            // Position
            RectTransform comboRect = comboTextObj.GetComponent<RectTransform>();
            comboRect.anchorMin = new Vector2(0.5f, 0.5f);
            comboRect.anchorMax = new Vector2(0.5f, 0.5f);
            comboRect.anchoredPosition = new Vector2(0, 100);

            // Game Over Panel
            GameObject goPanelObj = EnsureUIElement("GameOverPanel", canvas.transform);
            Image goPanelImg = goPanelObj.GetComponent<Image>();
            if (goPanelImg == null) goPanelImg = goPanelObj.AddComponent<Image>();
            goPanelImg.color = new Color(0, 0, 0, 0.8f);
            RectTransform goRect = goPanelObj.GetComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero;
            goRect.anchorMax = Vector2.one;
            goRect.offsetMin = Vector2.zero;
            goRect.offsetMax = Vector2.zero;
            goPanelObj.SetActive(false);

            // GO Text
            GameObject goTextObj = EnsureUIElement("TitleText", goPanelObj.transform);
            TextMeshProUGUI goText = goTextObj.GetComponent<TextMeshProUGUI>();
            if (goText == null) goText = goTextObj.AddComponent<TextMeshProUGUI>();
            goText.text = "게임 오버";
            goText.fontSize = 72;
            goText.alignment = TextAlignmentOptions.Center;
            RectTransform goTextRect = goTextObj.GetComponent<RectTransform>();
            goTextRect.anchoredPosition = new Vector2(0, 100);

            // Final Score Text
            GameObject finalScoreObj = EnsureUIElement("FinalScoreText", goPanelObj.transform);
            TextMeshProUGUI finalScoreText = finalScoreObj.GetComponent<TextMeshProUGUI>();
            if (finalScoreText == null) finalScoreText = finalScoreObj.AddComponent<TextMeshProUGUI>();
            finalScoreText.text = "최종 점수: 0";
            finalScoreText.fontSize = 40;
            finalScoreText.alignment = TextAlignmentOptions.Center;
            RectTransform fsRect = finalScoreObj.GetComponent<RectTransform>();
            fsRect.anchoredPosition = new Vector2(0, 0);

            // Restart Button
            GameObject restartBtnObj = EnsureUIElement("RestartButton", goPanelObj.transform);
            Button restartBtn = restartBtnObj.GetComponent<Button>();
            if (restartBtn == null) restartBtn = restartBtnObj.AddComponent<Button>();
            // Button Image
            Image restartImg = restartBtnObj.GetComponent<Image>();
            if (restartImg == null) restartImg = restartBtnObj.AddComponent<Image>();
            // Button Text
            GameObject rBtnTextObj = EnsureUIElement("Text (TMP)", restartBtnObj.transform);
            TextMeshProUGUI rBtnText = rBtnTextObj.GetComponent<TextMeshProUGUI>();
            if (rBtnText == null) rBtnText = rBtnTextObj.AddComponent<TextMeshProUGUI>();
            rBtnText.text = "재시작";
            rBtnText.alignment = TextAlignmentOptions.Center;
            rBtnText.color = Color.black;
            RectTransform rBtnRect = restartBtnObj.GetComponent<RectTransform>();
            rBtnRect.anchoredPosition = new Vector2(0, -100);
            rBtnRect.sizeDelta = new Vector2(200, 60);


            // Link UI to UIManager
            uiSO.Update();
            uiSO.FindProperty("m_importBtn").objectReferenceValue = importBtn;
            uiSO.FindProperty("m_scoreText").objectReferenceValue = scoreText;
            uiSO.FindProperty("m_comboText").objectReferenceValue = comboText;
            uiSO.FindProperty("m_movesText").objectReferenceValue = movesText;
            uiSO.FindProperty("m_gameOverPanel").objectReferenceValue = goPanelObj;
            uiSO.FindProperty("m_restartBtn").objectReferenceValue = restartBtn;
            uiSO.FindProperty("m_finalScoreText").objectReferenceValue = finalScoreText;
            uiSO.ApplyModifiedProperties();

            // Create Block Prefab if missing
            CreateBlockPrefab();
            
            // Assign Prefab to BoardManager
            GameObject prefabObj = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BlockPrefab.prefab");
            if (prefabObj != null)
            {
                SerializedObject boardSO = new SerializedObject(boardManager);
                boardSO.FindProperty("m_blockPrefab").objectReferenceValue = prefabObj.GetComponent<Block>();
                boardSO.ApplyModifiedProperties();
            }
            
            Debug.Log("Scene Setup Complete!");
        }

        private static T EnsureGameObject<T>(string name) where T : Component
        {
            GameObject obj = GameObject.Find(name);
            if (obj == null)
            {
                obj = new GameObject(name);
            }
            
            T component = obj.GetComponent<T>();
            if (component == null)
            {
                component = obj.AddComponent<T>();
            }
            return component;
        }

        private static GameObject EnsureUIElement(string name, Transform parent)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                GameObject obj = new GameObject(name);
                obj.transform.SetParent(parent, false);
                return obj;
            }
            return child.gameObject;
        }

        private static void CreateBlockPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            string path = "Assets/Prefabs/BlockPrefab.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                GameObject obj = new GameObject("BlockPrefab");
                obj.AddComponent<SpriteRenderer>();
                obj.AddComponent<Block>();
                
                PrefabUtility.SaveAsPrefabAsset(obj, path);
                GameObject.DestroyImmediate(obj);
            }
        }
    }
}
