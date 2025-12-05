using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace GravitySpinMatch.Managers
{
    public class InputManager : MonoBehaviour
    {
        // 이벤트: 시계 방향 회전 여부 (true: 시계, false: 반시계)
        public static event Action<bool> OnRotateCommand; 

        private Vector2 m_startTouchPos;
        private Vector2 m_endTouchPos;
        
        [Header("입력 설정")]
        [SerializeField] 
        private float m_swipeThreshold = 50f;

        private void Update()
        {
            HandleKeyboardInput();
            HandlePointerInput();
        }

        private void HandleKeyboardInput()
        {
            if (Keyboard.current == null) return;

            // 에디터용 디버그 키
            if (Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                Debug.Log("[InputManager] 반시계 방향 회전");
                OnRotateCommand?.Invoke(false); 
            }
            else if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                Debug.Log("[InputManager] 시계 방향 회전");
                OnRotateCommand?.Invoke(true); 
            }
        }

        private void HandlePointerInput()
        {
            if (Pointer.current == null) return;

            // 'press'는 마우스 좌클릭과 터치 입력을 통합해서 감지합니다.
            if (Pointer.current.press.wasPressedThisFrame)
            {
                m_startTouchPos = Pointer.current.position.ReadValue();
            }

            if (Pointer.current.press.wasReleasedThisFrame)
            {
                m_endTouchPos = Pointer.current.position.ReadValue();
                DetectSwipe();
            }
        }

        private void DetectSwipe()
        {
            float deltaX = m_endTouchPos.x - m_startTouchPos.x;
            
            if (Mathf.Abs(deltaX) > m_swipeThreshold)
            {
                // 가로 스와이프 판정
                if (deltaX > 0)
                {
                    OnRotateCommand?.Invoke(true); // 오른쪽 스와이프 -> 시계 방향
                }
                else
                {
                    OnRotateCommand?.Invoke(false); // 왼쪽 스와이프 -> 반시계 방향
                }
            }
        }
    }
}
