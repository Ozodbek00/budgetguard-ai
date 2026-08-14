using System.Globalization;
using System.Reflection;
using BudgetGuard.Application.Analysis.Services;
using BudgetGuard.Application.Common.Behaviours;
using BudgetGuard.Domain.Demo;
using BudgetGuard.Domain.Detection;
using BudgetGuard.Domain.Detection.Benford;
using BudgetGuard.Domain.Detection.Concentration;
using BudgetGuard.Domain.Detection.Explanations;
using BudgetGuard.Domain.Detection.Outliers;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BudgetGuard.Application;

/// <summary>Composition for the application and domain layers.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR handlers, validators, the detection engine and the
    /// analysis cache.
    /// <para>
    /// The domain detectors are registered here rather than in Infrastructure
    /// because they have no infrastructure concerns — they are pure functions
    /// over settings. Binding <see cref="DetectionSettings"/> from configuration
    /// is what makes every threshold adjustable without a rebuild.
    /// </para>
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DetectionSettings>()
            .Bind(configuration.GetSection(DetectionSettings.SectionName))
            .ValidateOnStart();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        services.AddTransient(
            typeof(MediatR.IPipelineBehavior<,>),
            typeof(ValidationBehaviour<,>));

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Detectors are stateless, but the language of the sentences they write
        // is per-request, so they are scoped rather than singleton and every
        // one of them is built from the SAME writer instance. Mixing languages
        // here would produce a finding whose sentence and evidence table
        // disagreed.
        services.AddScoped<IExplanationWriter>(_ =>
            ExplanationWriters.For(CultureInfo.CurrentUICulture.Name));

        services.AddScoped<IBenfordAnalyzer>(sp =>
            new BenfordAnalyzer(Settings(sp).Benford, Writer(sp)));

        services.AddScoped<IZScoreOutlierDetector>(sp =>
            new ZScoreOutlierDetector(Settings(sp).ZScore, Writer(sp)));

        services.AddScoped<IVendorConcentrationAnalyzer>(sp =>
            new VendorConcentrationAnalyzer(Settings(sp).VendorConcentration, Writer(sp)));

        services.AddScoped<IAnomalyAggregator>(sp => new AnomalyAggregator(
            sp.GetRequiredService<IBenfordAnalyzer>(),
            sp.GetRequiredService<IZScoreOutlierDetector>(),
            sp.GetRequiredService<IVendorConcentrationAnalyzer>(),
            Settings(sp),
            Writer(sp)));

        services.AddSingleton<SyntheticDatasetGenerator>();
        services.AddSingleton<IAnalysisCache, AnalysisCache>();
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IAnalysisService, AnalysisService>();

        return services;
    }

    private static DetectionSettings Settings(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<DetectionSettings>>().Value;

    private static IExplanationWriter Writer(IServiceProvider provider) =>
        provider.GetRequiredService<IExplanationWriter>();
}
