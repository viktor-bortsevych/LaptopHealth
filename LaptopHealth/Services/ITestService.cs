namespace LaptopHealth.Services
{
    /// <summary>
    /// Dummy service for demonstration of dependency injection
    /// </summary>
    public interface ITestService
    {
        string GetTestMessage(int testIndex);
        void LogTestExecution(int testIndex);
    }
}
