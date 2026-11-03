namespace GrawiaaApp.API.Interfaces
{
    public interface IGamingService
    {
        Task<double> UpdateScore(int userId, int points);
    }
}
