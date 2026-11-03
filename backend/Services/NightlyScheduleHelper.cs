namespace GrawiaaApp.API.Services
{
    /// <summary>
    /// Presentation spec: process all sessions nightly at 2 AM (Egypt local time).
    /// </summary>
    public static class NightlyScheduleHelper
    {
        public static TimeSpan DelayUntilNextRun()
        {
            TimeZoneInfo egyptZone;
            try { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }

            var nowEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone);
            var nextRun = nowEgypt.Date.AddHours(2);
            if (nowEgypt >= nextRun)
                nextRun = nextRun.AddDays(1);

            return nextRun - nowEgypt;
        }
    }
}
