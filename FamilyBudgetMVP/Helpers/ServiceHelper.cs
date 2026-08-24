namespace FamilyBudgetMVP.Helpers
{
    /// <summary>Доступ к DI-контейнеру из мест, куда он не инжектится (конвертеры, XAML).</summary>
    public static class ServiceHelper
    {
        public static T Get<T>() where T : notnull =>
            IPlatformApplication.Current!.Services.GetRequiredService<T>();
    }
}
