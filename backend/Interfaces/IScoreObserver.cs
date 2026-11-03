namespace GrawiaaApp.API.Interfaces
{
    public interface IScoreObserver
    {
        
        Task Execute(int userId, double newScore);
    }
}
