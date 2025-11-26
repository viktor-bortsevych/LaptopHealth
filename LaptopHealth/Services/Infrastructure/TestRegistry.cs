using LaptopHealth.Services.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace LaptopHealth.Services.Infrastructure
{
    /// <summary>
    /// Information about a registered test page
    /// </summary>
    public record TestPageInfo(
        [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type PageType,
        string Name,
        string Description
    );

    /// <summary>
    /// Registry for managing all available test pages
    /// </summary>
    public static class TestRegistry
    {
        private static readonly List<TestPageInfo> _registeredTests = [];

        /// <summary>
        /// Gets a read-only list of all registered tests
        /// </summary>
        public static IReadOnlyList<TestPageInfo> RegisteredTests => _registeredTests.AsReadOnly();

        /// <summary>
        /// Registers a test page with the registry
        /// </summary>
        /// <typeparam name="T">The test page type (must inherit from UserControl and implement ITestPage)</typeparam>
        /// <param name="name">The display name of the test</param>
        /// <param name="description">A brief description of what the test does</param>
        public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
            string name,
            string description = "") where T : System.Windows.Controls.UserControl, ITestPage
        {
            _registeredTests.Add(new TestPageInfo(typeof(T), name, description));
        }

        /// <summary>
        /// Clears all registered tests
        /// </summary>
        public static void Clear() => _registeredTests.Clear();
    }
}
