(function ($) {
    "use strict";

    if (!$ || !$.validator || !$.validator.unobtrusive) {
        return;
    }

    function parseFlexibleDecimal(value) {
        var normalized = String(value)
            .trim()
            .replace(/[\s\u00a0\u202f]/g, "");
        var lastComma = normalized.lastIndexOf(",");
        var lastDot = normalized.lastIndexOf(".");

        if (lastComma >= 0 && lastComma > lastDot) {
            normalized = normalized.replace(/\./g, "").replace(",", ".");
        } else {
            normalized = normalized.replace(/,/g, "");
        }

        if (!/^[+-]?(?:\d+(?:\.\d+)?|\.\d+)$/.test(normalized)) {
            return NaN;
        }

        return Number(normalized);
    }

    $.validator.methods.number = function (value, element) {
        return this.optional(element) || Number.isFinite(parseFlexibleDecimal(value));
    };

    $.validator.methods.min = function (value, element, parameter) {
        return this.optional(element) || parseFlexibleDecimal(value) >= parseFlexibleDecimal(parameter);
    };

    $.validator.methods.max = function (value, element, parameter) {
        return this.optional(element) || parseFlexibleDecimal(value) <= parseFlexibleDecimal(parameter);
    };

    $.validator.methods.range = function (value, element, parameters) {
        var number = parseFlexibleDecimal(value);
        return this.optional(element)
            || number >= parseFlexibleDecimal(parameters[0])
            && number <= parseFlexibleDecimal(parameters[1]);
    };

    $.validator.addMethod("mustbetrue", function (_value, element) {
        return element.checked;
    });
    $.validator.unobtrusive.adapters.addBool("mustbetrue");

    function messageContainer(form, fieldName) {
        return Array.from(form.querySelectorAll("[data-valmsg-for]"))
            .find(function (container) {
                return container.getAttribute("data-valmsg-for") === fieldName;
            });
    }

    $.validator.unobtrusive.options = {
        showErrors: function (errorMap, errorList) {
            var validator = this;
            var form = validator.currentForm;
            var invalidNames = new Set(Object.keys(errorMap));

            Array.from(form.elements).forEach(function (element) {
                if (!element.name) {
                    return;
                }

                var invalid = invalidNames.has(element.name);
                element.classList.toggle(validator.settings.errorClass, invalid);
                element.setAttribute("aria-invalid", invalid ? "true" : "false");

                var container = messageContainer(form, element.name);
                if (!container) {
                    return;
                }

                container.classList.toggle("field-validation-error", invalid);
                container.classList.toggle("field-validation-valid", !invalid);
                container.textContent = invalid ? errorMap[element.name] : "";
            });

            var summary = form.querySelector("[data-valmsg-summary='true']");
            if (summary) {
                summary.classList.toggle("validation-summary-errors", errorList.length > 0);
                summary.classList.toggle("validation-summary-valid", errorList.length === 0);
                var list = summary.querySelector("ul");
                if (list) {
                    list.replaceChildren.apply(
                        list,
                        errorList.map(function (error) {
                            var item = document.createElement("li");
                            item.textContent = error.message;
                            return item;
                        }));
                }
            }
        }
    };
}(window.jQuery));
