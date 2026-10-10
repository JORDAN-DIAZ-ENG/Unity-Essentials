namespace Essential.Juice
{
    /// <summary>
    /// Named shake sizes, so call sites say how big a hit feels rather than
    /// repeating trauma numbers. The trauma each one adds is tuned on
    /// <see cref="JuiceSettings"/>.
    /// </summary>
    public enum ShakeStrength
    {
        Light,
        Medium,
        Heavy
    }
}
