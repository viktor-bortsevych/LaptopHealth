namespace LaptopHealth.Services.Interfaces
{
    /// <summary>
    /// Service for managing counter operations
    /// This is a simple example of business logic separated from UI
    /// </summary>
    public interface ICounterService
    {
        int GetCurrentCount();
        void Increment();
        void Reset();
    }

    public class CounterService : ICounterService
    {
        private int _count = 0;

        public int GetCurrentCount()
        {
            return _count;
        }

        public void Increment()
        {
            _count++;
            System.Diagnostics.Debug.WriteLine($"[CounterService] Count incremented to {_count}");
        }

        public void Reset()
        {
            _count = 0;
            System.Diagnostics.Debug.WriteLine($"[CounterService] Count reset to 0");
        }
    }
}
