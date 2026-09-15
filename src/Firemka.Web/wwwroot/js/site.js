// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("DOMContentLoaded", function () {
    var toggle = document.querySelector("[data-navigation-toggle]");
    var navigation = document.getElementById("main-navigation");

    if (!toggle || !navigation) {
        return;
    }

    toggle.addEventListener("click", function () {
        var expanded = toggle.getAttribute("aria-expanded") === "true";
        toggle.setAttribute("aria-expanded", expanded ? "false" : "true");
        navigation.classList.toggle("show", !expanded);
    });
});
