using FlexJournalPro.Models;
using FlexJournalPro.ViewModels;
using FlexJournalPro.ViewModels.Screens;
using Microsoft.Extensions.DependencyInjection;

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
    }

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
            return ActivatorUtilities.CreateInstance<TemplatesListScreen>(_serviceProvider, mainViewModel);
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
            return ActivatorUtilities.CreateInstance<JournalEditorScreen>(_serviceProvider, journal, mainViewModel);
        }

        public ScreenBase CreateUserEditorScreen(AppUser? userToEdit, MainViewModel mainViewModel)
        {
            return ActivatorUtilities.CreateInstance<UserEditorScreen>(_serviceProvider, userToEdit, mainViewModel);
        }
    }
}