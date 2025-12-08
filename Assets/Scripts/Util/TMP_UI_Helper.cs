using UnityEngine;
using TMPro; // TextMeshProUGUI를 사용하기 위해 필요
using UnityEngine.Serialization; // [FormerlySerializedAs] 사용

/// <summary>
/// TextMeshProUGUI UI 요소를 생성하는 유틸리티 클래스입니다.
/// </summary>
public static class TMP_UI_Helper
{
    /// <summary>
    /// 새로운 TextMeshProUGUI 요소를 생성하고 설정합니다.
    /// </summary>
    /// <param name="parent">새로운 UI 요소의 부모 RectTransform입니다. 주로 Canvas의 RectTransform입니다.</param>
    /// <param name="name">생성될 GameObject의 이름입니다.</param>
    /// <param name="text">TextMeshProUGUI에 표시될 초기 텍스트입니다.</param>
    /// <param name="position">UI 요소의 로컬 위치입니다.</param>
    /// <param name="size">UI 요소의 너비와 높이입니다.</param>
    /// <param name="fontSize">텍스트의 폰트 크기입니다.</param>
    /// <param name="fontAsset">사용할 TMP_FontAsset입니다. null이면 기본 폰트가 사용됩니다.</param>
    /// <param name="color">텍스트의 색상입니다.</param>
    /// <param name="alignment">텍스트의 정렬 방식입니다.</param>
    /// <returns>생성된 TextMeshProUGUI 컴포넌트입니다.</returns>
    public static TextMeshProUGUI CreateTextMeshProUGUI(
        RectTransform parent,
        string name,
        string text,
        Vector2 position,
        Vector2 size,
        float fontSize = 24f,
        TMP_FontAsset fontAsset = null,
        Color? color = null,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center
    )
    {
        // 1. GameObject 생성
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();

        // 2. 부모 설정 및 RectTransform 초기화
        rectTransform.SetParent(parent, false); // false를 사용하여 월드 스케일을 유지하지 않음
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
        rectTransform.pivot = new Vector2(0.5f, 0.5f); // 중앙 피벗
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f); // 중앙 앵커
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f); // 중앙 앵커

        // 3. TextMeshProUGUI 컴포넌트 추가 및 설정
        TextMeshProUGUI tmpText = textObject.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.color = color ?? Color.white; // 색상이 지정되지 않으면 흰색 사용
        tmpText.alignment = alignment;
        
        // 경고 수정: enableWordWrapping 대신 textWrappingMode 사용
        tmpText.textWrappingMode = TextWrappingModes.Normal;

        tmpText.overflowMode = TextOverflowModes.Overflow;

        // 폰트 에셋 설정: 지정된 폰트가 없으면 기본 TMP 폰트 에셋 사용
        if (fontAsset != null)
        {
            tmpText.font = fontAsset;
        }
        else
        {
            // TMP_Settings.defaultFontAsset은 TextMeshPro의 기본 설정을 따릅니다.
            // 만약 기본 폰트가 설정되어 있지 않다면 에러가 발생할 수 있으므로 확인하는 것이 좋습니다.
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("TMP_Settings.defaultFontAsset is not set. Please ensure a default font asset is configured in TextMeshPro's Project Settings.");
                // 대안으로 Resources.Load를 통해 기본 폰트를 로드할 수도 있습니다.
                // 예: tmpText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }
            tmpText.font = TMP_Settings.defaultFontAsset;
        }

        return tmpText;
    }
}
