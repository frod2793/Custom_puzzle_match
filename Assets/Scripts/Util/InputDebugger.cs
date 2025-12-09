using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // TextMeshPro를 사용하기 위해 추가
using System.Text;

public class InputDebugger : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI m_debugText;

    private StringBuilder m_stringBuilder = new StringBuilder();

    void Update()
    {
        if (m_debugText == null) return;

        m_stringBuilder.Clear();

        // 1. 입력 시스템 상태
        m_stringBuilder.AppendLine($"Pointer.current is null: {Pointer.current == null}");

        if (Pointer.current != null)
        {
            // 2. 입력 이벤트 상태
            m_stringBuilder.AppendLine($"Press was pressed: {Pointer.current.press.wasPressedThisFrame}");
            m_stringBuilder.AppendLine($"Press was released: {Pointer.current.press.wasReleasedThisFrame}");
            m_stringBuilder.AppendLine($"Screen Position: {Pointer.current.position.ReadValue()}");

            // 3. Raycast 정보
            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(Pointer.current.position.ReadValue());
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

            if (hit.collider != null)
            {
                m_stringBuilder.AppendLine($"<color=green>Raycast HIT: {hit.collider.name}</color>");
                m_stringBuilder.AppendLine($"Hit Object Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            }
            else
            {
                m_stringBuilder.AppendLine("<color=red>Raycast HIT: None</color>");
            }
        }

        m_debugText.text = m_stringBuilder.ToString();
    }
}
