using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Firemka.Web.TagHelpers;

[HtmlTargetElement("firemka-validation-summary")]
public sealed class FiremkaValidationSummaryTagHelper : TagHelper
{
    [HtmlAttributeName("summary")]
    public ValidationSummary Summary { get; set; } = ValidationSummary.ModelOnly;

    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);

        var errors = ViewContext.ViewData.ModelState
            .Where(entry => Summary == ValidationSummary.All || string.IsNullOrEmpty(entry.Key))
            .SelectMany(entry => entry.Value?.Errors ?? [])
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "Nie udało się przetworzyć formularza."
                : error.ErrorMessage)
            .ToArray();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("data-valmsg-summary", "true");

        var existingClasses = output.Attributes["class"]?.Value?.ToString();
        var stateClass = errors.Length == 0
            ? "validation-summary-valid"
            : "validation-summary-errors";
        output.Attributes.SetAttribute(
            "class",
            string.Join(' ', new[] { existingClasses, stateClass }.Where(value => !string.IsNullOrWhiteSpace(value))));

        var list = new TagBuilder("ul");
        foreach (var error in errors)
        {
            var item = new TagBuilder("li");
            item.InnerHtml.Append(error);
            list.InnerHtml.AppendHtml(item);
        }

        output.Content.SetHtmlContent(list);
    }
}
