using APP_GoiXetheoGPS.Models;
using CommunityToolkit.Mvvm.Input;

namespace APP_GoiXetheoGPS.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}