namespace Match3.Effects
{
    /// <summary>
    /// 게임 내에서 사용되는 이펙트의 종류를 정의합니다.
    /// </summary>
    public enum EffectType
    {
        None = 0,
        TileMatch = 1,  // 기본 타일 매치
        SpecialMatch = 2, // 특수 타일 매치
        BombExplosion = 3 // 폭탄 폭발
    }
}
