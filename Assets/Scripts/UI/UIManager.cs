using UnityEngine;
using UnityEngine.UI;
using System;

namespace Match3.UI
{
    public class UIManager : MonoBehaviour
    {
        // --- Singleton Pattern ---
        public static UIManager Instance { get; private set; }
        // -------------------------

        [Header("Buttons")]
        [SerializeField] private Button m_rotateButton;
        [SerializeField] private Button m_refillButton;

        // --- 외부에서 구독할 이벤트들 ---
        public event Action OnRotateButtonPressed;
        public event Action OnRefillButtonPressed;
        // --------------------------------

        private void Awake()
        {
            // 싱글톤 인스턴스 설정
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 각 버튼의 onClick 이벤트에 리스너를 코드 기반으로 등록
            if (m_rotateButton != null)
            {
                m_rotateButton.onClick.AddListener(() => 
                {
                    // 구독자들에게 이벤트 방송
                    OnRotateButtonPressed?.Invoke();
                });
            }

            if (m_refillButton != null)
            {
                m_refillButton.onClick.AddListener(() =>
                {
                    OnRefillButtonPressed?.Invoke();
                });
            }
        }

        private void OnDestroy()
        {
            // 씬 전환 시 메모리 누수 방지를 위해 리스너 정리
            if (m_rotateButton != null) m_rotateButton.onClick.RemoveAllListeners();
            if (m_refillButton != null) m_refillButton.onClick.RemoveAllListeners();
        }
    }
}
