using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using VMixParasite.Services;
using VMixParasite.ViewModels;

namespace VMixParasite;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<VmixApiService>();
		builder.Services.AddSingleton<MainViewModel>();
		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
