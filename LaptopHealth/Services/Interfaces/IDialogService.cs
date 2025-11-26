namespace LaptopHealth.Services.Interfaces
{
    public interface IDialogService
    {
        string? OpenFileDialog(string title, string filter, bool multiselect = false);
    }
}
