using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

namespace GravitySpinMatch.Managers
{
    public enum GameState
    {
        Initializing,
        Playing,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        private static GameManager s_instance;
        public static GameManager Instance => s_instance;

        [Header("게임 설정")]
        [SerializeField] private int m_maxMoves = 20;

        private int m_movesLeft;
        public int MovesLeft => m_movesLeft;

        private GameState m_currentState;
        public GameState CurrentState => m_currentState;

        // 이벤트
        public event Action<int> OnMovesChanged;
        public event Action<GameState> OnStateChanged;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async UniTaskVoid Start()
        {
            // 게임 오브젝트 파괴 시 취소되는 CancellationToken 획득
            var token = this.GetCancellationTokenOnDestroy();
            await InitializeGameAsync(token);
        }

        private async UniTask InitializeGameAsync(CancellationToken token)
        {
            Debug.Log("[GameManager] 게임 초기화 중...");
            
            m_currentState = GameState.Initializing;
            OnStateChanged?.Invoke(m_currentState);

            // 추후 초기화 로직 (데이터 로드, UI 설정 등)
            await UniTask.Delay(100, cancellationToken: token);
            
            StartGame();
        }

        public void StartGame()
        {
            m_movesLeft = m_maxMoves;
            m_currentState = GameState.Playing;
            
            OnMovesChanged?.Invoke(m_movesLeft);
            OnStateChanged?.Invoke(m_currentState);
            
            if (ScoreManager.Instance != null) ScoreManager.Instance.ResetScore();
            
            Debug.Log("[GameManager] 게임 시작!");
        }

        public bool TryUseMove()
        {
            if (m_currentState != GameState.Playing) return false;
            if (m_movesLeft <= 0) return false;

            m_movesLeft--;
            OnMovesChanged?.Invoke(m_movesLeft);

            if (m_movesLeft <= 0)
            {
                // 여기서 즉시 게임 오버를 호출하지 않습니다.
                // 마지막 이동(중력, 매칭 등)이 끝날 때까지 기다려야 하기 때문입니다.
                // 따라서 true를 반환하되, BoardManager나 별도 체크 로직이 시퀀스 종료 후 게임 오버를 트리거합니다.
            }

            return true;
        }

        public void CheckGameOverCondition()
        {
            if (m_movesLeft <= 0 && m_currentState == GameState.Playing)
            {
                EndGame();
            }
        }

        private void EndGame()
        {
            m_currentState = GameState.GameOver;
            OnStateChanged?.Invoke(m_currentState);
            Debug.Log("[GameManager] 게임 오버!");
        }
        
        public void RestartGame()
        {
            // 단순 변수 리셋 또는 씬 리로드
            StartGame();
            // 이상적으로는 BoardManager에게 보드 리셋을 요청해야 합니다.
            // 프로토타입 단계이므로 씬을 다시 로드하는 단순한 방식을 사용합니다.
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}
