using KamiYomu.Web.Entities.Integrations;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface IKavitaService
{

    Task<IReadOnlyList<KavitaLibrary>> LoadAllCollectionsAsync(CancellationToken cancellationToken);
    Task<bool> TestConnection(KavitaSettings kavitaSettings, CancellationToken cancellationToken);
    Task UpdateAllCollectionsAsync(CancellationToken cancellationToken);
}
