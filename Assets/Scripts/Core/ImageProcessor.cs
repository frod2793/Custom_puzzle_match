using UnityEngine;

namespace GravitySpinMatch.Core
{
    public static class ImageProcessor
    {
        /// <summary>
        /// Texture2D를 스프라이트 그리드로 자릅니다.
        /// </summary>
        /// <param name="source">원본 텍스처</param>
        /// <param name="rows">행 개수</param>
        /// <param name="columns">열 개수</param>
        /// <returns>스프라이트 배열</returns>
        public static Sprite[] SliceTexture(Texture2D source, int rows, int columns)
        {
            if (source == null) return null;

            int blockWidth = source.width / columns;
            int blockHeight = source.height / rows;
            
            Sprite[] sprites = new Sprite[rows * columns];
            int index = 0;

            // Loop from top-left to bottom-right (or however we want to map them)
            // Unity Texture coordinates: (0,0) is Bottom-Left.
            // Let's slice them so index 0 is Top-Left to match intuitive image reading?
            // Or just simple iteration.
            
            for (int y = rows - 1; y >= 0; y--) // Top to Bottom
            {
                for (int x = 0; x < columns; x++) // Left to Right
                {
                    Rect rect = new Rect(x * blockWidth, y * blockHeight, blockWidth, blockHeight);
                    
                    // Create Sprite
                    // Pivot at center
                    Sprite newSprite = Sprite.Create(source, rect, new Vector2(0.5f, 0.5f));
                    newSprite.name = $"Slice_{x}_{y}";
                    sprites[index] = newSprite;
                    index++;
                }
            }

            return sprites;
        }

        /// <summary>
        /// 텍스처를 정사각형으로 크롭하고 리사이징합니다.
        /// </summary>
        public static Texture2D PrepareTexture(Texture2D source, int targetSize = 512)
        {
            // Simple Center Crop to Square
            int minDim = Mathf.Min(source.width, source.height);
            int startX = (source.width - minDim) / 2;
            int startY = (source.height - minDim) / 2;

            Color[] pixels = source.GetPixels(startX, startY, minDim, minDim);
            
            Texture2D squareTex = new Texture2D(minDim, minDim);
            squareTex.SetPixels(pixels);
            squareTex.Apply();

            // Resize (Bilinear scaling simple implementation or use Unity's if available via RenderTexture)
            // For simplicity in raw C#, we can use a RenderTexture to resize efficiently.
            
            RenderTexture rt = RenderTexture.GetTemporary(targetSize, targetSize, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(squareTex, rt);
            
            Texture2D result = new Texture2D(targetSize, targetSize);
            RenderTexture.active = rt;
            result.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0);
            result.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            
            // Cleanup
            if (Application.isPlaying) 
                Object.Destroy(squareTex);
            else 
                Object.DestroyImmediate(squareTex);

            return result;
        }
    }
}
