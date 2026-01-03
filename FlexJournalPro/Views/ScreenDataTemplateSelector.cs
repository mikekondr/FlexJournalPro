using FlexJournalPro.ViewModels.Screens;
using FlexJournalPro.Views.Screens;
using System.Windows;
using System.Windows.Controls;

namespace FlexJournalPro.Views
{
    /// <summary>
    /// Вибирає відповідний DataTemplate для екрану
    /// </summary>
    public class ScreenDataTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (container is FrameworkElement element)
            {
                return item switch
                {
                    JournalsListScreen => CreateTemplate<JournalsListView>(element),
                    JournalEditorScreen => CreateTemplate<JournalEditorView>(element),
                    TemplatesListScreen => CreateTemplate<TemplatesListView>(element),
                    NewJournalScreen => CreateTemplate<NewJournalView>(element),
                    _ => base.SelectTemplate(item, container)
                };
            }

            return base.SelectTemplate(item, container);
        }

        private static DataTemplate CreateTemplate<T>(FrameworkElement element) where T : UserControl, new()
        {
            var factory = new FrameworkElementFactory(typeof(T));
            return new DataTemplate { VisualTree = factory };
        }
    }
}
