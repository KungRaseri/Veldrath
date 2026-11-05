using System.Globalization;
using System.Reflection;
using Xunit.Sdk;

namespace RealmEngine.Shared.Tests.Infrastructure;

/// <summary>
/// Pins <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/> for the
/// duration of one test method and restores the previous values afterwards.
/// </summary>
/// <remarks>
/// Used by the formatting tests: the product interpolates numbers with the current culture
/// (<c>$"{value:F2}x"</c>), so an assertion that hard-codes a dot decimal separator is only valid
/// under a dot-decimal culture. Pinning here keeps the product semantics intact — the expectation is
/// still "this is what a dot-decimal player sees" — while making the test independent of the machine
/// locale it happens to run under.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class UseCultureAttribute : BeforeAfterTestAttribute
{
    private readonly string _cultureName;
    private CultureInfo? _originalCulture;
    private CultureInfo? _originalUiCulture;

    /// <summary>Initializes a new instance of the <see cref="UseCultureAttribute"/> class.</summary>
    /// <param name="cultureName">The culture name to pin, e.g. <c>en-US</c>.</param>
    public UseCultureAttribute(string cultureName) => _cultureName = cultureName;

    /// <inheritdoc />
    public override void Before(MethodInfo methodUnderTest)
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUiCulture = CultureInfo.CurrentUICulture;

        var culture = CultureInfo.GetCultureInfo(_cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <inheritdoc />
    public override void After(MethodInfo methodUnderTest)
    {
        CultureInfo.CurrentCulture = _originalCulture!;
        CultureInfo.CurrentUICulture = _originalUiCulture!;
    }
}
