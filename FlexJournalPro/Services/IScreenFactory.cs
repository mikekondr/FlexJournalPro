using FlexJournalPro.Models;
using FlexJournalPro.ViewModels;

namespace FlexJournalPro.Services
{
    public interface IScreenFactory
    {
        ScreenBase CreateJournalsListScreen(MainViewModel mainViewModel);
        ScreenBase CreateTemplatesListScreen(MainViewModel mainViewModel);
        ScreenBase CreateUsersListScreen(MainViewModel mainViewModel);
        ScreenBase CreateNewJournalScreen(MainViewModel mainViewModel);
        ScreenBase CreateJournalEditorScreen(JournalMetadata journal, MainViewModel mainViewModel);
        ScreenBase CreateUserEditorScreen(AppUser? userToEdit, MainViewModel mainViewModel);
        ScreenBase CreateLogsScreen(MainViewModel mainViewModel);
    }
}