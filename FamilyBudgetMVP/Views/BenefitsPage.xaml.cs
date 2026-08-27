using System.Globalization;
using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Views
{
    /// <summary>
    /// Справочник льгот и субсидий: поиск по региону/тегам, метка актуальности,
    /// обратная связь об устаревших данных + шаблоны документов (ТЗ MVP, этап 3).
    /// </summary>
    public partial class BenefitsPage : ContentPage
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        private readonly BenefitsService _benefits;
        private readonly IFileSaver _fileSaver;

        private bool _suppressSearch;

        public BenefitsPage(BenefitsService benefits, IFileSaver fileSaver)
        {
            InitializeComponent();
            _benefits = benefits;
            _fileSaver = fileSaver;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            PremiumLock.IsVisible = !FeatureGate.IsUnlocked(Feature.FullBenefits);

            try
            {
                if (RegionPicker.ItemsSource == null || RegionPicker.Items.Count <= 1)
                {
                    _suppressSearch = true;
                    var regions = await _benefits.GetRegionsAsync();
                    foreach (var r in regions)
                        RegionPicker.Items.Add(r);
                    RegionPicker.SelectedIndex = 0;
                    _suppressSearch = false;
                }

                var actual = await _benefits.GetLatestUpdatedAtAsync();
                ActualLabel.Text = $"Данные справочника актуальны на {actual:d MMMM yyyy} г.";

                await ReloadResultsAsync();
                BuildTemplatesSection();
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Льготы: загрузка");
                ActualLabel.Text = "Не удалось загрузить справочник";
            }
        }

        private async void OnPremiumClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//settings");
        }

        private async void OnSearchChanged(object? sender, TextChangedEventArgs e) => await ReloadResultsAsync();

        private async void OnRefreshCatalogClicked(object? sender, EventArgs e)
        {
            try
            {
                int count = await _benefits.RefreshFromUrlAsync(BenefitsService.DefaultCatalogUrl);

                // Перезаполняем пикер регионов и обновляем список
                RegionPicker.Items.Clear();
                var regions = await _benefits.GetRegionsAsync();
                foreach (var r in regions)
                    RegionPicker.Items.Add(r);
                RegionPicker.SelectedIndex = 0;

                ActualLabel.Text = $"Справочник обновлён: {count} записей";
                await ReloadResultsAsync();
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Льготы: обновление справочника");
                await DisplayAlertAsync("Не удалось обновить",
                    $"Проверьте подключение к интернету.\n{ex.Message}", "OK");
            }
        }

        private async void OnRegionChanged(object? sender, EventArgs e)
        {
            if (_suppressSearch)
                return;
            await ReloadResultsAsync();
        }

        private string? SelectedRegion =>
            RegionPicker.SelectedIndex is > 0 ? RegionPicker.SelectedItem as string : null;

        private async Task ReloadResultsAsync()
        {
            try
            {
                var items = await _benefits.SearchAsync(SelectedRegion, SearchEntry.Text);

                ResultsList.Children.Clear();

                ResultsCountLabel.IsVisible = true;
                ResultsCountLabel.Text = $"{items.Count} {(items.Count % 10 == 1 && items.Count % 100 != 11 ? "запись" : "записей")}";
                EmptyState.IsVisible = items.Count == 0;

                foreach (var b in items)
                    ResultsList.Children.Add(BuildBenefitCard(b));
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Льготы: поиск");
            }
        }

        private Border BuildBenefitCard(Benefit b)
        {
            static Label InfoLabel(string text) => new()
            {
                Text = text,
                FontSize = 13,
                LineBreakMode = LineBreakMode.WordWrap,
                TextColor = (Color)Application.Current!.Resources["InkSecondary"]
            };

            var report = new Button
            {
                Text = "⚠ Сообщить об ошибке",
                FontSize = 11,
                Padding = new Thickness(8, 4),
                CornerRadius = 8,
                BackgroundColor = Colors.Transparent,
                BorderWidth = 1,
                BorderColor = (Color)Application.Current.Resources["SurfaceBorder"],
                TextColor = (Color)Application.Current.Resources["InkTertiary"],
                HorizontalOptions = LayoutOptions.Start
            };
            report.Clicked += async (_, _) => await OnReportErrorAsync(b);

            var content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = b.Name, FontFamily = "OpenSansSemibold", FontSize = 15, LineBreakMode = LineBreakMode.WordWrap },
                    new Label
                    {
                        Text = b.Region,
                        FontSize = 11,
                        TextColor = (Color)Application.Current.Resources["InkTertiary"]
                    },
                    InfoLabel(b.Description),
                    InfoLabel($"Кто может: {b.Conditions}"),
                    InfoLabel($"Документы: {b.Documents}"),
                    InfoLabel($"Куда обращаться: {b.WhereToApply}"),
                    new Label
                    {
                        Text = $"Актуально на {b.UpdatedAt.ToString("d MMMM yyyy", Ru)} г.",
                        FontSize = 11,
                        TextColor = (Color)Application.Current.Resources["InkTertiary"]
                    },
                    report
                }
            };

            return new Border
            {
                Style = (Style)Application.Current.Resources["Card"],
                Content = content
            };
        }

        private async Task OnReportErrorAsync(Benefit b)
        {
            try
            {
                var message = new EmailMessage
                {
                    Subject = $"Ошибка в справочнике льгот: {b.Name}",
                    Body = $"Запись: {b.Name}\nРегион: {b.Region}\n\nЧто не так: ",
                    To = { "feedback@familybudget.local" }
                };
                await Email.ComposeAsync(message);
            }
            catch (Exception ex)
            {
                // Почтового клиента нет — предлагаем скопировать текст жалобы
                LogService.Error(ex, "Льготы: обратная связь");
                await Clipboard.SetTextAsync($"Ошибка в справочнике льгот: {b.Name} ({b.Region})");
                await DisplayAlertAsync("Спасибо!", "Почтовый клиент не найден. Текст обращения скопирован в буфер обмена.", "OK");
            }
        }

        private void BuildTemplatesSection()
        {
            TemplatesList.Children.Clear();

            foreach (var t in DocTemplateCatalog.All)
            {
                var open = new Button
                {
                    Text = "Открыть",
                    Style = (Style)Application.Current.Resources["GhostDelete"],
                    WidthRequest = 96,
                    HeightRequest = 40,
                    FontSize = 13,
                    VerticalOptions = LayoutOptions.Center
                };
                open.Clicked += async (_, _) => await OpenTemplateAsync(t);

                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new(GridLength.Star),
                        new(GridLength.Auto)
                    },
                    Children =
                    {
                        new VerticalStackLayout
                        {
                            Spacing = 2,
                            Children =
                            {
                                new Label { Text = t.Title, FontFamily = "OpenSansSemibold", FontSize = 15 },
                                new Label
                                {
                                    Text = t.Description,
                                    FontSize = 12,
                                    TextColor = (Color)Application.Current!.Resources["InkSecondary"]
                                }
                            }
                        },
                        open
                    }
                };

                TemplatesList.Children.Add(new Border
                {
                    Style = (Style)Application.Current.Resources["Card"],
                    Content = row
                });
            }
        }

        private async Task OpenTemplateAsync(DocTemplate template)
        {
            try
            {
                string content = await DocTemplateCatalog.LoadContentAsync(template);

                string action = await DisplayActionSheetAsync(
                    template.Title, "Закрыть", "Скачать файл…", "Скопировать текст");

                if (action == "Скопировать текст")
                {
                    await Clipboard.SetTextAsync(content);
                    await DisplayAlertAsync("Готово", "Текст шаблона скопирован.", "OK");
                }
                else if (action == "Скачать файл…")
                {
                    string? path = await _fileSaver.SaveTextAsync($"{template.AssetPath.Split('/').Last()}", content);
                    await DisplayAlertAsync("Сохранено", path != null ? $"Файл сохранён:\n{path}" : "Сохранение отменено", "OK");
                }
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Шаблоны: открытие");
                await DisplayAlertAsync("Ошибка", "Не удалось открыть шаблон.", "OK");
            }
        }
    }
}
