using System;

namespace Ship_Game;

public partial class UniverseScreen
{
    /// <summary>
    /// Saves forcefully Pause the game, until the auto-save is complete
    /// </summary>
    public bool IsSaving { get; private set; }
    string PendingSaveName;
    int Auto = 1;

    public SavedGame Save(string saveName, bool throwOnError = false)
    {
        try
        {
            IsSaving = true;
            var savedGame = new SavedGame(this);
            savedGame.Save(saveName);
            return savedGame; // used in unit testing
        }
        catch (Exception e)
        {
            if (throwOnError)
                throw;
            Log.Error(e, $"Universe.Save('{saveName}') failed");
        }
        finally
        {
            IsSaving = false;
        }
        return null;
    }

    public void SaveAsync(string saveName, bool resetLogOnComplete = false)
    {
        IsSaving = true;
        var savedGame = new SavedGame(this);
        savedGame.SaveAsync(saveName, (error) =>
        {
            IsSaving = false;
            if (error != null)
            {
                Log.Error(error, $"Universe.SaveAsync('{saveName}') failed");
            }
            else if (resetLogOnComplete)
            {
                // Bound blackbox.log growth (and the tail attached to Sentry reports)
                // by truncating it after each successful autosave. Startup rotation to
                // blackbox.old is unchanged.
                Log.ResetCurrentLog();
            }
        });
    }

    // Saves must run on the simulation thread to ensure thread safety
    public void SaveDuringNextUpdate(string saveName)
    {
        PendingSaveName = saveName;
    }

    // Ludoal fork: the Battle Arena is a UniverseScreen too — its StarDate runs and
    // was rotating the REAL game's autosave slots with arena snapshots.
    public bool DisableAutoSave;

    void CheckForPendingSaves()
    {
        GameBase game = GameBase.Base;

        if (DisableAutoSave)
            return;

        if (PendingSaveName.NotEmpty())
        {
            SaveAsync(PendingSaveName);
            PendingSaveName = null;

            // reset auto-save timers since we just saved the game
            LastAutosaveTime = game.TotalElapsed;
            LastAutosaveStarDate = UState.StarDate;
        }
        else
        {
            if (LastAutosaveTime == 0f)
                LastAutosaveTime = game.TotalElapsed;

            // Ludoal fork: no autosave while paused — the game state does not advance,
            // saving the same turn repeatedly just rotates the autosave slots away.
            // The timer keeps running; the save fires at the first unpaused check.
            // Year-based autosave (AutoSaveYears > 0, default 5 star-years = 50 turns): follows
            // the pace of the GAME, not the wall clock, and a pause freezes it for free
            // (the StarDate does not move). AutoSaveYears = 0 falls back to time-based.
            bool due;
            if (GlobalStats.AutoSaveYears > 0)
            {
                if (LastAutosaveStarDate == 0f)
                    LastAutosaveStarDate = UState.StarDate;
                due = UState.StarDate - LastAutosaveStarDate >= GlobalStats.AutoSaveYears - 0.001f;
            }
            else
            {
                float timeSinceLastAutoSave = (game.TotalElapsed - LastAutosaveTime);
                due = timeSinceLastAutoSave >= GlobalStats.AutoSaveFreq && !UState.Paused;
            }

            if (due)
            {
                LastAutosaveTime = game.TotalElapsed;
                LastAutosaveStarDate = UState.StarDate;
                string saveName = "Autosave" + Auto;
                if (++Auto > 3) Auto = 1;
                SaveAsync(saveName, resetLogOnComplete: true);
            }
        }
    }
}