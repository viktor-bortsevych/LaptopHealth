using LaptopHealth.Services.Interfaces;
using Microsoft.Win32;

namespace LaptopHealth.Services.Infrastructure
{
    public class DialogService : IDialogService
    {
        public string? OpenFileDialog(string title, string filter, bool multiselect = false)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                Multiselect = multiselect
            };

            return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
        }
    }
}
