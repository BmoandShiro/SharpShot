namespace SharpShot
{
    /// <summary>
    /// Compile-time channel flags. GitHub builds leave these false.
    /// SteamBuild=true defines STEAM + DISABLE_INAPP_UPDATES.
    /// StoreBuild=true defines STORE + DISABLE_INAPP_UPDATES.
    /// </summary>
    public static class BuildInfo
    {
        // Static properties (not const) so both branches stay reachable to the compiler.
        // The active value is still fixed at compile time by SteamBuild / StoreBuild.
#if STEAM
        public static readonly bool IsSteam = true;
#else
        public static readonly bool IsSteam = false;
#endif

#if STORE
        public static readonly bool IsStore = true;
#else
        public static readonly bool IsStore = false;
#endif

#if DISABLE_INAPP_UPDATES
        public static readonly bool DisableInAppUpdates = true;
#else
        public static readonly bool DisableInAppUpdates = false;
#endif
    }
}
