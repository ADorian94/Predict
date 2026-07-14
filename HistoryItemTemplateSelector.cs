using Predict.Models;

namespace Predict;

public class HistoryItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate HistoryTemplate { get; set; } = null!;
    public DataTemplate AdTemplate { get; set; } = null!;

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        item is AdPlaceholder ? AdTemplate : HistoryTemplate;
}
