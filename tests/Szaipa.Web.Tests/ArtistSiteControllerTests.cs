using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Controllers;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Services.Admin;
using Xunit;

namespace Szaipa.Web.Tests;

public sealed class ArtistSiteControllerTests
{
    [Fact]
    public async Task Index_shows_linked_publications_in_saved_order_before_unselected_options()
    {
        var selectedInOrder = new[]
        {
            new PublicationOption(92001, "先展示", null, null),
            new PublicationOption(92007, "后展示", null, null)
        };
        var repository = new StubArtistSiteAdminRepository(selectedInOrder);
        var controller = new ArtistSiteController(repository, new StubAssetStorage());

        var result = await controller.Index(1, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ArtistSiteViewModel>(view.Model);
        Assert.Equal(
            selectedInOrder.Select(option => option.Id),
            model.Publications.Take(selectedInOrder.Length).Select(option => option.Id));
        Assert.Equal(
            model.Publications.Skip(selectedInOrder.Length).OrderByDescending(option => option.Id).Select(option => option.Id),
            model.Publications.Skip(selectedInOrder.Length).Select(option => option.Id));
        Assert.Equal(selectedInOrder.Select(option => option.Id), model.Exhibitions.Linked.Select(option => option.Id));
    }

    private sealed class StubArtistSiteAdminRepository : IArtistSiteAdminRepository
    {
        private readonly IReadOnlyList<PublicationOption> _selectedInOrder;

        public StubArtistSiteAdminRepository(IReadOnlyList<PublicationOption> selectedInOrder)
        {
            _selectedInOrder = selectedInOrder;
        }

        public Task<bool> SupportsWorksSortAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<ArtistSiteOverview?> GetOverviewAsync(int artistId, CancellationToken cancellationToken) =>
            Task.FromResult<ArtistSiteOverview?>(new ArtistSiteOverview
            {
                Artist = new Artist { Id = artistId, ArtistNameCN = "测试艺术家" }
            });

        public Task<ArtistWorkList> GetWorksAsync(int artistId, CancellationToken cancellationToken) =>
            Task.FromResult(new ArtistWorkList { Items = Array.Empty<ArtistWorkItem>() });

        public Task<IReadOnlyList<int>> AddWorksAsync(
            int artistId,
            IReadOnlyList<NewArtistWork> works,
            AdminActor actor,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> UpdateWorkAsync(
            int artistId,
            int workId,
            string? title,
            string? content,
            AdminActor actor,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> DeleteWorkAsync(
            int artistId,
            int workId,
            AdminActor actor,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReorderWorksAsync(
            int artistId,
            IReadOnlyList<int> orderedWorkIds,
            AdminActor actor,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<PublicationOption>> GetPublicationOptionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PublicationOption>>(
            [
                new PublicationOption(92001, "先展示", null, null),
                new PublicationOption(92007, "后展示", null, null)
            ]);

        public Task<ArtistExhibitionLinks> GetExhibitionLinksAsync(int artistId, CancellationToken cancellationToken) =>
            Task.FromResult(new ArtistExhibitionLinks
            {
                Linked = _selectedInOrder,
                HideDefaultExhibitions = true
            });

        public Task<bool> SaveExhibitionLinksAsync(
            int artistId,
            IReadOnlyList<int> orderedPublicationIds,
            bool hideDefaultExhibitions,
            AdminActor actor,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubAssetStorage : IAdminAssetStorage
    {
        public Task<AdminAssetSaveResult> SaveImageAsync(
            IFormFile file,
            string subfolder,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> SaveOriginalAsync(
            IFormFile original,
            string subfolder,
            string croppedFileName,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> CloneOriginalAsync(
            string subfolder,
            string previousFileName,
            string croppedFileName,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public string? FindOriginalUrl(string subfolder, string fileName) => throw new NotSupportedException();
    }
}
