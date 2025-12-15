using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Match3; 

public class LevelButtonCreator
{
    [MenuItem("Tools/Create Level Button Prefab")]
    public static void Create()
    {
        // 1. 디렉토리 확인 및 생성
        string directory = "Assets/Prefabs/UI";
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }

        // 2. GameObject 생성 및 컴포넌트 추가
        GameObject root = new GameObject("LevelButton");
        root.AddComponent<RectTransform>();
        root.AddComponent<CanvasRenderer>();
        Image img = root.AddComponent<Image>();
        Button btn = root.AddComponent<Button>();
        LevelButton lvlBtn = root.AddComponent<LevelButton>();

        // 이미지 설정 (기본값)
        img.color = Color.white;
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);

        // 자식 텍스트 생성
        GameObject textObj = new GameObject("LevelText");
        textObj.transform.SetParent(root.transform, false);
        textObj.AddComponent<RectTransform>();
        textObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        
        tmp.text = "1";
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
        
        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero; // 꽉 채우기

        // 3. SerializedObject를 사용하여 private 필드 연결
        SerializedObject so = new SerializedObject(lvlBtn);
        so.FindProperty("m_levelText").objectReferenceValue = tmp;
        so.FindProperty("m_button").objectReferenceValue = btn;
        so.ApplyModifiedProperties();

        // 4. 프리팹으로 저장
        string path = directory + "/LevelButton.prefab";
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);

        // 5. 정리
        Object.DestroyImmediate(root);
        
        Debug.Log($"[LevelButtonCreator] 프리팹이 성공적으로 생성되었습니다: {path}");
        
        // 하이라이트
        EditorGUIUtility.PingObject(prefab);
    }
}
