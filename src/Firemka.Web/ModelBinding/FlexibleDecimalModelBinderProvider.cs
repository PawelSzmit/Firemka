using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Firemka.Web.ModelBinding;

public sealed class FlexibleDecimalModelBinderProvider : IModelBinderProvider
{
    private static readonly IModelBinder Binder = new FlexibleDecimalModelBinder();

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType)
            ?? context.Metadata.ModelType;
        return type == typeof(decimal) ? Binder : null;
    }

    private sealed class FlexibleDecimalModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
            var value = valueResult.FirstValue;
            if (string.IsNullOrWhiteSpace(value))
            {
                if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
                {
                    bindingContext.Result = ModelBindingResult.Success(null);
                }

                return Task.CompletedTask;
            }

            var normalized = value
                .Trim()
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("\u00a0", string.Empty, StringComparison.Ordinal)
                .Replace("\u202f", string.Empty, StringComparison.Ordinal);
            var lastComma = normalized.LastIndexOf(",", StringComparison.Ordinal);
            var lastDot = normalized.LastIndexOf(".", StringComparison.Ordinal);
            var usesDecimalComma = lastComma >= 0 && lastComma > lastDot;
            var parsingCulture = usesDecimalComma
                ? CultureInfo.GetCultureInfo("pl-PL")
                : CultureInfo.InvariantCulture;
            normalized = usesDecimalComma
                ? normalized.Replace(".", string.Empty, StringComparison.Ordinal)
                : normalized.Replace(",", string.Empty, StringComparison.Ordinal);

            if (decimal.TryParse(normalized, NumberStyles.Number, parsingCulture, out var parsed))
            {
                bindingContext.Result = ModelBindingResult.Success(parsed);
                return Task.CompletedTask;
            }

            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"Wartość „{value}” nie jest poprawną liczbą.");
            return Task.CompletedTask;
        }
    }
}
