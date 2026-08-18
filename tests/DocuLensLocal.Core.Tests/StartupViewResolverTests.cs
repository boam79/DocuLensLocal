using System.Text.Json;
using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class StartupViewResolverTests
{
    [Fact]
    public void first_run_without_folder_or_index_shows_folder_select()
    {
        var view = StartupViewResolver.Resolve(new AppSettings(), indexedDocumentCount: 0);

        Assert.Equal(StartupView.FirstRunFolderSelect, view);
    }

    [Fact]
    public void folder_selected_but_never_indexed_stays_on_folder_select()
    {
        var settings = new AppSettings { IndexFolder = @"C:\Users\tttt\Desktop\인수인계\계약서_스캔" };

        var view = StartupViewResolver.Resolve(settings, indexedDocumentCount: 0);

        Assert.Equal(StartupView.FirstRunFolderSelect, view);
    }

    [Fact]
    public void existing_documents_in_index_open_search()
    {
        var settings = new AppSettings { IndexFolder = @"C:\docs" };

        var view = StartupViewResolver.Resolve(settings, indexedDocumentCount: 276);

        Assert.Equal(StartupView.MainSearch, view);
    }

    [Fact]
    public void successful_empty_index_still_opens_search()
    {
        var settings = new AppSettings
        {
            IndexFolder = @"C:\empty",
            IndexCompleted = true,
        };

        var view = StartupViewResolver.Resolve(settings, indexedDocumentCount: 0);

        Assert.Equal(StartupView.MainSearch, view);
    }

    [Fact]
    public void documents_without_completed_flag_still_open_search()
    {
        var view = StartupViewResolver.Resolve(new AppSettings(), indexedDocumentCount: 1);

        Assert.Equal(StartupView.MainSearch, view);
    }

    [Fact]
    public void old_settings_json_without_index_completed_defaults_false()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"IndexFolder\":\"C:\\\\docs\"}");

        Assert.NotNull(settings);
        Assert.False(settings.IndexCompleted);
        Assert.Equal(@"C:\docs", settings.IndexFolder);
    }
}
