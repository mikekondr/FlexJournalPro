using FlexJournalPro.Models;
using FlexJournalPro.ViewModels;
using FlexJournalPro.ViewModels.Screens;
using Microsoft.Extensions.DependencyInjection;

namespace FlexJournalPro.Services
{
    public class ScreenFactory : IScreenFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ScreenFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ScreenBase CreateJournalsListScreen(MainViewModel mainViewModel)
        {
            return ActivatorUtilities.CreateInstance<JournalsListScreen>(_serviceProvider, mainViewModel);
        }

        public ScreenBase CreateTemplatesListScreen(MainViewModel mainViewModel)
        {
            return ActivatorUtilities.CreateInstance<TemplatesListScreen>(_serviceProvider);
        }

        public ScreenBase CreateUsersListScreen(MainViewModel mainViewModel)
        {
            return ActivatorUtilities.CreateInstance<UsersListScreen>(_serviceProvider, mainViewModel);
        }

        public ScreenBase CreateNewJournalScreen(MainViewModel mainViewModel)
        {
            return ActivatorUtilities.CreateInstance<NewJournalScreen>(_serviceProvider, mainViewModel);
        }

        public ScreenBase CreateJournalEditorScreen(JournalMetadata journal, MainViewModel mainViewModel)
        {
            return ActivatorUtilities.CreateInstance<JournalEditorScreen>(_serviceProvider, journal);
        }

        public ScreenBase CreateUserEditorScreen(AppUser? userToEdit, MainViewModel mainViewModel)
        {
            if (userToEdit == null)
            {
                return ActivatorUtilities.CreateInstance<UserEditorScreen>(_serviceProvider, new AppUser(), mainViewModel);
            }
            return ActivatorUtilities.CreateInstance<UserEditorScreen>(_serviceProvider, userToEdit, mainViewModel);
        }

        public ScreenBase CreateLogsScreen(MainViewModel mainViewModel)
        {
            return ActivatorUtilities.CreateInstance<LogsScreen>(_serviceProvider, mainViewModel);
        }
    }
}