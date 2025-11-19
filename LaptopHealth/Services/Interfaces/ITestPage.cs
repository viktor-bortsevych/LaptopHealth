namespace LaptopHealth.Services.Interfaces
{
    /// <summary>
    /// Interface for all test pages
    /// Provides metadata about the test
    /// </summary>
    public interface ITestPage
    {
        /// <summary>
        /// Gets the name of the test
        /// </summary>
        string TestName { get; }

        /// <summary>
        /// Gets the description of what this test does
        /// </summary>
        string TestDescription { get; }
    }
}
