File: Core/CutsceneDetector.cs — tracks movie/cutscene activity via EventManager subscriptions so the mod can yield input to the game's native skip path.

namespace Wasteland2AccessibilityMod.Core  (line 4)

static class CutsceneDetector  (line 15)
    // Subscribes lazily to EventInfo_MovieStarted/Ended; combines movie flag with Drama.isCutsceneOn for IsActive.

    private static bool isMoviePlaying  (line 17)
    private static bool subscribed  (line 18)

    public static bool IsActive { get; }  (line 20)
        // note: getter calls EnsureSubscribed() on every access; also reads Drama.isCutsceneOn directly.

    private static void EnsureSubscribed()  (line 29)
        // note: subscribes to EventManager lazily (first call after EventManager is available); no-ops if already subscribed.
    private static void OnMovieStarted(EventInfoBase e)  (line 48)
    private static void OnMovieEnded(EventInfoBase e)  (line 53)
