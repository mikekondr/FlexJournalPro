# Кастомний заголовок вікна FlexJournalPro

## Огляд

FlexJournalPro тепер має модернізований кастомний заголовок вікна з Material Design стилем замість стандартного системного заголовку Windows.

## Особливості

### ? Реалізовано:

1. **Кастомний заголовок (Title Bar)**
   - Висота: 48px
   - Фон: Primary колір Material Design
   - Розділювач внизу для візуального відділення

2. **Логотип та назва програми**
   - Іконка: BookOpenPageVariant (28x28px)
   - Назва: "ЖурналПро" (жирним шрифтом, 16px)
   - Підзаголовок: "Система управління журналами" (12px, напівпрозорий)

3. **Кнопки управління вікном**
   - **Згорнути** (Minimize) - іконка WindowMinimize
   - **Розгорнути/Відновити** (Maximize/Restore) - іконка динамічно змінюється
   - **Закрити** (Close) - червоний фон при наведенні

4. **WindowChrome**
   - Висота області для захоплення: 48px
   - Товщина рамки для зміни розміру: 5px
   - Можливість перетягування вікна за заголовок
   - Подвійний клік для максимізації

## Технічна реалізація

### XAML структура:

```xaml
<Window WindowStyle="None"
        AllowsTransparency="False"
        ResizeMode="CanResize">
    
    <WindowChrome.WindowChrome>
        <WindowChrome CaptionHeight="48"
                      ResizeBorderThickness="5"
                      GlassFrameThickness="0"
                      CornerRadius="0"
                      UseAeroCaptionButtons="False"/>
    </WindowChrome.WindowChrome>
    
    <!-- Content -->
</Window>
```

### Стилі кнопок:

#### TitleBarButtonStyle
- Ширина: 46px
- Висота: 32px
- Прозорий фон за замовчуванням
- Білий напівпрозорий фон при наведенні (10% opacity)
- Білий напівпрозорий фон при натисканні (20% opacity)

#### CloseButtonStyle
- Базується на TitleBarButtonStyle
- Червоний фон (#E81123) при наведенні
- Темно-червоний фон (#8B0A14) при натисканні

### Code-behind обробники:

```csharp
private void MinimizeButton_Click(object sender, RoutedEventArgs e)
{
    WindowState = WindowState.Minimized;
}

private void MaximizeButton_Click(object sender, RoutedEventArgs e)
{
    WindowState = WindowState == WindowState.Maximized 
        ? WindowState.Normal 
        : WindowState.Maximized;
}

private void CloseButton_Click(object sender, RoutedEventArgs e)
{
    Close();
}

private void MainWindow_StateChanged(object sender, System.EventArgs e)
{
    UpdateMaximizeRestoreButton();
}

private void UpdateMaximizeRestoreButton()
{
    if (WindowState == WindowState.Maximized)
    {
        MaximizeIcon.Kind = PackIconKind.WindowRestore;
        MaximizeButton.ToolTip = "Відновити";
    }
    else
    {
        MaximizeIcon.Kind = PackIconKind.WindowMaximize;
        MaximizeButton.ToolTip = "Розгорнути";
    }
}
```

## Функціональність

### Повністю працююча функціональність Windows:

- ? Перетягування вікна за заголовок
- ? Подвійний клік для максимізації/відновлення
- ? Зміна розміру вікна перетягуванням країв
- ? Згортання у панель завдань
- ? Максимізація на весь екран
- ? Відновлення до нормального розміру
- ? Закриття програми
- ? Alt+F4 для закриття
- ? Win+? для максимізації
- ? Win+? для відновлення

### Візуальні ефекти:

- Hover ефекти на кнопках
- Плавні переходи кольорів
- Чіткі іконки Material Design
- Tooltips для кнопок
- Динамічна зміна іконки maximize/restore

## Колірна схема

| Елемент | Колір | Опис |
|---------|-------|------|
| Фон заголовку | Primary (#0061A4) | Основний колір теми |
| Розділювач | Primary Dark (#004A7F) | Темніший відтінок |
| Текст | White (#FFFFFF) | Білий текст |
| Підзаголовок | White 70% opacity | Напівпрозорий |
| Кнопки (hover) | White 10% opacity | Ледь помітний фон |
| Кнопка Close (hover) | Red (#E81123) | Червоний для важливої дії |

## Порівняння

### Було (Стандартний заголовок):
- ? Застарілий Windows вигляд
- ? Немає можливості кастомізації
- ? Не відповідає стилю додатку
- ? Обмежені можливості брендингу

### Стало (Кастомний заголовок):
- ? Сучасний Material Design вигляд
- ? Повна кастомізація
- ? Єдиний стиль з рештою інтерфейсу
- ? Логотип та брендинг
- ? Зручні кнопки управління

## Технічні деталі

### WindowChrome властивості:

- **CaptionHeight**: 48 - висота області заголовку для drag&drop
- **ResizeBorderThickness**: 5 - товщина невидимої рамки для зміни розміру
- **GlassFrameThickness**: 0 - вимкнення ефекту Aero Glass
- **CornerRadius**: 0 - прямокутні кути (відповідає Material Design)
- **UseAeroCaptionButtons**: False - використання кастомних кнопок

### WindowChrome.IsHitTestVisibleInChrome:

Ця властивість встановлена на `True` для всіх кнопок, щоб вони реагували на кліки навіть у області заголовку.

## Можливі покращення

Наступні функції можна додати у майбутніх версіях:

1. **Анімації** - плавні переходи при зміні стану вікна
2. **Додаткові кнопки** - швидкі дії (налаштування, допомога)
3. **Breadcrumbs** - відображення поточного розділу
4. **Пошук** - глобальний пошук у заголовку
5. **Користувацький профіль** - аватар та ім'я користувача
6. **Налаштування теми** - перемикач світлої/темної теми

## Сумісність

- ? Windows 10
- ? Windows 11
- ? .NET 10
- ? Всі роздільні здатності екрану
- ? Мульти-моніторні конфігурації

---

**Реалізовано:** Кастомний заголовок вікна з Material Design стилем та повною функціональністю! ??
