using LaptopHealth.Services.Interfaces;

namespace LaptopHealth.Services.Infrastructure
{
    /// <summary>
    /// Implementation of the test service
    /// </summary>
    public class TestService : ITestService
    {
        public string GetTestMessage(int testIndex)
        {
            return $"Running test {testIndex + 1}";
        }

        public void LogTestExecution(int testIndex)
        {
            System.Diagnostics.Debug.WriteLine($"[TestService] Test {testIndex + 1} executed at {System.DateTime.Now:HH:mm:ss}");
        }
    }
}
