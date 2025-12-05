using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using GravitySpinMatch.Core;
using GravitySpinMatch.Data;
using System.IO;

namespace GravitySpinMatch.Managers
{
    public class CustomThemeManager : MonoBehaviour
    {
        [Header("의존성")]
        [SerializeField] private BoardManager m_boardManager;
        
        [Header("설정")]
        [SerializeField] private int m_sliceRows = 3; // 예: 3x3 그리드
        [SerializeField] private int m_sliceCols = 3; 
        // 참고: Match-3 게임에서 사진을 단순히 자르는 방식은 블록 간 구분이 모호할 수 있습니다.
        // 하지만 기획서 요구사항에 따라 "이미지를 잘라 퍼즐 블록으로 사용"하는 방식을 따릅니다.
        // (추상화 스타일)
        
        // 파일 선택기 실행
        public void ImportImageAsync()
        {
#if UNITY_EDITOR
            ImportImageEditor();
#elif UNITY_ANDROID || UNITY_IOS
            ImportImageMobile();
#else
            Debug.Log("[CustomThemeManager] 실제 파일 선택기가 지원되지 않는 플랫폼입니다. 시뮬레이션을 사용합니다.");
            SimulateImportImageAsync().Forget();
#endif
        }

#if UNITY_EDITOR
        private async void ImportImageEditor()
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel("이미지 선택", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path))
            {
                byte[] fileData = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(fileData)) // Auto-resize
                {
                    await CreateThemeFromTextureAsync(tex);
                }
            }
        }
#endif

#if UNITY_ANDROID || UNITY_IOS
        private void ImportImageMobile()
        {
            NativeGallery.GetImageFromGallery((path) =>
            {
                if (path != null)
                {
                    // Create Texture from selected image
                    Texture2D texture = NativeGallery.LoadImageAtPath(path, 1024, false);
                    if (texture == null)
                    {
                        Debug.Log("텍스처를 로드할 수 없습니다: " + path);
                        return;
                    }

                    CreateThemeFromTextureAsync(texture).Forget();
                }
            });
        }
#endif

        private async UniTaskVoid SimulateImportImageAsync()
        {
             // (필요 시 시뮬레이션 로직 유지)
            
            Debug.Log("[CustomThemeManager] 이미지 가져오기 시뮬레이션 중...");
            
            // 더미 텍스처 생성 (사진 시뮬레이션용 컬러 패턴)
            Texture2D dummyTex = new Texture2D(512, 512);
            for(int y=0; y<512; y++)
            {
                for(int x=0; x<512; x++)
                {
                    dummyTex.SetPixel(x, y, new Color(x/512f, y/512f, 0.5f));
                }
            }
            dummyTex.Apply();

            await CreateThemeFromTextureAsync(dummyTex);
        }

        public async UniTask CreateThemeFromTextureAsync(Texture2D source)
        {
            Debug.Log("[CustomThemeManager] 이미지 처리 중...");

            // 1. 준비 (정사각형 크롭 + 리사이징)
            Texture2D processed = ImageProcessor.PrepareTexture(source, 512);

            // 2. 자르기 (Slice)
            // 게임 로직을 위해 충분한 수의 유니크 스프라이트가 필요합니다.
            Sprite[] slices = ImageProcessor.SliceTexture(processed, m_sliceRows, m_sliceCols);

            // 3. 테마 생성
            ThemeData newTheme = ScriptableObject.CreateInstance<ThemeData>();
            newTheme.name = "UserCustomTheme";
            newTheme.UpdateBlockSprites(slices);

            // 4. 적용
            m_boardManager.SetTheme(newTheme);
            
            Debug.Log("[CustomThemeManager] 테마 적용 완료!");
        }
    }
}
