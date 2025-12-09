using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // TextMeshProUGUI 사용을 위해 추가

namespace Match3
{
    /// <summary>
    /// 타이틀 씬에서 LevelDatabase를 기반으로 레벨 선택 UI를 생성하고 씬 전환을 관리합니다.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        [Header("데이터베이스 및 UI 프리팹")]
        [SerializeField] private LevelDatabase m_levelDatabase;
        [SerializeField] private GameObject m_levelButtonPrefab;
        [SerializeField] private Transform m_levelButtonContainer;

        private const string k_GameSceneName = "Game";

        private void Start()
        {
            if (m_levelDatabase == null || m_levelButtonPrefab == null || m_levelButtonContainer == null)
            {
                Debug.LogError("<b>[치명적 오류]</b> TitleManager의 필수 데이터가 할당되지 않았습니다!", this);
                return;
            }

            PopulateLevelButtons();
        }

        /// <summary>
        /// LevelDatabase의 정보를 바탕으로 레벨 선택 버튼들을 동적으로 생성합니다.
        /// </summary>
        private void PopulateLevelButtons()
        {
            // 기존에 생성된 버튼이 있다면 모두 삭제
            foreach (Transform child in m_levelButtonContainer)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < m_levelDatabase.Levels.Count; i++)
            {
                GameObject buttonGO = Instantiate(m_levelButtonPrefab, m_levelButtonContainer);
                Button levelButton = buttonGO.GetComponent<Button>();
                
                // 버튼의 텍스트 설정 (자식 오브젝트에서 TextMeshProUGUI 컴포넌트를 찾아 수정)
                TextMeshProUGUI buttonText = buttonGO.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = $"Level {i + 1}";
                }

                // 버튼 클릭 이벤트에 리스너 등록
                int levelID = i; // 클로저 문제 방지를 위해 지역 변수에 복사
                levelButton.onClick.AddListener(() => OnLevelButtonPressed(levelID));
            }
        }

        /// <summary>
        /// 레벨 선택 버튼이 눌렸을 때 호출될 함수입니다.
        /// </summary>
        /// <param name="levelID">선택된 레벨의 ID (LevelDatabase에서의 인덱스)</param>
        private void OnLevelButtonPressed(int levelID)
        {
            GameSettings.SelectedLevelID = levelID;
            SceneManager.LoadScene(k_GameSceneName);
        }
    }
}
