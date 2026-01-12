namespace MusicSugessionAppMVP_ASP.Models
{
    public enum CrateLifecycleState
    {
        Active = 1,     // swiping & discovery ongoing
        Ended = 2,      // user finished swiping
        Exported = 3,   // at least one export completed
        ToBeDiscarded = 4   // user started a new crate without exporting
    }
}
