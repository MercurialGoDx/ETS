public enum GoldSource
{
    PassiveTick,
    Kill,
    Other,

    /// <summary>Фиксированная выдача золота (напр. Sacrifice) — не должна расти от бонуса % к получаемому золоту.</summary>
    Fixed
}
