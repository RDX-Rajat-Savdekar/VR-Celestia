namespace CelestiaVR.Audio
{
    public enum SoundEvent
    {
        GazeEnter,        // Look at a star / celestial body — soft ascending chime (3-D)
        Select,           // Dwell complete / body selected — crystalline chord (3-D)
        Deselect,         // Dismiss — soft descending tone (2-D)
        InspectionOpen,   // Hologram reveals — ethereal wide sweep (3-D)
        InspectionClose,  // Hologram exits — descending fade (2-D)
        PanelOpen,        // Control panel opens — short ascending swoosh (2-D)
        PanelClose,       // Control panel closes — short descending swoosh (2-D)
        ButtonPress,      // Any UI button tap — brief click tone (2-D)
        ModeSwitch,       // Observe / Inspect toggle — two-tone ping (2-D)
        StickPickup,      // Grab stick — woody noise+tone click (3-D)
        StickDeposit,     // Drop stick at fireplace site — low thud (3-D)
        FireIgnite,       // Fire lights up — rising whoosh (3-D)
        FlareShot,        // Flare gun fires — sharp bang (3-D)
        TimeScroll,       // Time scrolling tick — subtle pulse (2-D)
        Movement,         // Locomotion (walking forward/back) — soft step tick (2-D)
    }
}
